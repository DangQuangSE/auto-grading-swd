# Phase 3: Service layer

**Maps to:** P1 story 1 (business logic tách khỏi endpoint).

## Goal

Move the remaining logic out of `SubmissionsEndpoints.cs` — authorization decisions, the catalog lookup, object-storage orchestration, event publishing, and the rollback-on-failure flow — into `Service/SubmissionService.cs`. After this phase, the endpoint handlers only extract data from the HTTP request/`ClaimsPrincipal` and hand it to the service.

## Steps

1. Create `Interfaces/ISubmissionService.cs`:
   ```
   Task<IReadOnlyList<Submission>> ListForRequesterAsync(SubmissionListQuery query, RequesterContext requester, CancellationToken ct);
   Task<Submission> GetForRequesterAsync(Guid id, RequesterContext requester, CancellationToken ct); // throws NotFound/Forbidden-style domain exceptions
   Task<Submission> UploadAsync(UploadSubmissionCommand command, RequesterContext requester, CancellationToken ct);
   Task RetryAsync(Guid id, RequesterContext requester, CancellationToken ct);
   ```
   `RequesterContext` (record: `Guid? UserId, bool IsStudent, bool IsLecturer, bool IsAdmin`) replaces passing `ClaimsPrincipal` into the service — keeps the service free of ASP.NET Core auth types. Endpoint builds this from `ClaimsPrincipal` before calling the service.

2. Create `Service/SubmissionService.cs` implementing `ISubmissionService`, injecting `ISubmissionRepository`, `ICatalogApiClient`, `IObjectStorage`, `IEventBus`:
   - `ListForRequesterAsync` — reimplements the role-branching from the old `GET /` handler (lines 30-48): student restricted to own `StudentId`, lecturer requires `assignmentId` and gets `allowedStudentIds` via `GetLecturerAllowedStudentIdsAsync` moved here as a private method, admin/no-filter case.
   - `GetForRequesterAsync` — reimplements lines 60-66 (404/403 checks), throwing a `SubmissionNotFoundException`/`SubmissionForbiddenException` (new small exception types) instead of returning `IResult` directly.
   - `UploadAsync` — reimplements lines 127-199: resolve `studentId` from requester/form, call `repository.CreateWithAttemptCheckAsync` (catching the two attempt exceptions from Phase 1 and re-throwing as service-level equivalents, or let them propagate — endpoint maps them to `Results.Conflict`), upload to `storage`, call `repository.SaveUploadResultAsync`, on failure call `repository.DeleteAsync` + best-effort storage cleanup (same try/catch-and-swallow semantics as today), then publish `SubmissionUploaded` and `SubmissionStatusChanged` via `eventBus`.
   - `RetryAsync` — reimplements lines 82-97: authorization check, `repository.ResetForRetryAsync`, then enqueue the Hangfire job (`IBackgroundJobClient.Enqueue<ExtractionJob>(...)`) — this call moves into the service so it lives next to the state mutation it's paired with, per the research recommendation.

3. Define `UploadSubmissionCommand` (record) in `Service/` or `Dto/` (decide in Phase 4 alongside the other DTOs) carrying `Guid AssignmentId, Guid? StudentId, Stream ReportStream, string ReportFileName, string ReportContentType, Stream? DiagramStream, string? DiagramFileName, string? DiagramContentType` — **not** `IFormFile`, so `Service/` has no ASP.NET Core dependency (the endpoint opens the `IFormFile` streams and passes them down; ownership/disposal of the streams stays the endpoint's responsibility, matching current `await using` usage).

## Design Constraints

- `Service/` must not reference `Microsoft.AspNetCore.*` types (`ClaimsPrincipal`, `IFormFile`, `IResult`) — this is the actual "endpoint isn't the boss" boundary the whole refactor exists to create. If a Service method needs to signal "not found" or "forbidden", it throws a plain exception; the endpoint layer maps exceptions to `IResult` in Phase 4.
- The Hangfire enqueue call for retry moves into `Service`, but the Hangfire enqueue call inside `Jobs/SubmissionUploadedHandler.cs` (triggered by the RabbitMQ event, not by this service) stays where it is — that one isn't part of this HTTP flow and Phase 5 handles `Jobs/` separately.
- Do not change `Endpoints/SubmissionsEndpoints.cs` route bodies yet beyond what's strictly needed to compile against the new `ISubmissionService` — the real endpoint slimming (removing duplicate logic, wiring exception→`IResult` mapping cleanly) is Phase 4.
- **Stream ownership**: `UploadAsync` only reads from `ReportStream`/`DiagramStream` — it must never dispose them, including on the exception path. The endpoint handler opens both streams in `await using` blocks (same as today) so they're disposed on the way out regardless of whether `UploadAsync` throws.
- **`RequesterContext` validation happens in the endpoint, before calling the service**: if `ClaimTypes.NameIdentifier` is missing or not a valid `Guid` for a student role, the endpoint returns `Results.Forbid()` immediately and never constructs/calls into `Service` — this matches the existing `Guid.TryParse(...) → Forbid` checks already in the current endpoint (lines 32, 61, 82, 112, 133), just relocated to run before the service call instead of inline with the query.
- **Retry idempotency is out of scope**: the current endpoint has no de-duplication for `POST /{id}/retry` (calling it twice enqueues `ExtractionJob` twice) — this refactor preserves that exact behavior rather than fixing it, since adding de-duplication would be a behavior change and the spec explicitly scopes this as behavior-preserving. Note it as a known pre-existing limitation, not something to silently fix mid-refactor.

Preflight: interpreted "the endpoint layer maps exceptions to IResult in Phase 4" as describing final ownership/polish, not a literal deferral — Phase 3's own Manual Verification demands identical 404/403/409 behavior today, which requires a working exception→`IResult` mapping to exist now (`TryBuildRequesterContext` + per-exception `catch` blocks in `SubmissionsEndpoints.cs`). Phase 4 will replace this with the DTO-based final form; the mapping logic itself doesn't change, only its packaging. New exception types added beyond the two named in this phase's Steps: `SubmissionAssignmentNotFoundException` (kept distinct from `SubmissionNotFoundException` because the upload 404 has an error body while the GET/retry 404s are empty — conflating them would have changed the API contract). Minor DRY addition not explicitly in the spec: `EnsureCanActOnAsync` factors out the identical student/lecturer authorization check shared by `GetForRequesterAsync` and `RetryAsync` — same checks, same order, same exceptions as if written inline twice.

## Quality and Testing State

- Quality: approved — `plans/submission-layered-refactor/quality/phase-03-service-quality-report.json`, receipt issued
- Testing: manual only. `dotnet build` passed 0/0; `Service/` grep-confirmed free of ASP.NET Core types. Live endpoint smoke test deferred to Phase 6 full regression per user's Phase 1 decision.

## Manual Verification

1. `dotnet build` — zero errors.
2. Repeat the same 4-route manual pass from Phase 2 (list/get/upload/retry, for student/lecturer/admin roles) — behavior must still be identical. This is the highest-risk phase (all business rules moved), so re-verify the attempt-limit and attempt-conflict paths specifically, and the rollback path (force a storage upload failure, e.g. temporarily point `IObjectStorage` at an invalid endpoint, confirm the submission row is removed and no orphaned artifact/object remains).
