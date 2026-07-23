# Phase 2: Repository layer

## Requirements

Move ALL EF Core access out of `Endpoints/` into `Repository/` layer, implementing the five repository interfaces from Phase 1. Do this as **one complete pass across all 25 routes**, not endpoint-by-endpoint — per the Submission/Grading precedent, a half-migrated state (some routes on repositories, others still on raw `CatalogDbContext`) is riskier than a single complete swap when there's no automated test net.

Endpoints in this phase **do** start calling repositories instead of `CatalogDbContext` — but business rules/authorization/orchestration stay inline in the endpoint for now (that's Phase 3). This phase only relocates *data access*.

## Design Constraints

- The `IsolationLevel.Serializable` transaction inside `IEnrollmentRepository.UpsertStudentAsync`/`CorrectAdminAsync` must be entirely inside the repository method — no `BeginTransactionAsync`/`CommitAsync` call may remain in `StudentEnrollmentEndpoints.cs` or `AdminEnrollmentEndpoints.cs` after this phase. Same for `IClassRepository.CreateAsync`/`UpdateAsync` (currently use default transaction in `SaveAndPublishAsync` in ClassesEndpoints).
- **`IEnrollmentRepository` must leave the `DbContext` in a clean state on transaction failure.** The current code's `EnrollmentCommands` class has `RollbackAndClearAsync` (lines 247–251) that calls `db.ChangeTracker.Clear()` on conflict — this cleanup must move inside the repository methods themselves (`catch { db.ChangeTracker.Clear(); throw; }`) so a failed upsert never leaves stale tracked entities for a subsequent call.
- Repository methods must not accept or return HTTP types (`IResult`, `ClaimsPrincipal`) — only domain types, plain scalars, or the `EnrollmentCommandResult<T>` discriminated union (which is not an HTTP type, just application logic).
- `CatalogDbContext` must no longer be referenced from `Endpoints/` after this phase (grep for `CatalogDbContext` in `Endpoints/*.cs` should return zero matches except in method signatures where repositories inject it implicitly).
- The `EnrollmentCommandResult<T>` pattern (currently in `EnrollmentContracts.cs`) stays as-is; do not force it into exception-based signaling — the current result-type discriminator has proven itself for multi-outcome operations and should not be "fixed" (per plan decision #4).
- **Composite-FK relationship preservation:** The constraint between `StudentEnrollment` and `Class` via `(ClassId, SubjectId)` → `(Id, EnrollmentSubjectId)` must be validated exactly as today (check via `db.Classes.AnyAsync(item => item.Id == classId && item.SubjectId == subjectId)`). `IEnrollmentRepository` queries this constraint directly using its own `DbContext` reference, not by calling out to `IClassRepository` (both repos share the same scoped instance anyway, and crossing repo boundaries inside a transaction would split the transaction boundary).

## Steps

1. Move `Data/CatalogDbContext.cs` → `Repository/CatalogDbContext.cs`. Update namespace/usings everywhere it's referenced:
   - `Endpoints/SubjectsEndpoints.cs` (line 5) — will be removed in this phase.
   - `Endpoints/AssignmentsEndpoints.cs` (line 2) — will be removed in this phase.
   - `Endpoints/ClassesEndpoints.cs` (line 2) — will be removed in this phase.
   - `Endpoints/RubricsEndpoints.cs` (line 2) — will be removed in this phase.
   - `Endpoints/EnrollmentQueries.cs` (line 1) — will be removed in this phase.
   - `Endpoints/EnrollmentCommands.cs` (line 2) — will be removed in this phase.
   - `Jobs/RubricParsingJob.cs` (line 1) — update namespace now; the job still depends on `CatalogDbContext` directly until Phase 5.
   - `Program.cs` (line 15) — update namespace where `AddDbContext` is called.

2. Create `Repository/SubjectRepository.cs` implementing `ISubjectRepository`, extracting EF Core access from `SubjectsEndpoints.cs`:
   - `ListAsync` → the query built across lines 34–42 (search filter, pagination).
   - `ListOpenAsync` → the query built across lines 52–55 (RegistrationStatus.Open filter, pagination).
   - `GetByIdAsync` → `db.Subjects.FirstOrDefaultAsync(item => item.Id == id)` (conceptual, not currently used by endpoint but needed for consistency).
   - `CreateAsync` → adds subject, catches `DbUpdateException` (line 113) and translates to domain-level error (or throws a domain exception), `SaveChangesAsync` (line 111).
   - `UpdateRegistrationAsync` → the Serializable transaction block (lines 132–144): fetch subject, update status, `SaveChangesAsync`, commit, return subject. Wrap entire block in comment explaining the Serializable isolation.

3. Create `Repository/AssignmentRepository.cs` implementing `IAssignmentRepository`, extracting EF Core access from `AssignmentsEndpoints.cs`:
   - `ListAsync` → the query built across lines 18–29 (optional `subjectId` filter, pagination).
   - `GetByIdAsync` → `db.Assignments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id)` (lines 37).
   - `CreateAsync` → adds assignment, `SaveChangesAsync` (line 54).
   - `UpdateAsync` → fetch assignment, update fields, `SaveChangesAsync` (line 68).

4. Create `Repository/ClassRepository.cs` implementing `IClassRepository`, extracting EF Core access from `ClassesEndpoints.cs`:
   - `ListAsync` → legacy list (no filters, lines 38–44).
   - `ListAdminAsync` → the query built across lines 57–74 (optional `subjectId` filter, pagination).
   - `ListForSubjectAsync` → the query built across lines 98–112 (subjectId filter, check registration status if student role).
   - `GetByIdAsync` → `db.Classes.FirstOrDefaultAsync(item => item.Id == id)`.
   - `CreateAsync` → adds class, wraps `SaveChangesAsync` in try-catch for `DbUpdateException`, returns error or class.
   - `UpdateAsync` → fetch class, update fields, return error or class.
   - `AnyAsync` → `db.Classes.AnyAsync(subject => subject.Id == id)`.
   - `AnyWithEnrollmentsAsync` → `db.StudentEnrollments.AnyAsync(enrollment => enrollment.ClassId == id)` (line 201).
   - `SaveAndPublishAsync` helper stays in repository: the Serializable transaction (lines 224–246) that commits the save and publishes the event. **Important:** this is orchestration-adjacent, but it's purely data-persistence logic (transaction + event publish), not business rule logic — keep it in the repository. Return null on success, `IResult` error on failure (or throw a domain exception and let Service catch it — choice deferred to Phase 3).

5. Create `Repository/EnrollmentRepository.cs` implementing `IEnrollmentRepository`, **extracting the entire `EnrollmentQueries` + `EnrollmentCommands` logic** and merging them. **CRITICAL:** `EnrollmentRepository.UpsertStudentAsync` and `CorrectAdminAsync` must query `Class` data directly via `db.Classes.AnyAsync(...)` / `db.Classes.FirstOrDefaultAsync(...)` using their own injected `CatalogDbContext` instance — NOT via `IClassRepository.GetAsync(...)` or any injected repository dependency. `EnrollmentRepository` must not take an `IClassRepository` constructor dependency at all. A cross-repository call inside the Serializable-transaction boundary would split the transaction across two logical units of work and silently break the atomicity guarantee this isolation level exists to provide. Both repositories share the same scoped `CatalogDbContext` instance within one HTTP request, so querying `db.Classes` directly from `EnrollmentRepository` is safe and equivalent:
   - Move `EnrollmentQueries` methods: `ListStudentAsync`, `ListAdminAsync`, `ListStudentIdsForLecturerAsync`, `GetStudentAsync`, `GetStudentByIdAsync`, `GetAdminAsync` (lines 9–124).
   - Move `EnrollmentCommands` methods: `UpsertStudentAsync`, `CorrectAdminAsync` (lines 12–166), **with the entire transaction/rollback/cleanup logic byte-for-byte, including `RollbackAndClearAsync`** (lines 247–251), `TryDecodeRowVersion` (lines 253–270), `IsConstraintConflict` (lines 273–274). **Add inline comment above transaction blocks:** `// CRITICAL: the Serializable transaction must stay entirely inside this method — the attempt-check and ClassId-change must execute atomically, never split across Service/Endpoints, that reopens the race condition this isolation level prevents.` Also: `// On failure, ChangeTracker.Clear() keeps this DbContext instance safe for subsequent operations.`
   - Return `EnrollmentCommandResult<EnrollmentSummary>` / `EnrollmentCommandResult<AdminEnrollmentSummary>` directly from the methods (do not convert to exceptions).

6. Create `Repository/RubricRepository.cs` implementing `IRubricRepository`, extracting EF Core access from `RubricsEndpoints.cs`:
   - `ListAsync` → the query built across lines 21–40 (subjectId/assignmentId filters, role-based visibility filter on status/lecturerId).
   - `GetByIdAsync` → the query built across lines 279 (with or without criteria included via `Include(r => r.Criteria)`).
   - `GetByAssignmentIdAsync` → `db.Rubrics.Include(r => r.Criteria).FirstOrDefaultAsync(r => r.AssignmentId == assignmentId)` (conceptual, used in upload logic).
   - `CreateAsync` → adds rubric, `SaveChangesAsync` (line 142).
   - `UpdateAsync` → fetch rubric, update fields, `SaveChangesAsync`.
   - `UpdateCriteriaAsync` → calls `db.ReplaceRubricCriteria(rubric, newCriteria)` (custom helper, line 190), `SaveChangesAsync`.
   - `ConfirmAsync` → calls `rubric.Confirm()` domain method (line 218), `SaveChangesAsync`, returns the rubric.
   - `UnlockAsync` → calls `rubric.Unlock()` domain method (line 259), `SaveChangesAsync`, returns the rubric.
   - `DownloadFileAsync` → `db.Rubrics.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id)` (line 46), returns rubric or null.
   - Both update/confirm/unlock methods wrap `SaveChangesAsync` in try-catch for `DbUpdateConcurrencyException` (line 307–310) and translate to domain error (or throw exception, Phase 3 decides).

7. Update all five endpoint files to inject their corresponding repositories instead of `CatalogDbContext`:
   - `SubjectsEndpoints.cs` (line 30): inject `ISubjectRepository repo` instead of `CatalogDbContext db`.
   - `AssignmentsEndpoints.cs` (line 14, 35, 42, 59): inject `IAssignmentRepository repo` instead of `CatalogDbContext db`.
   - `ClassesEndpoints.cs` (line 34, 49, etc.): inject `IClassRepository repo` instead of `CatalogDbContext db`.
   - `RubricsEndpoints.cs` (line 21, 44, 87, etc.): inject `IRubricRepository repo` instead of `CatalogDbContext db`.
   - `StudentEnrollmentEndpoints.cs` (line 20): inject `IEnrollmentRepository repo` instead of `EnrollmentCommands commands` (we'll wire the service in Phase 3; for now, skip refactoring the enrollment endpoints, as they're bound together with EnrollmentQueries/EnrollmentCommands).
   - Actually: **leave StudentEnrollmentEndpoints.cs, AdminEnrollmentEndpoints.cs, LecturerEnrollmentEndpoints.cs unchanged for now** — they're small and tightly bound to EnrollmentQueries/EnrollmentCommands. We'll update them in Phase 3 when we create `IEnrollmentService`.
   - Similarly, leave `EnrollmentQueries.cs` and `EnrollmentCommands.cs` untouched for now — they'll be deleted or refactored in Phase 3.

8. Update `Program.cs` DI section (lines 26–28): register the five repositories:
   - **Add** (do not remove `AddScoped<EnrollmentQueries>()` / `AddScoped<EnrollmentCommands>()` yet — `StudentEnrollmentEndpoints.cs`/`AdminEnrollmentEndpoints.cs`/`LecturerEnrollmentEndpoints.cs` still inject those types directly per step 7, so removing their registrations now would break DI resolution at runtime): `AddScoped<ISubjectRepository, SubjectRepository>()`, `AddScoped<IAssignmentRepository, AssignmentRepository>()`, `AddScoped<IClassRepository, ClassRepository>()`, `AddScoped<IEnrollmentRepository, EnrollmentRepository>()` (unused until Phase 3, harmless to register early), `AddScoped<IRubricRepository, RubricRepository>()`.
   - `EnrollmentQueries`/`EnrollmentCommands` registrations are removed in **Phase 3**, once `StudentEnrollmentEndpoints.cs`/`AdminEnrollmentEndpoints.cs`/`LecturerEnrollmentEndpoints.cs` switch to injecting `IEnrollmentService` instead.
   - Update the DbContext namespace (line 15).

## Success Criteria

- `dotnet build` on `AutoGrading.Catalog.Api` compiles with zero errors.
- Five repository `.cs` files exist in `Repository/` and are imported correctly.
- `grep -rn "CatalogDbContext" Endpoints/` returns zero matches (except internal comments explaining its removal).
- All 25 routes continue to work identically (manual pass through Swagger/Postman).

## Quality and Testing State

- Quality gate: not evaluated (Cook runs `/ck:quality --gate` after implementing this phase)
- Testing: not started

## Manual Verification

1. `dotnet build` — zero errors.
2. `grep -rn "CatalogDbContext" Endpoints/` returns nothing.
3. Verify `Jobs/RubricParsingJob.cs` namespace/using is updated and still references the moved `CatalogDbContext`.
4. Manually exercise all 25 routes end-to-end (via Swagger/Postman against a local run):
   - **Subjects (4 routes):** `GET /subjects`, `GET /subjects/open-for-registration`, `POST /subjects/` (create one), `PATCH /subjects/{id}/registration` (toggle open/closed) — confirm identical behavior.
   - **Assignments (4 routes):** `GET /assignments/`, `GET /assignments/{id}`, `POST /assignments/` (create), `PUT /assignments/{id}` (update) — confirm identical behavior.
   - **Classes (6 routes):** `GET /classes/`, `GET /classes/admin`, `GET /classes/by-subject/{subjectId}`, `POST /classes/` (legacy), `POST /classes/subject-scoped`, `PATCH /classes/{id}` (update lecturer) — confirm identical behavior.
   - **Rubrics (7 routes):** `GET /rubrics/`, `GET /rubrics/{id}/file`, `POST /rubrics/upload` (to MinIO, enqueue Hangfire), `POST /rubrics/{id}/retry-parsing`, `PATCH /rubrics/{id}/criteria`, `POST /rubrics/{id}/confirm`, `POST /rubrics/{id}/unlock` — confirm identical behavior, Hangfire job still enqueues.
   - **Enrollments (5 routes, most critical):** 
     - `GET /enrollments/me` as student — confirm paginated list of own enrollments, row-version base64 encoding preserved.
     - `PUT /enrollments/me/{subjectId}` as student — confirm upsert with optimistic-locking (`409 Conflict` on concurrent updates, no error when idempotent). **Simulate concurrent conflict:** Issue two PUT requests with stale rowVersion simultaneously, confirm both don't succeed (one gets `409 Conflict`).
     - `GET /enrollments/admin` as admin — confirm filter by studentId/subjectId/classId works.
     - `PUT /enrollments/admin/{studentId}/{subjectId}` as admin — confirm correction upsert works identically.
     - `GET /enrollments/lecturer-student-ids?subjectId=...&lecturerId=...` as lecturer — confirm returns distinct student IDs for that lecturer's classes in that subject.
