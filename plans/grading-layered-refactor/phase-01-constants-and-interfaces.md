# Phase 1: Constants + Interfaces (additive only)

**Maps to:** Plan story "repository interface", "move client interface declarations" — groundwork only, no code path switches yet.

## Requirements

Create the new folders and interface contracts with zero behavior change. Nothing that currently works stops working — this phase only adds files and moves two interface *declarations* (not their implementations).

## Design Constraints

- Zero behavior change — this phase must compile and the app must run identically to before, since nothing yet calls the new repository interface.
- `IGradingRepository` methods are named after what the caller needs (`PublishAsync`, `GetRunsForSubmissionAsync`), not generic CRUD — the atomic 3-table write (`FinalGrade`+`GradePublication`+`GradePublishedOutbox`) from `GradesEndpoints.cs:267–278` must be fully owned by the `PublishAsync` method, never split across separate methods. **Note:** the current code's transaction is `BeginTransactionAsync(ct)` with no explicit isolation level (defaults to `ReadCommitted`) — this refactor preserves that exact isolation level; do not add `IsolationLevel.Serializable` (that would be a behavior change, not a structural refactor, and this endpoint has no analogous attempt-limit race Submission's `CreateWithAttemptCheckAsync` was guarding against).
- Do not touch `GradesEndpoints.cs`, `Jobs/*`, `Program.cs`, or `Handlers/*` in this phase — this is groundwork only.

Preflight: Root namespace is `AutoGrading.Grading.Api.*`. Existing convention colocates interface + concrete implementation in one file (`ICatalogApiClient`+`CatalogApiClient`, `ISubmissionApiClient`+`SubmissionApiClient`) — splitting them across `Interfaces/` ↔ `Clients/` per this plan is a deliberate, plan-driven deviation, not an oversight. DI registration style: top-level `Program.cs`, `builder.Services.AddScoped<TInterface, TImpl>()`/`AddHttpClient<TInterface, TImpl>()`. Primary-constructor DI (`class Foo(Dep dep) : IGradingRepository`) is the established style — new classes should follow it. Only one hardcoded error string exists in the current endpoint (`GradesEndpoints.cs:214`, "The grading run is not a completed run for this submission.") — captured in `GradingConstants.GradingRunNotCompleted` now but deliberately not wired into the endpoint yet, matching Submission's Phase 1 precedent (constants populate but aren't consumed until Phase 4's endpoint rewrite). `/simplify` pass after implementation found and fixed two genuinely dead `using` lines in `GradesEndpoints.cs` (`AutoGrading.Common.Messaging`, `AutoGrading.Contracts.Events` — unused, pre-existing, in the same using-block already being edited); all other findings (constant not yet consumed, `IGradingRepository` not yet wired into DI, `CatalogApiClient`'s pre-existing HTTP-pattern inconsistency vs `SubmissionApiClient`) were correctly identified as by-design deferrals or out-of-scope pre-existing code, not fixed.

## Steps

1. Create `Constant/GradingConstants.cs` (if referenced at all during this or later phases — check current endpoint for hardcoded strings like error messages). For now, this file is scaffolded empty or with placeholder structure; will be populated in Phase 4 when the endpoint is rewritten. The phase is still valid (it's structural groundwork) even if no constants are needed immediately.

2. Create `Interfaces/IGradingRepository.cs` — new contract, methods derived from what the endpoint currently does inline against `GradingDbContext`:
   ```
   Task<IReadOnlyList<AiGradingRun>> GetRunsForSubmissionAsync(Guid submissionId, CancellationToken ct);
   Task<GradePublication?> GetLatestPublicationAsync(Guid submissionId, CancellationToken ct);
   Task<FinalGrade?> GetFinalGradeByIdAsync(Guid finalGradeId, CancellationToken ct);
   Task<AiGradingRun?> GetRunByIdAsync(Guid runId, Guid submissionId, CancellationToken ct);
   Task<FinalGrade?> GetLatestFinalGradeAsync(Guid submissionId, CancellationToken ct);
   Task<IReadOnlyList<FinalGrade>> GetLatestFinalGradesBatchAsync(IReadOnlyCollection<Guid> submissionIds, CancellationToken ct);
   Task<bool> IsRunCompletedAsync(Guid runId, Guid submissionId, CancellationToken ct);
   Task<FinalGrade> PublishAsync(Guid submissionId, Guid? runId, decimal score, string? notes, Guid userId, CancellationToken ct);
   Task<int> CountPublicationsAsync(CancellationToken ct);
   Task<IReadOnlyList<AiGradingRun>> GetUnpublishedCompletedRunsBatchAsync(int batchSize, CancellationToken ct);
   Task AddRunAsync(AiGradingRun run, CancellationToken ct);
   Task UpdateRunStatusAsync(Guid runId, AiGradingRunStatus status, DateTimeOffset completedAt, CancellationToken ct);
   Task AddCriterionScoresAsync(Guid runId, IReadOnlyList<AiCriterionScore> scores, CancellationToken ct);
   Task<IReadOnlyList<GradePublishedOutbox>> GetPendingOutboxMessagesAsync(int batchSize, CancellationToken ct);
   Task MarkOutboxDispatchedAsync(Guid outboxId, CancellationToken ct);
   ```

3. Move `ICatalogApiClient` interface declaration from `Clients/CatalogApiClient.cs` to `Interfaces/ICatalogApiClient.cs`. Update the `namespace` and the `using` in `Clients/CatalogApiClient.cs` (and keep it importing from `Interfaces/`). No method signature changes.

4. Move `ISubmissionApiClient` interface declaration from `Clients/SubmissionApiClient.cs` to `Interfaces/ISubmissionApiClient.cs`. Update the `namespace` and the `using` in `Clients/SubmissionApiClient.cs` (and keep it importing from `Interfaces/`). No method signature changes.

## Success Criteria

- `dotnet build` on `AutoGrading.Grading.Api` compiles with zero errors/warnings introduced.
- `Interfaces/IGradingRepository.cs`, `Interfaces/ICatalogApiClient.cs`, `Interfaces/ISubmissionApiClient.cs` all exist.
- `Clients/CatalogApiClient.cs` and `Clients/SubmissionApiClient.cs` still import and implement their interfaces (now from `Interfaces/`), unchanged in behavior.
- Application runs and all 7 routes behave exactly as before — this is a smoke check that the move didn't break a `using`/namespace reference.

## Quality and Testing State

- Quality: approved — `plans/grading-layered-refactor/quality/phase-01-constants-and-interfaces-quality-report.json`, receipt issued
- Testing: manual only (no automated test project for Grading, confirmed empty). `dotnet build` passed 0/0 both before and after `/simplify` fixes.

## Manual Verification

1. `dotnet build` on `AutoGrading.Grading.Api` — must compile with zero errors/warnings introduced.
2. Grep `-rn "AutoGrading.Grading.Api.Interfaces"` across the project — should find only the 3 interface files and their usages in `Clients/` files (nothing in `Endpoints/` or `Jobs/` yet).
3. Run the service locally (`dotnet run` or via docker-compose), confirm:
   - `GET /grades/{submissionId}/runs` — returns array of runs with scores.
   - `GET /grades/{submissionId}/result` — returns published grade result or 404/403 appropriately.
   - `GET /grades/{submissionId}/final` — returns final grade or 404/403 appropriately.
   - `GET /grades/final?submissionIds=...` — returns batch of final grades.
   - `POST /grades/{submissionId}/publish` — publishes a grade.
   - `POST /grades/publish-all` — publishes all unpublished grades.
   - `POST /grades/{submissionId}/regrade` — enqueues a regrade job.
   All behavior identical to before — nothing in this phase should have changed runtime behavior.
