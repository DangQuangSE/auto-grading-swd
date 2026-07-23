# Phase 5: Update Handlers

## Requirements

`Handlers/ClassLecturerAssignedHandler.cs`, `Handlers/SubmissionUploadedHandler.cs`, and `Handlers/GradePublishedHandler.cs` stop depending on `IdentityDbContext` directly and depend on `IUserRepository` instead, so EF Core is fully contained inside `Repository/`. Each handler's idempotency semantics (handling concurrent redeliveries) must be preserved exactly.

## Design Constraints

- Each handler must call narrow, idempotent repository methods that preserve the exact current behavior (e.g., upsert-by-key semantics for `ClassLecturerCache`, append-only for `SubmissionGrader`, idempotent insert for `SubmissionStudent`).
- `DbUpdateExceptionExtensions.IsPrimaryKeyViolation()` is a static helper used by handlers to detect and swallow idempotent redeliveries. Read the file; if it has no `DbContext` dependency (it doesn't — it's pure `SqlException` inspection), move namespace-only (`Handlers/` → `Repository/` or leave in place — either is fine, just update `using` statements in handlers) or leave it unchanged in `Handlers/`.
- No new behavior — each handler's current logic and idempotency comments (e.g., line 32 of `ClassLecturerAssignedHandler`: "row already inserted by a concurrent delivery") must be preserved exactly, just shifted from raw EF to repository method calls.

Preflight (post-implementation `/simplify` note): a review pass flagged the duplicated try/catch-PK-violation pattern across all 3 handlers, the two near-identical exists-then-insert method pairs in the repository, and the check/insert-vs-catch split between repository and handler as candidates for consolidation. All rejected: the try/catch duplication is pre-existing (identical in the original 3 handler files before this phase touched them, just wrapping a repository call now instead of `db.SaveChangesAsync()` directly); genericizing the two exists/insert pairs would trade ~4 lines saved for generic-type-constraint complexity across only 2 call sites; and the check-vs-catch split is inherent to the check-then-act concurrency pattern itself (repository owns data access, handler owns the business decision of "log and swallow on a redelivery race"), not an artifact of this refactor.

Read each handler:
- `ClassLecturerAssignedHandler` (lines 14–34): upsert logic (check if cache row exists, if not add; either way update `ClassName`/`LecturerId`; `SaveChangesAsync`; catch PK violation and swallow with debug log).
- `SubmissionUploadedHandler` (lines 14–34): idempotent insert (check if submission row exists, if so exit; otherwise add new row; `SaveChangesAsync`; catch PK violation and swallow).
- `GradePublishedHandler` (lines 16–43): idempotent insert (check if grader row exists with same submission/lecturer, if so exit; otherwise add new row; `SaveChangesAsync`; catch PK violation and swallow).
- All three catch `DbUpdateException ex when (ex.IsPrimaryKeyViolation())` → log debug and return (not re-throw).

The handlers' constructors all follow primary-constructor DI: `sealed class FooHandler(IdentityDbContext db, ILogger<FooHandler> logger)`. In Phase 5, change `IdentityDbContext db` to `IUserRepository repository`.

## Steps

1. Add to `IUserRepository` (Phase 1/2 file) whatever narrow methods handlers need:
   ```
   Task UpsertClassLecturerCacheAsync(Guid classId, string className, Guid lecturerId, CancellationToken ct);
   // (or split into separate InsertOrUpdateAsync methods if that's clearer)
   
   Task<bool> SubmissionStudentExistsAsync(Guid submissionId, CancellationToken ct);
   Task InsertSubmissionStudentAsync(Guid submissionId, Guid studentId, CancellationToken ct);
   
   Task<bool> SubmissionGraderExistsAsync(Guid submissionId, Guid lecturerId, CancellationToken ct);
   Task InsertSubmissionGraderAsync(Guid submissionId, Guid lecturerId, CancellationToken ct);
   ```
   (Adjust method names as needed to match the repository's naming style — the goal is narrow, atomic, idempotent operations.)

2. Update `Handlers/ClassLecturerAssignedHandler.cs`:
   - Change constructor: `sealed class ClassLecturerAssignedHandler(IUserRepository repository, ILogger<ClassLecturerAssignedHandler> logger)`.
   - `HandleAsync`: call `repository.UpsertClassLecturerCacheAsync(@event.ClassId, @event.ClassName, @event.LecturerId, ct)`. If it throws `DbUpdateException ex when (ex.IsPrimaryKeyViolation())`, log debug "row already inserted by a concurrent delivery." and return. Propagate any other exception.

3. Update `Handlers/SubmissionUploadedHandler.cs`:
   - Change constructor: `sealed class SubmissionUploadedHandler(IUserRepository repository, ILogger<SubmissionUploadedHandler> logger)`.
   - `HandleAsync`: check `if (await repository.SubmissionStudentExistsAsync(@event.SubmissionId, ct)) { logger.LogDebug(...); return; }`, then call `repository.InsertSubmissionStudentAsync(@event.SubmissionId, @event.StudentId, ct)`. Catch `DbUpdateException ex when (ex.IsPrimaryKeyViolation())` → log debug and return. Preserve the exact log message: "submission {SubmissionId} already recorded; redelivery."

4. Update `Handlers/GradePublishedHandler.cs`:
   - Change constructor: `sealed class GradePublishedHandler(IUserRepository repository, ILogger<GradePublishedHandler> logger)`.
   - `HandleAsync`: check `if (await repository.SubmissionGraderExistsAsync(@event.SubmissionId, @event.PublishedByUserId, ct)) { logger.LogDebug(...); return; }`, then call `repository.InsertSubmissionGraderAsync(@event.SubmissionId, @event.PublishedByUserId, ct)`. Catch `DbUpdateException ex when (ex.IsPrimaryKeyViolation())` → log debug and return. Preserve the exact log messages.

5. Handle `DbUpdateExceptionExtensions.cs`:
   - Read the file to confirm it has no `DbContext` dependency (it doesn't — it's pure `SqlException` number checking).
   - Either leave it in `Handlers/` unchanged, or move it to `Repository/` with a namespace update (both are fine). If you move it, update all `using` statements in handlers.

## Quality and Testing State

- Quality: approved — `plans/identity-layered-refactor/quality/phase-05-update-handlers-quality-report.json`, receipt issued
- Testing: manual only. `dotnet build` passed 0/0; `grep IdentityDbContext Handlers/` returns zero matches. Live RabbitMQ event smoke test deferred to Phase 6.

## Manual Verification

1. `dotnet build` — zero errors.
2. `grep -rn "IdentityDbContext" Handlers/` returns zero matches (confirms handlers no longer inject `IdentityDbContext` directly).
3. Trigger the 3 domain events that feed the handlers (via docker-compose RabbitMQ or direct service calls):
   - Publish `ClassLecturerAssigned` event → confirm `ClassLecturerCache` row is upserted with correct `ClassName`/`LecturerId`.
   - Publish the same `ClassLecturerAssigned` event again (simulate redelivery) → confirm no duplicate row, no error logged other than debug "already inserted".
   - Publish `SubmissionUploaded` event → confirm `SubmissionStudent` row is inserted with correct `StudentId`.
   - Publish the same `SubmissionUploaded` event again → confirm no duplicate row, no error other than debug "already recorded; redelivery."
   - Publish `GradePublished` event → confirm `SubmissionGrader` row is inserted with correct `LecturerId`.
   - Publish the same `GradePublished` event with different `PublishedByUserId` → confirm a new row is added (append-only behavior).
   - Publish the same event a second time → confirm no error other than debug log.
