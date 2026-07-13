# Phase 2: Identity Event Consumers & Local Caches

## Requirements

Create local read-only cache tables in Identity (`ClassLecturerCache`, `SubmissionStudent`, `SubmissionGrader`), implement idempotent event handlers for `ClassLecturerAssigned`, `SubmissionUploaded`, and `GradePublished` events, and register the handlers in Identity's startup. These caches enable server-side authorization checks in Phase 4 without requiring HTTP calls to Catalog/Submission/Grading during request processing.

**Design note (revised after plan review — read before implementing):** the original design planned an eagerly-maintained `StudentGraderCache` (StudentId → set of LecturerIds), built by joining `SubmissionUploaded` and `GradePublished` as each arrived. That's wrong: those are two independent events from two different services with **no guaranteed arrival order** — `GradePublished` for a submission can legitimately arrive at Identity before `SubmissionUploaded` for the same submission (event bus redelivery, service restart timing, no ordering guarantee across queues). An eager join breaks in that case: the handler has a `LecturerId` but no `StudentId` to attach it to yet, and there's no clean way to "retry later" without either losing the grader fact or reaching for an HTTP fallback (which would violate the no-direct-inter-service-HTTP convention this codebase has followed throughout).

**Fix: store each event's fact in its own table, keyed by the one thing both events share (`SubmissionId`), and defer the join to query time in Phase 4.** Whichever event arrives first just writes its own row — no ordering dependency, no pending/retry state, no HTTP fallback needed:
- `SubmissionStudent` (SubmissionId **PK**, StudentId) — written by `SubmissionUploadedHandler`, single row per submission (a submission has exactly one student).
- `SubmissionGrader` (composite **PK**: `SubmissionId` + `LecturerId`) — written by `GradePublishedHandler`, **one row per (submission, lecturer) pair, not overwritten on re-grade** — per explicit product decision, every lecturer who has ever published a grade for a submission keeps edit authority over that student, even after a different lecturer re-grades it later. A re-grade by a new lecturer *adds* a new row; it does not remove the previous grader's row.
- At authorization-check time (Phase 4): `SELECT DISTINCT sg.LecturerId FROM SubmissionGrader sg JOIN SubmissionStudent ss ON sg.SubmissionId = ss.SubmissionId WHERE ss.StudentId = @targetStudentId` — this INNER JOIN naturally returns nothing until *both* rows exist for a given submission, regardless of which arrived first, and returns every lecturer who's ever graded any of that student's submissions since `SubmissionGrader` never deletes rows. No data loss, no special-casing.

This also sidesteps the earlier rubric-parsing feature's "mutate navigation collection instead of DbSet" bug class entirely — there's no parent-with-child-collection here, just single-row upserts keyed by `SubmissionId`, so there's nothing resembling a "replace all children" operation to get wrong.

## Steps

1. Add three new domain entities to Identity (`be/src/Services/Identity/AutoGrading.Identity.Api/Domain/`):
   - `ClassLecturerCache`: `ClassId` (Guid, **PK**), `ClassName` (string), `LecturerId` (Guid).
   - `SubmissionStudent`: `SubmissionId` (Guid, **PK**), `StudentId` (Guid).
   - `SubmissionGrader`: `SubmissionId` (Guid), `LecturerId` (Guid) — **composite PK on (SubmissionId, LecturerId)**, no single-column identity `Id`. Multiple rows per `SubmissionId` are expected and intentional (full grading-authority history, not just the most recent grader).
   `ClassLecturerCache` and `SubmissionStudent` are single-row-per-key upserts; `SubmissionGrader` is append-only (insert-if-not-exists on the composite key, never update/overwrite).

2. Update `IdentityDbContext` to include `DbSet<ClassLecturerCache>`, `DbSet<SubmissionStudent>`, `DbSet<SubmissionGrader>`. Configure `ClassLecturerCache`/`SubmissionStudent`'s single-column PK and `SubmissionGrader`'s composite key (`entity.HasKey(g => new { g.SubmissionId, g.LecturerId })`) explicitly in `OnModelCreating`, and add an index on `SubmissionStudent.StudentId` (this is the lookup direction Phase 4's authorization query needs).

3. Create an EF Core migration `AddIdentityAuthorizationCaches` that creates all three tables.

4. Create three event handler classes in `be/src/Services/Identity/AutoGrading.Identity.Api/Handlers/`, each implementing `IIntegrationEventHandler<TEvent>`:
   - `ClassLecturerAssignedHandler(IdentityDbContext db, ILogger<ClassLecturerAssignedHandler>)`
   - `SubmissionUploadedHandler(IdentityDbContext db, ILogger<SubmissionUploadedHandler>)`
   - `GradePublishedHandler(IdentityDbContext db, ILogger<GradePublishedHandler>)`

5. `ClassLecturerAssignedHandler.HandleAsync()`: `FirstOrDefaultAsync(c => c.ClassId == @event.ClassId)`; if found, update `ClassName`/`LecturerId` in place; if not found, `db.ClassLecturerCaches.Add(new ...)`. `SaveChangesAsync()`. Redelivery of the same event is a no-op update (same values written again) — idempotent by construction, no special redelivery-detection logic needed since it's a single-row upsert-by-PK, not a collection replace.

6. `SubmissionUploadedHandler.HandleAsync()`: `FirstOrDefaultAsync(s => s.SubmissionId == @event.SubmissionId)`; if found, leave as-is (a submission's `StudentId` never changes, so nothing to update — just log at debug level that this is a redelivery); if not found, `db.SubmissionStudents.Add(new SubmissionStudent { SubmissionId = @event.SubmissionId, StudentId = @event.StudentId })`. `SaveChangesAsync()`.

7. `GradePublishedHandler.HandleAsync()`: `AnyAsync(g => g.SubmissionId == @event.SubmissionId && g.LecturerId == @event.PublishedByUserId)`; if a row for this exact (SubmissionId, LecturerId) pair already exists, no-op (redelivery of the same publish event); if not, `db.SubmissionGraders.Add(new SubmissionGrader { SubmissionId = @event.SubmissionId, LecturerId = @event.PublishedByUserId })` — **insert-only, never update/overwrite an existing row**, so a re-grade by a different lecturer adds a second (SubmissionId, LecturerId) row rather than replacing the first lecturer's row. `SaveChangesAsync()`. **Do not attempt to look up `SubmissionStudent` in this handler at all** — that join happens in Phase 4 at query time, not here. This handler only ever touches `SubmissionGrader`.

7a. All three handlers (Steps 5–7): wrap the `SaveChangesAsync()` call in a try/catch for `DbUpdateException`. Two near-simultaneous deliveries of the same event can both pass the `FirstOrDefaultAsync` not-found check before either commits, so the second `SaveChangesAsync()` can fail on the PK unique constraint — catch that specific case, log at debug level ("row already inserted by a concurrent delivery"), and return normally rather than letting the exception propagate as a job failure. Don't swallow any other `DbUpdateException` cause.

8. Create xUnit tests (new `AutoGrading.Identity.Api.Tests` project, EF Core InMemory, matching the pattern already established in `AutoGrading.Grading.Api.Tests`/`AutoGrading.Catalog.Api.Tests`) covering:
   - `ClassLecturerAssignedHandler`: same event delivered twice → exactly one `ClassLecturerCache` row, with the latest values.
   - `SubmissionUploadedHandler`: same event delivered twice → exactly one `SubmissionStudent` row.
   - `GradePublishedHandler`: same event delivered twice → exactly one `SubmissionGrader` row for that (SubmissionId, LecturerId) pair.
   - `GradePublishedHandler` full-history: publish for `SubmissionId=X` by `LecturerA`, then publish again for the same `SubmissionId=X` by a different `LecturerB` (re-grade) → assert **both** rows exist afterward (`(X, LecturerA)` and `(X, LecturerB)`), and both lecturers are returned by the authorization join, not just the most recent one.
   - **Event-ordering test (the case the original design broke on):** deliver `GradePublished` for a `SubmissionId` *before* delivering `SubmissionUploaded` for that same `SubmissionId`; assert both handlers complete without throwing, and that after `SubmissionUploaded` is later delivered, a query joining `SubmissionGrader`+`SubmissionStudent` on that `SubmissionId` returns the grader — i.e. order doesn't matter, verified directly, not just asserted in a comment.
   - The reverse order (`SubmissionUploaded` then `GradePublished`) also joins correctly.
   - Concurrent redelivery: simulate two `SaveChangesAsync()` calls racing for the same PK (e.g. insert the row directly, then call the handler again so it also tries to insert) and assert the handler doesn't throw.

9. Register the three handlers in Identity's `Program.cs` (`builder.Services.AddScoped<ClassLecturerAssignedHandler>()` etc., then `eventBus.Subscribe<ClassLecturerAssigned, ClassLecturerAssignedHandler>()` / `Subscribe<SubmissionUploaded, SubmissionUploadedHandler>()` / `Subscribe<GradePublished, GradePublishedHandler>()` after `app.Build()`), following the exact pattern already used for `ArtifactsExtractedHandler`/`RubricConfirmedHandler` in Grading's `Program.cs`.

10. Run the migration in dev and verify all three tables are created.

## Success Criteria

- `ClassLecturerCache`, `SubmissionStudent`, `SubmissionGrader` entities exist, each with the PK described in Step 1
- `IdentityDbContext` includes all three DbSets, with an index on `SubmissionStudent.StudentId`
- EF Core migration `AddIdentityAuthorizationCaches` creates all three tables
- All three event handlers exist, implement `IIntegrationEventHandler<T>`, and are registered/subscribed in `Program.cs` after `app.Build()`
- Each handler's redelivery test passes (same event twice → exactly one row, no duplicates, no thrown exception)
- **The event-ordering test passes in both directions** (`GradePublished` before `SubmissionUploaded`, and the reverse) — this is the finding that blocked the previous version of this plan and must be demonstrated working, not just asserted as a design intent
- No HTTP calls to Catalog/Submission/Grading appear anywhere in these three handlers — grep confirms no `HttpClient`/`IHttpClientFactory` usage was added to the Identity project for this phase
- Identity service compiles and starts without errors

## Risks

- **Cache memory growth** — Over years, `SubmissionStudent` accumulates one row per submission, and `SubmissionGrader` one row per (submission, grader) pair, indefinitely. *Mitigation:* acceptable at school scale for this iteration; note as a future cleanup candidate (e.g. archive rows older than N years) if it ever matters.
- **Full grading-history means edit access never expires** — per explicit product decision, a lecturer who graded a student once keeps roster-edit authority over that student forever, even after another lecturer takes over grading. *Mitigation:* this is the intended behavior (confirmed during plan validation), not a bug; if it ever needs to be revoked (e.g. a lecturer leaves the school), that's an admin-only `PATCH /users/{userId}` override, not something this feature needs to solve.
- **Concurrent redelivery of the same event on two worker threads** — two near-simultaneous deliveries of the same event could both pass the not-found check before either commits, producing two rows for the same key (PK for `ClassLecturerCache`/`SubmissionStudent`, composite PK for `SubmissionGrader`). *Mitigation:* the key itself is the natural uniqueness guard — a second concurrent insert with the same key fails at the database with a PK-violation `DbUpdateException`; catch that specific case in each handler and treat it as "already inserted by a concurrent delivery, nothing to do" (log and return) rather than letting it bubble up as an unhandled job failure.
