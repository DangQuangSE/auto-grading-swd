# Phase 3: Service layer

## Requirements

Move business logic, authorization decisions, and orchestration out of `Endpoints/` into five `Service/` classes implementing the service interfaces from Phase 1. Endpoints now inject services only, not repositories.

## Design Constraints

- Service methods must not accept or return HTTP types (`IResult`, `ClaimsPrincipal`, `IFormFile`) — work with domain types and DTOs only. Role checks (`user.IsInRole(...)`) stay in `Endpoints/` for now, or move into Service only as private helper methods that receive a `RequesterContext` record (not raw `ClaimsPrincipal`).
- No direct `DbContext` access in Service layer — all data access goes through repositories.
- Authorization helpers that are specific to a single service (e.g., enrollment row-version checks, rubric visibility filters) may move into Service as private methods; they call repository methods to fetch data but never touch DbContext directly.
- `Rubric` domain methods (`rubric.Confirm()`, `rubric.Unlock()`) are already business logic — Service calls them and then updates via repository.
- `IEnrollmentService` continues returning `EnrollmentCommandResult<T>` (not throwing exceptions) — this is the one deliberate exception to the exception-based-signaling convention, justified by design decision #4.
- File-upload orchestration in `RubricsEndpoints.UploadRubricAsync` (upload to MinIO, then enqueue Hangfire) stays in Endpoints for now — Service does not enqueue Hangfire jobs. This boundary is deliberate: split across layers risks leaving a rubric in Parsing state if the endpoint crashes between service return and enqueue.

## Steps

1. Create `Service/SubjectService.cs` implementing `ISubjectService`:
   - `ListAsync(search?, page?, pageSize?, ct)` → call `repo.ListAsync(search, page, pageSize, ct)`.
   - `ListOpenAsync(page?, pageSize?, ct)` → call `repo.ListOpenAsync(page, pageSize, ct)`.
   - `GetByIdAsync(id, ct)` → call `repo.GetByIdAsync(id, ct)`.
   - `CreateAsync(request, ct)` → validate input (code/name non-empty, length limits from `SubjectsEndpoints.cs` lines 87–98), create `Subject` domain object, call `repo.CreateAsync(subject, ct)`, catch domain exceptions and translate to HTTP results in the endpoint (Phase 4).
   - `UpdateRegistrationAsync(id, status, ct)` → validate `Enum.IsDefined(status)` (line 127), call `repo.UpdateRegistrationAsync(id, status, ct)`.

2. Create `Service/AssignmentService.cs` implementing `IAssignmentService`:
   - `ListAsync(subjectId?, page?, pageSize?, ct)` → call `repo.ListAsync(subjectId, page, pageSize, ct)`.
   - `GetByIdAsync(id, ct)` → call `repo.GetByIdAsync(id, ct)`.
   - `CreateAsync(request, ct)` → validate `MaxAttempts >= 1` (line 44), create `Assignment` domain object, call `repo.CreateAsync(assignment, ct)`.
   - `UpdateAsync(id, request, ct)` → validate `MaxAttempts >= 1`, fetch `assignment`, update fields, call `repo.UpdateAsync(id, assignment, ct)`.

3. Create `Service/ClassService.cs` implementing `IClassService`:
   - `ListLegacyAsync(ct)` → call `repo.ListAsync(ct)`.
   - `ListAdminAsync(subjectId?, page?, pageSize?, ct)` → call `repo.ListAdminAsync(subjectId, page, pageSize, ct)`.
   - `ListForSubjectAsync(subjectId, page?, pageSize?, isStudent, ct)` → call `repo.ListForSubjectAsync(subjectId, page, pageSize, ct)`, optionally pass `isStudent` flag to control visibility (future: could be moved to repository if needed).
   - `GetByIdAsync(id, ct)` → call `repo.GetByIdAsync(id, ct)`.
   - `CreateLegacyAsync(request, ct)` → validate input (name not empty, lecturer not Guid.Empty, lines 254–267), normalize name (lines 249–250), create `Class` domain object, call `repo.CreateAsync(class, ct)`, orchestrate event publish (move `SaveAndPublishAsync` logic here or keep in repo — see constraint above). Return `Class` on success, throw exception on conflict (Phase 4 catches and maps).
   - `CreateSubjectScopedAsync(request, ct)` → same as legacy + validate subject exists (line 143–145), set `EnrollmentSubjectId = SubjectId`.
   - `UpdateAsync(id, request, ct)` → fetch class, validate input (lecturer required if LecturerId provided, subject exists if SubjectId changed), check no enrollments exist before changing subject (line 201–207), update class fields, call `repo.UpdateAsync(id, class, ct)`, orchestrate event publish.

4. Create `Service/EnrollmentService.cs` implementing `IEnrollmentService`:
   - `ListStudentAsync(studentId, page?, pageSize?, ct)` → call `repo.ListStudentAsync(studentId, page, pageSize, ct)`.
   - `ListAdminAsync(studentId?, subjectId?, classId?, page?, pageSize?, ct)` → call `repo.ListAdminAsync(studentId, subjectId, classId, page, pageSize, ct)`.
   - `ListStudentIdsForLecturerAsync(lecturerId, subjectId, ct)` → call `repo.ListStudentIdsForLecturerAsync(lecturerId, subjectId, ct)`.
   - `UpsertStudentAsync(studentId, subjectId, classId, rowVersion?, ct)` → **call `repo.UpsertStudentAsync(studentId, subjectId, classId, rowVersion, ct)` and return its result directly** — no exception-throwing wrapping here. The repository already returns `EnrollmentCommandResult<EnrollmentSummary>` with all conflict/error details. Service passes it through unchanged.
   - `CorrectAdminAsync(studentId, subjectId, classId, rowVersion?, ct)` → same as above, call repository and return `EnrollmentCommandResult<AdminEnrollmentSummary>` directly.

5. Create `Service/RubricService.cs` implementing `IRubricService`:
   - `ListAsync(subjectId?, assignmentId?, userId?, isAdmin, ct)` → call `repo.ListAsync(subjectId, assignmentId, userId, isAdmin, ct)`.
   - `GetByIdAsync(id, includeCriteria, ct)` → call `repo.GetByIdAsync(id, includeCriteria, ct)`.
   - `UploadAsync(form, userId, isAdmin, ct)` → **does NOT upload to MinIO or enqueue Hangfire** — upload orchestration stays in Endpoints. This method validates scope (line 94–96: if `SchoolWide`, only admins allowed), checks existing rubric authorization (line 103–105: only lecturer owning the rubric or admin can re-upload), fetches existing rubric if `AssignmentId` is set, creates or updates rubric domain object, calls `repo.CreateAsync` or `repo.UpdateAsync`. **Returns the created/updated `Rubric` object (not a file key or job ID).** Endpoints will handle MinIO upload and Hangfire enqueue after this Service call succeeds.
   - `RetryParsingAsync(id, userId, isAdmin, ct)` → fetch rubric (call `repo.GetByIdAsync`), check authorization (only lecturer owner or admin), check status is `Parsing` (else conflict), return success. **Does NOT enqueue Hangfire job** — Endpoints does that after Service succeeds.
   - `UpdateCriteriaAsync(id, criteria, userId, isAdmin, ct)` → fetch rubric, check authorization, check status is `Draft`, call `repo.UpdateCriteriaAsync(id, criteria, ct)`.
   - `ConfirmAsync(id, userId, isAdmin, ct)` → fetch rubric, check authorization, call `rubric.Confirm()` domain method (may throw `InvalidOperationException`), call `repo.ConfirmAsync(rubric, ct)`, publish `RubricConfirmed` event (or move to Endpoints, Phase 4 decides).
   - `UnlockAsync(id, userId, isAdmin, ct)` → fetch rubric, check authorization, call `rubric.Unlock()` domain method (may throw `InvalidOperationException`), call `repo.UnlockAsync(rubric, ct)`.
   - `DownloadFileAsync(id, userId, isAdmin, ct)` → fetch rubric, check visibility (`CanView` logic from line 297–298: Confirmed rubrics are public, Draft/Parsing only visible to owner or admin), return download stream via `repo.DownloadFileAsync`.

6. Create authorization helper methods as needed:
   - Rubric visibility check: `IsAuthorized(rubric, userId, isAdmin)` → private method inside `RubricService`.
   - Rubric viewability check: `CanView(rubric, userId, isAdmin)` → private method inside `RubricService`.
   - **Scope clarification:** these helpers take scalar `Guid userId` / `bool isAdmin` parameters (matching every public `RubricService` method signature in step 5, e.g. `ConfirmAsync(id, userId, isAdmin, ct)`), not a `RequesterContext` record. `RubricService` does not need `RequesterContext`'s `IsStudent`/`IsLecturer` fields — only "is this user the owning lecturer or an admin" — so scalars are the minimal-surface choice here. This mirrors the endpoint-level role-check pattern already used elsewhere in this service and keeps the service API surface minimal; it does not need to match Identity's `RequesterContext` usage verbatim, since Identity's services genuinely branch on student/lecturer/admin three ways while Rubric only branches on owner-or-admin.
   - (Enrollment and Class authorization is implicit in role checks at the endpoint level, no separate helper needed yet.)

7. Update `Program.cs` DI section (after Phase 2's repository registrations):
   - Add: `AddScoped<ISubjectService, SubjectService>()`, `AddScoped<IAssignmentService, AssignmentService>()`, `AddScoped<IClassService, ClassService>()`, `AddScoped<IEnrollmentService, EnrollmentService>()`, `AddScoped<IRubricService, RubricService>()`.

## Success Criteria

- `dotnet build` on `AutoGrading.Catalog.Api` compiles with zero errors.
- Five service `.cs` files exist in `Service/` and are imported correctly.
- No `DbContext` or `IFormFile` or `ClaimsPrincipal` types appear in `Service/` files (grep search).
- No HTTP-specific logic (authorization filters, `Results.*` calls) in Service layer.
- All 25 routes continue to work identically (manual pass through Swagger/Postman).

## Quality and Testing State

- Quality gate: not evaluated (Cook runs `/ck:quality --gate` after implementing this phase)
- Testing: not started

## Manual Verification

1. `dotnet build` — zero errors.
2. `grep -rn "DbContext\|ClaimsPrincipal\|IResult\|IFormFile" Service/` returns nothing.
3. Manually exercise all 25 routes end-to-end (via Swagger/Postman against a local run):
   - **Subjects:** Same as Phase 2 tests.
   - **Assignments:** Same as Phase 2 tests.
   - **Classes:** Same as Phase 2 tests, plus verify `ClassLecturerAssigned` event still publishes (check via RabbitMQ/event logs).
   - **Rubrics:**
     - `GET /rubrics/` as lecturer — confirm only Confirmed rubrics + own Draft/Parsing rubrics are returned.
     - `POST /rubrics/upload` — confirm file is still uploaded to MinIO (Endpoints handles it), Hangfire job is still enqueued (Endpoints handles it).
     - `POST /rubrics/{id}/confirm` — confirm domain method `Confirm()` is called, `RubricConfirmed` event is published (check event logs).
     - `POST /rubrics/{id}/unlock` — confirm domain method `Unlock()` is called, status transitions back to `Draft`.
   - **Enrollments (most critical):** Same as Phase 2 tests (Service is mostly a pass-through to Repository for enrollments, per design decision #4).
