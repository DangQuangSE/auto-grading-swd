# Phase 5: Update Jobs

**Maps to:** Plan story "decouple background jobs from `GradingDbContext`".

## Requirements

`Jobs/AiGradingJob.cs` and `Jobs/GradePublishedOutboxDispatcher.cs` stop depending on `GradingDbContext` directly and depend on `IGradingRepository` instead, so EF Core is fully contained inside `Repository/`. `Jobs/ArtifactsExtractedHandler.cs` needs no change (already verified thin — only enqueues the Hangfire job, no DB access).

## Design Constraints

- Preserve the exact `AiGradingJob` state machine transitions (`Running` → `Completed`/`Failed`) and the exact event publishing sequence (publish `SubmissionStatusChanged("AiGrading")`, then `AiGradingCompleted`, then `SubmissionStatusChanged("Completed")` or error variant).
- Preserve the exact `GradePublishedOutboxDispatcher` poll-loop and mark-dispatched sequence.
- Do not introduce any new service class — `AiGradingJob` remains the orchestrator, just decoupled from `GradingDbContext`.

Preflight: added `UpdateRunStatusAsync`/`AddCriterionScoresAsync` to `IGradingRepository` in Phase 1. Reuse `GetUnpublishedCompletedRunsBatchAsync(batchSize)` if needed; fetch methods return runs with `.Include(Scores)` to provide full data for the `PublishAsync` orchestration. One behavior-neutral difference from the original: the original staged all new `AiCriterionScore` rows in the EF change tracker and committed them together in one `SaveChangesAsync` call; `AddCriterionScoresAsync` now saves all scores in one batch call (matching the repository's per-method style). The net persisted state is identical — if the LLM call throws mid-parsing, no partial scores are saved either way. `/simplify` pass flagged batching `GradePublishedOutboxDispatcher`'s per-message `MarkOutboxDispatchedAsync` calls and parallelizing its publish loop — both rejected: the original code already commits each message's dispatch individually inside the loop (`message.DispatchedAt = ...; await db.SaveChangesAsync(...)` per iteration), so batching/parallelizing would be a genuine behavior change (different partial-failure semantics), not just a DbContext-decoupling change, and this phase's Design Constraints require preserving the exact poll-loop/mark-dispatched sequence. Also flagged: `AiGradingJob`'s no-rubric fallback-criterion logic "belongs in a service" — rejected, it's pre-existing unchanged logic and this phase's own Design Constraints explicitly forbid introducing a new service class here.

## Steps

1. No new `IGradingRepository` methods needed — `AddRunAsync`, `UpdateRunStatusAsync`, `AddCriterionScoresAsync`, `GetPendingOutboxMessagesAsync`, and `MarkOutboxDispatchedAsync` were all already added in Phase 1/implemented in Phase 2 specifically so this phase would have them ready. Do not add a `GetForExtractionAsync` method returning `Submission` — that's Submission service's domain type, not Grading's; `AiGradingJob` gets submission data from `ISubmissionApiClient.GetSubmissionAsync(...)` (already an interface, unchanged), not from `IGradingRepository`.

2. Update `Jobs/AiGradingJob.cs`:
   - Constructor: inject `IGradingRepository` instead of `GradingDbContext` (keep all other injections: `ICatalogApiClient`, `ISubmissionApiClient`, `IOpenCodeClient`, `OpenCodeOptions`, `IEventBus`).
   - Line 34–35: replace `db.AiGradingRuns.Add(run); await db.SaveChangesAsync(ct)` with `await repository.AddRunAsync(run, ct)`.
   - Lines 88–90: replace the status update + save with `await repository.UpdateRunStatusAsync(run.Id, AiGradingRunStatus.Completed, DateTimeOffset.UtcNow, ct)`.
   - Lines 74–86: replace the loop `db.AiCriterionScores.Add(new AiCriterionScore { ... })` accumulation + single `SaveChangesAsync` with `await repository.AddCriterionScoresAsync(run.Id, scores_list, ct)` (build a list, then pass it).
   - Line 104–105: replace the failure status update with `await repository.UpdateRunStatusAsync(run.Id, AiGradingRunStatus.Failed, DateTimeOffset.UtcNow, ct)`.

3. Update `Jobs/GradePublishedOutboxDispatcher.cs`:
   - Constructor: keep `IServiceScopeFactory`, `ILogger`, add nothing else.
   - Inside `ExecuteAsync`: instead of `var db = scope.ServiceProvider.GetRequiredService<GradingDbContext>()`, get `var repository = scope.ServiceProvider.GetRequiredService<IGradingRepository>()`.
   - Line 19–20: replace `db.GradePublishedOutbox.Where(x => x.DispatchedAt == null).OrderBy(x => x.CreatedAt).Take(100).ToListAsync(ct)` with `await repository.GetPendingOutboxMessagesAsync(100, ct)`.
   - Line 24–25: replace `message.DispatchedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct)` with `await repository.MarkOutboxDispatchedAsync(message.Id, ct)`.

4. No change to `Jobs/ArtifactsExtractedHandler.cs` — confirmed in the plan that it only enqueues `AiGradingJob`, no DB access.

## Success Criteria

- `dotnet build` compiles with zero errors.
- `Jobs/` grep `-rn "GradingDbContext"` returns zero matches — jobs have no direct DbContext reference.
- All state transitions and event publishing sequences in `AiGradingJob` remain unchanged (compare before/after code flow).
- `GradePublishedOutboxDispatcher` poll-loop behavior is unchanged.

## Quality and Testing State

- Quality: approved — `plans/grading-layered-refactor/quality/phase-05-update-jobs-quality-report.json`, receipt issued
- Testing: manual only. `dotnet build` passed 0/0; `grep GradingDbContext Jobs/` returns zero matches. Live Hangfire/outbox end-to-end smoke test deferred to Phase 6.

## Manual Verification

1. `dotnet build` — zero errors.
2. Grep `-rn "GradingDbContext" Jobs/` returns nothing.
3. Upload a submission end-to-end, watch the Hangfire dashboard for `AiGradingJob` execution:
   - Confirm it transitions `Running → Completed` (or `Failed` for a deliberately malformed report).
   - Verify the same number of `AiCriterionScore` rows are persisted as before.
   - Verify event sequence in logs: `SubmissionStatusChanged("AiGrading")`, then `AiGradingCompleted`, then `SubmissionStatusChanged("Completed")`.
4. Publish a grade to populate outbox, then watch `GradePublishedOutboxDispatcher`:
   - Confirm it polls every 2 seconds, fetches pending messages, publishes them, and marks as dispatched.
   - Verify `GradePublished` events are fired correctly (check event logs or RabbitMQ consumer trace).
5. Trigger via the full end-to-end flow (submit → extract → grade → publish) to confirm all jobs still work together.
