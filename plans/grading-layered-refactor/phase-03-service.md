# Phase 3: Service layer

**Maps to:** Plan story "move authorization decisions and business logic into `GradingService`".

## Requirements

Move the remaining logic out of `GradesEndpoints.cs` — authorization decisions (`IsLecturerAllowedAsync`, `CanReadSubmissionAsync`, `FilterAllowedForLecturerAsync`), the batch-publishing orchestration, and the Hangfire job enqueue call — into `Service/GradingService.cs`. After this phase, the endpoint handlers only extract data from the HTTP request/`ClaimsPrincipal` and hand it to the service.

## Design Constraints

- `Service/` must not reference `Microsoft.AspNetCore.*` types (`ClaimsPrincipal`, `IResult`, `IBackgroundJobClient`) — this is the actual "endpoint isn't the boss" boundary. If a Service method needs to signal "not found" or "forbidden", it throws a plain domain exception; the endpoint layer maps exceptions to `IResult` in Phase 4.
- The Hangfire enqueue call for regrade moves into `Service`, but the call inside `Jobs/ArtifactsExtractedHandler.cs` (triggered by event, not by this HTTP flow) stays where it is — that one isn't part of this refactor.
- Do not change `Endpoints/GradesEndpoints.cs` route bodies yet beyond what's strictly needed to compile against the new `IGradingService` — the real endpoint slimming (removing duplicate logic, wiring exception → `IResult` mapping cleanly) is Phase 4.
- **`RequesterContext` validation happens in the endpoint, before calling the service**: if `ClaimTypes.NameIdentifier` is missing or not a valid `Guid` for a student role, the endpoint returns `Results.Forbid()` immediately and never constructs/calls into `Service`. This matches the existing `Guid.TryParse(...) → Forbid` checks already in some endpoints.

Preflight: interpreted "the endpoint layer maps exceptions to IResult in Phase 4" as describing final ownership/polish, not a literal deferral — Phase 3's own Manual Verification demands identical 403 behavior now, which requires a working exception → `IResult` mapping to exist now. Phase 4 will replace this with the DTO-based final form; the mapping logic itself doesn't change. Exception types actually defined: `GradingForbiddenException`, `InvalidGradingRunException`. **`GradeNotFoundException` was deliberately NOT defined** — none of the 7 routes ever need to signal "not found" via exception; every not-found case is already representable as a nullable return value (`FinalGrade?`, or the `PublishedResultData` tuple's `Grade`/`GradingDone` fields), matching the original endpoint's behavior exactly. Defining an exception type with zero call sites would be dead code. `GetPublishedResultForRequesterAsync`'s return type uses `bool? GradingDone` (null = the defensive "publication exists but its FinalGrade is missing" edge case → bare 404; true/false = real "no publication yet" case → 404 with a body) to preserve the original endpoint's two distinct 404 shapes through one data object — a `/simplify` pass flagged this as slightly awkward three-state encoding but confirmed it's functionally correct and adequately documented; not reworked given the alternative (a dedicated exception type) wouldn't be more consistent with this codebase's established GET-route pattern (nullable returns, not exceptions, for "not found"). `PublishAllAsync` has an explicit `if (!requester.IsAdmin) throw new GradingForbiddenException();` even though the route's `.RequireRole("admin")` policy already guarantees this — added during `/simplify` for consistency with every other service method (all of which check authorization explicitly), even though it's a no-op in practice. `Service` takes `IGradingRepository` + `ISubmissionApiClient` + `ICatalogApiClient` + `IBackgroundJobClient`, all interfaces or plain services (no ASP.NET Core types). `PublishAllAsync`'s batch loop calls `repository.PublishAsync` directly (not `this.PublishGradeAsync`), matching the original endpoint's `PublishOneAsync` direct-call pattern exactly — the batch query already guarantees each run is completed and unpublished, so routing through `PublishGradeAsync`'s redundant idempotency/completeness checks would add DB round-trips without changing behavior.

## Steps

1. Create `Interfaces/IGradingService.cs`:
   ```
   Task<IReadOnlyList<AiGradingRun>> GetRunsForRequesterAsync(Guid submissionId, RequesterContext requester, CancellationToken ct);
   Task<(FinalGrade? Grade, DateTimeOffset? PublishedAt, AiGradingRun? Run, bool GradingDone)> GetPublishedResultForRequesterAsync(Guid submissionId, RequesterContext requester, CancellationToken ct);
   Task<FinalGrade?> GetFinalGradeForRequesterAsync(Guid submissionId, RequesterContext requester, CancellationToken ct);
   Task<IReadOnlyList<FinalGradeData>> GetFinalGradesBatchForRequesterAsync(IReadOnlyCollection<Guid> submissionIds, RequesterContext requester, CancellationToken ct);
   Task RegradeAsync(Guid submissionId, string? assignmentDescriptionOverride, RequesterContext requester, CancellationToken ct);
   Task<FinalGrade> PublishGradeAsync(Guid submissionId, Guid? gradingRunId, decimal finalScore, string? notes, RequesterContext requester, CancellationToken ct);
   Task<PublishAllResult> PublishAllAsync(RequesterContext requester, CancellationToken ct);
   ```
   where `RequesterContext` is a record: `Guid? UserId, bool IsStudent, bool IsLecturer, bool IsAdmin`.
   and `PublishAllResult` is a record: `int Published, int Skipped, int Failed`.
   and `FinalGradeData` carries the same fields as `FinalGradeResponse` (will be defined in DTOs, Phase 4).

2. Create `Service/GradingService.cs` implementing `IGradingService`, injecting `IGradingRepository`, `ISubmissionApiClient`, `ICatalogApiClient`, `IBackgroundJobClient`:
   - `GetRunsForRequesterAsync` — authorization check (only lecturer/admin allowed for this endpoint per line 21), then call `repository.GetRunsForSubmissionAsync(submissionId, ct)`. If lecturer, check `IsLecturerAllowedAsync(...)` first; if false, throw `GradingForbiddenException`.
   - `GetPublishedResultForRequesterAsync` — authorization check via `CanReadSubmissionAsync(...)` (student sees own, lecturer sees enrolled students, admin sees all). If no publication, query `repository.GetUnpublishedCompletedRunsBatchAsync(...)` to determine `GradingDone` flag (is there a completed run for this submission?). Return tuple with the grade, published-at timestamp, optional run (if grade has `GradingRunId`), and `GradingDone` boolean.
   - `GetFinalGradeForRequesterAsync` — authorization check via `CanReadSubmissionAsync(...)`. Call `repository.GetLatestFinalGradeAsync(submissionId, ct)`.
   - `GetFinalGradesBatchForRequesterAsync` — if lecturer, filter submission ids via `FilterAllowedForLecturerAsync(...)`. Call `repository.GetLatestFinalGradesBatchAsync(filtered_ids, ct)`.
   - `RegradeAsync` — authorization check (only lecturer/admin, with lecturer requiring `IsLecturerAllowedAsync`). Enqueue `IBackgroundJobClient.Enqueue<AiGradingJob>(job => job.ExecuteAsync(submissionId, assignmentDescriptionOverride, CancellationToken.None))`.
   - `PublishGradeAsync` — authorization check (only lecturer/admin, with lecturer requiring `IsLecturerAllowedAsync`). If `gradingRunId` is provided, check `repository.IsRunCompletedAsync(runId, submissionId, ct)` — throw `InvalidGradingRunException` if not completed. Check `repository.GetLatestPublicationAsync(submissionId, ct)` — if exists, return the existing `FinalGrade` unchanged rather than publishing again (**idempotency, matching current lines 209–210**: calling this twice for the same submission must not create a duplicate publication — this is load-bearing for `PublishAllAsync`'s retry-safe batch loop below). Otherwise call `repository.PublishAsync(submissionId, gradingRunId, finalScore, notes, requester.UserId!.Value, ct)`.
   - `PublishAllAsync` — authorization check (admin only). Loop: `while (true) { runs = repository.GetUnpublishedCompletedRunsBatchAsync(100, ct); if (runs.Count == 0) break; foreach (run in runs) { try { await PublishGradeAsync(run.SubmissionId, run.Id, run.Scores.Sum(s => s.SuggestedScore), null, requester, ct); published++; } catch (Exception) when (!ct.IsCancellationRequested) { failed++; } ; if (runs.Count < 100 || failed > 0) break; } }`. Do **not** call `db.ChangeTracker.Clear()` here — that's now `PublishAsync`'s own responsibility on failure (Phase 2), so `Service` never needs EF-Core-specific cleanup knowledge. Return tuple: `Published` = newly published this call, `Skipped` = publications that already existed before this call started (`repository.CountPublicationsAsync(ct)` captured at the top, matching current line 225's semantics exactly), `Failed` = runs that threw during publish.
   - Private helpers moved from endpoint: `IsLecturerAllowedAsync(submissionId, lecturerId, ISubmissionApiClient, ICatalogApiClient, ct)` — unchanged logic from line 116–126. `CanReadSubmissionAsync(submissionId, requester, ISubmissionApiClient, ICatalogApiClient, ct)` — unchanged logic from lines 104–112. `FilterAllowedForLecturerAsync(submissionIds, lecturerId, ISubmissionApiClient, ICatalogApiClient, ct)` — unchanged logic from lines 152–181, returns filtered `HashSet<Guid>`.

3. Define exception types (in `Domain/Exceptions/` or `Service/` namespace):
   - `GradingForbiddenException(string message)` — thrown when authorization fails.
   - `GradeNotFoundException(string message)` — thrown when a grade/run not found.
   - `InvalidGradingRunException(string message)` — thrown when run state is invalid for the operation.

## Success Criteria

- `dotnet build` on `AutoGrading.Grading.Api` compiles with zero errors.
- `Service/GradingService.cs` exists and implements `IGradingService`.
- Grep `-rn "GradingDbContext" Service/` returns zero matches.
- Grep `-rn "ClaimsPrincipal\|IResult\|IBackgroundJobClient" Service/` returns zero matches (no ASP.NET Core types in Service layer).
- All 7 routes continue to work identically (manual pass).

## Quality and Testing State

- Quality: approved — `plans/grading-layered-refactor/quality/phase-03-service-quality-report.json`, receipt issued
- Testing: manual only (no automated test project). `dotnet build` passed 0/0; `Service/GradingService.cs` grep-confirmed free of ASP.NET Core types and `GradingDbContext`. Live endpoint smoke test deferred to Phase 6.

## Manual Verification

1. `dotnet build` — zero errors.
2. Repeat the same 7-route manual pass from Phase 2 (all 7 endpoints, all role cases) — behavior must still be identical. This is a high-risk phase (all business rules moved), so re-verify:
   - Authorization: lecturer accessing a submission outside their enrolled students should get `403`.
   - Student accessing their own vs another's submission (should work vs `403`).
   - Admin accessing any submission (should work).
   - Regrade: enqueue still fires (check Hangfire dashboard).
   - Publish: idempotency confirmed (publish same run twice → second returns same published grade).
   - Publish-all: batch logic works with correct counts.
