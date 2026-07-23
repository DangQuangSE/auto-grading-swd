# Phase 2: Repository layer

**Maps to:** Plan story "move EF Core access behind `IGradingRepository`".

## Requirements

Move ALL EF Core access out of `GradesEndpoints.cs` into `Repository/GradingRepository.cs`, implementing `IGradingRepository` from Phase 1. Do this as **one complete pass across all 7 route handlers**, not route-by-route — per the precedent from Submission's research, a half-migrated state (some routes on `GradingRepository`, others still on raw `GradingDbContext`) is riskier than a single complete swap when there's no automated test net.

Endpoints in this phase **do** start calling the repository instead of `GradingDbContext` — but authorization decisions and business rules stay inline in the endpoint for now (that's Phase 3). This phase only relocates *data access*.

## Design Constraints

- The `PublishAsync` transaction (plain `BeginTransactionAsync(ct)`, default `ReadCommitted` isolation — matching the current code exactly, **not** `IsolationLevel.Serializable`) must be entirely inside the repository method — no `BeginTransactionAsync`/`CommitAsync` call may remain in `GradesEndpoints.cs` after this phase.
- **`PublishAsync` must leave the `DbContext` in a clean state on failure.** The current endpoint code relies on `PublishAllAsync`'s loop calling `db.ChangeTracker.Clear()` after each failed `PublishOneAsync` — because once `Service`/`Endpoints` no longer touch `DbContext` directly, that cleanup must move inside `PublishAsync` itself: wrap the body in `try { ... } catch { db.ChangeTracker.Clear(); throw; }` so a failed publish never leaves stale tracked entities for the *next* `PublishAsync` call on the same scoped `DbContext` (this matters specifically for `PublishAllAsync`'s batch loop in Phase 3, which calls `PublishAsync` repeatedly against the same request-scoped repository instance).
- Repository methods must not accept or return HTTP types (`IResult`, `ClaimsPrincipal`) — only domain types and plain scalars/exceptions.
- `GradingDbContext` must no longer be referenced from `Endpoints/` after this phase (grep for `GradingDbContext` in `Endpoints/GradesEndpoints.cs` should return zero matches once this phase is done).

Preflight: same repo conventions as Phase 1 (root namespace `AutoGrading.Grading.Api.*`, primary-constructor DI, `Program.cs` top-level `AddScoped<TInterface, TImpl>`). No new conventions to discover — this phase's structural addition is the `Repository/` folder holding both `GradingDbContext` (moved) and the new `GradingRepository` implementation. `FindPublishedGradeAsync` (old helper) and `GetLatestFinalGradeAsync`'s inline query turned out to be the exact same query shape — consolidated into one repository method, no separate method needed. `/simplify` pass added one repository method beyond Phase 1's original interface list, `HasCompletedRunAsync(submissionId)`: `GetPublishedResultAsync`'s "is grading done" check was calling `GetRunsForSubmissionAsync` (which `.Include(Scores)`) just to test `.Any(Status == Completed)`, discarding the scores — 4/4 review agents flagged this as real over-fetching, worth a narrow query rather than deferring to Phase 3.

## Steps

1. Move `Data/GradingDbContext.cs` → `Repository/GradingDbContext.cs`. Update namespace/usings everywhere it's referenced:
   - `Endpoints/GradesEndpoints.cs` (line 5) — will be removed entirely in this phase when it stops using it.
   - `Jobs/AiGradingJob.cs` (line 5) — update namespace now; the job still depends on `GradingDbContext` directly until Phase 5.
   - `Jobs/GradePublishedOutboxDispatcher.cs` (line 3) — update namespace now; the dispatcher still depends on `GradingDbContext` directly until Phase 5.
   - `Handlers/RubricConfirmedHandler.cs` (line 4) — update namespace now; this handler stays on `GradingDbContext` (out of scope per plan).
   - `Program.cs` (line 8) — update namespace where `AddDbContext` is called.

2. Create `Repository/GradingRepository.cs` implementing `IGradingRepository`, extracting EF Core query chains currently built across the endpoint:
   - `GetRunsForSubmissionAsync` → `db.AiGradingRuns.AsNoTracking().Include(r => r.Scores).Where(r => r.SubmissionId == submissionId).ToListAsync(...)` (lines 52–53).
   - `GetLatestPublicationAsync` → `db.GradePublications.AsNoTracking().Where(...).OrderByDescending(p => p.PublishedAt).FirstOrDefaultAsync(...)` (lines 63–66).
   - `GetFinalGradeByIdAsync` → `db.FinalGrades.AsNoTracking().FirstOrDefaultAsync(f => f.Id == finalGradeId, ...)` (line 76–77).
   - `GetRunByIdAsync` → `db.AiGradingRuns.AsNoTracking().Include(r => r.Scores).FirstOrDefaultAsync(r => r.Id == runId && r.SubmissionId == submissionId, ...)` (lines 82–83).
   - `GetLatestFinalGradeAsync` → `db.FinalGrades.AsNoTracking().Join(db.GradePublications.AsNoTracking(), f => f.Id, p => p.FinalGradeId, (f, p) => new { Grade = f, p.PublishedAt }).Where(x => x.Grade.SubmissionId == submissionId).OrderByDescending(x => x.PublishedAt).Select(x => x.Grade).FirstOrDefaultAsync(...)` (lines 95–100).
   - `GetLatestFinalGradesBatchAsync` → `db.FinalGrades.AsNoTracking().Join(db.GradePublications.AsNoTracking(), f => f.Id, p => p.FinalGradeId, (f, p) => new { Grade = f, p.PublishedAt }).Where(x => ids.Contains(x.Grade.SubmissionId)).OrderByDescending(x => x.PublishedAt).ToListAsync(...)` then grouped (lines 141–144), returning one `FinalGrade` per `SubmissionId`.
   - `IsRunCompletedAsync` → `db.AiGradingRuns.AnyAsync(r => r.Id == runId && r.SubmissionId == submissionId && r.Status == AiGradingRunStatus.Completed, ...)` (lines 212–213).
   - `CountPublicationsAsync` → `db.GradePublications.AsNoTracking().CountAsync(...)` (line 225).
   - `GetUnpublishedCompletedRunsBatchAsync` → the loop's per-batch query (lines 232–237) — returns up to `batchSize` run Ids from completed, unpublished runs, grouped by submission to get the latest per submission.
   - `PublishAsync` → the entire `await using var transaction = await db.Database.BeginTransactionAsync(ct)` block (lines 271–278, no explicit isolation level — matches current code, do not add `IsolationLevel.Serializable`), creating `FinalGrade` + `GradePublication` + `GradePublishedOutbox` atomically, wrapped in `try { ...; await transaction.CommitAsync(ct); return grade; } catch { db.ChangeTracker.Clear(); throw; }` so a failure never leaves stale tracked entities for a subsequent call. Throw no domain exceptions (on success returns `FinalGrade`). Add an inline comment directly above the transaction block: `// CRITICAL: this 3-table write must stay entirely inside this method — do not split it across Service/Endpoints. On failure, ChangeTracker.Clear() keeps this DbContext instance safe for PublishAllAsync's next call.`
   - `AddRunAsync` → `db.AiGradingRuns.Add(run); await db.SaveChangesAsync(...)` (lines 34–35 in Job).
   - `UpdateRunStatusAsync` → set `run.Status` and `run.CompletedAt`, then `db.SaveChangesAsync(...)` (lines 88–90 in Job).
   - `AddCriterionScoresAsync` → `db.AiCriterionScores.AddRange(scores); await db.SaveChangesAsync(...)` (lines 74–86 in Job, looped). Note: accept the whole collection; can optimize to batch-insert all at once instead of looping in the job.
   - `GetPendingOutboxMessagesAsync` → `db.GradePublishedOutbox.Where(x => x.DispatchedAt == null).OrderBy(x => x.CreatedAt).Take(batchSize).ToListAsync(...)` (lines 19–20 in Dispatcher).
   - `MarkOutboxDispatchedAsync` → fetch message by id, set `DispatchedAt = DateTimeOffset.UtcNow`, `db.SaveChangesAsync(...)` (lines 24–25 in Dispatcher).

3. Update `Endpoints/GradesEndpoints.cs` to inject `IGradingRepository` instead of `GradingDbContext` in all 7 route handlers, calling the new repository methods. Authorization checks (`user.IsInRole(...)`, calls to `IsLecturerAllowedAsync`, `CanReadSubmissionAsync`), the `Hangfire.IBackgroundJobClient.Enqueue(...)` call, and any other non-data-access logic **stay in the endpoint for now** — only the EF Core parts move. This is intentionally a temporary state; Phase 3 moves the rest.

## Success Criteria

- `dotnet build` on `AutoGrading.Grading.Api` compiles with zero errors.
- `Repository/GradingDbContext.cs` and `Repository/GradingRepository.cs` both exist and are imported correctly.
- `grep -rn "GradingDbContext" Endpoints/` returns zero matches — endpoint has no direct DbContext reference.
- All 7 routes continue to work identically (manual pass through Swagger/Postman).

## Quality and Testing State

- Quality: approved — `plans/grading-layered-refactor/quality/phase-02-repository-quality-report.json`, receipt issued (first pass incorrectly flagged Phase 1's already-approved `Interfaces/`/`Constant/` as this phase's scope creep, since nothing is committed between phases yet; corrected on re-verify by cross-referencing Phase 1's own receipt)
- Testing: manual only (no automated test project). `dotnet build` passed 0/0; `grep GradingDbContext Endpoints/` and the `RubricConfirmedHandler` scope grep both zero matches. Live endpoint smoke test deferred to Phase 6 per the same manual-verification approach used throughout Submission.

## Manual Verification

1. `dotnet build` — zero errors.
2. `grep -rn "GradingDbContext" Endpoints/` returns nothing.
3. Confirm `Handlers/RubricConfirmedHandler.cs` is genuinely out of scope: `grep -n "db.AiGradingRuns\|db.FinalGrades\|db.GradePublications\|db.GradePublishedOutbox" Handlers/RubricConfirmedHandler.cs` returns zero matches (it only touches `db.LocalRubrics`/`db.LocalRubricCriteria`) — confirms it's a different bounded context, not something this refactor's repository needs to cover, and it's fine for it to keep depending on `GradingDbContext` directly.
4. Manually exercise all 7 routes end-to-end (via Swagger or Postman against a local run):
   - `GET /grades/{submissionId}/runs` — confirm returns same run list for lecturer vs student vs admin cases.
   - `GET /grades/{submissionId}/result` — confirm 404/403/200 behave identically for all role cases; verify `gradingDone` flag is still returned when no publication exists but runs are completed.
   - `GET /grades/{submissionId}/final` — confirm 404/200 identically, joined correctly.
   - `GET /grades/final?submissionIds=...` — confirm batch returns same results with no duplicates per submission.
   - `POST /grades/{submissionId}/publish` — confirm `409 Conflict` still fires if grading run is incomplete; confirm `200 OK` (idempotent) if already published; confirm `201 Created` on first publish.
   - `POST /grades/publish-all` — confirm batch publishing works with same published/skipped/failed counts.
   - `POST /grades/{submissionId}/regrade` — confirm enqueues Hangfire job (check Hangfire dashboard), returns `202 Accepted`.
