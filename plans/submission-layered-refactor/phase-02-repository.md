# Phase 2: Repository layer

**Maps to:** P1 story 2 (data access đứng sau interface).

## Goal

Move ALL EF Core access out of `SubmissionsEndpoints.cs` into `Repository/SubmissionRepository.cs`, implementing `ISubmissionRepository` from Phase 1. Do this as **one complete pass across the whole endpoint file**, not endpoint-by-endpoint — per the converged research finding, a half-migrated state (some routes on `SubmissionRepository`, others still on raw `SubmissionDbContext`) is riskier than a single complete swap when there's no automated test net.

Endpoints in this phase **do** start calling the repository instead of `SubmissionDbContext` — but business rules/authorization stay inline in the endpoint for now (that's Phase 3). This phase only relocates *data access*.

## Steps

1. Move `Data/SubmissionDbContext.cs` → `Repository/SubmissionDbContext.cs`. Update namespace/usings everywhere it's referenced — **including `Jobs/ExtractionJob.cs` line 5** (`using AutoGrading.SubmissionSvc.Api.Data;` → `using AutoGrading.SubmissionSvc.Api.Repository;`) and `Program.cs`. This must happen in this phase even though `ExtractionJob` still depends on `SubmissionDbContext` directly until Phase 5 — otherwise the move breaks the build immediately (`ExtractionJob` won't compile against the old namespace).

2. Create `Repository/SubmissionRepository.cs` implementing `ISubmissionRepository`:
   - `ListAsync` → the `db.Submissions.AsNoTracking().Where(...)` chain currently built across lines 24-49 of the old endpoint, parameterized by the filters already computed by the caller (repository does not know about roles/claims — that's still the endpoint's job this phase, moves to Service in Phase 3).
   - `GetByIdAsync` → `db.Submissions.AsNoTracking().Include(s => s.Artifacts).FirstOrDefaultAsync(...)` (lines 56-58).
   - `CreateWithAttemptCheckAsync` → the entire `try { BeginTransactionAsync(Serializable) ... CommitAsync }` / `catch (DbUpdateException)` block (lines 138-158), including the `ChangeTracker.Clear()` on conflict. Throw `SubmissionAttemptLimitReachedException`/`SubmissionAttemptConflictException` (from Phase 1) instead of returning `Results.Conflict` directly — the repository has no HTTP concerns. Add an inline comment directly above the transaction block: `// CRITICAL: the Serializable transaction must stay entirely inside this method — do not split the attempt-check and the insert across Service/Endpoints, that reopens the race condition this isolation level prevents.`
   - `SaveUploadResultAsync` → sets `ReportObjectKey`/`DiagramObjectKey`/`State`/`UpdatedAt` and `SaveChangesAsync` (lines 176-180).
   - `DeleteAsync` → the rollback `db.Submissions.Remove(submission); SaveChangesAsync` (lines 184-185).
   - `ResetForRetryAsync` → the artifact cleanup + state reset from the `/retry` endpoint (lines 90-95).

3. Update `SubmissionsEndpoints.cs` to inject `ISubmissionRepository` instead of `SubmissionDbContext` in all four route handlers, calling the new repository methods. Authorization checks (`user.IsInRole(...)`), the catalog lookup (`GetLecturerAllowedStudentIdsAsync`), object-storage upload, and event publishing **stay in the endpoint for now** — only the EF Core parts move. This is intentionally a temporary shape; Phase 3 moves the rest.

## Design Constraints

- The `IsolationLevel.Serializable` transaction must be entirely inside `CreateWithAttemptCheckAsync` — no `BeginTransactionAsync`/`CommitAsync` call may remain in `SubmissionsEndpoints.cs` after this phase.
- Repository methods must not accept or return HTTP types (`IResult`, `ClaimsPrincipal`) — only domain types and plain scalars/exceptions.
- `SubmissionDbContext` must no longer be referenced from `Endpoints/` after this phase (grep for `SubmissionDbContext` in `Endpoints/SubmissionsEndpoints.cs` should return zero matches once this phase is done).

Preflight: same repo conventions as Phase 1 (root namespace `AutoGrading.SubmissionSvc.Api.*`, primary-constructor DI, `Program.cs` top-level `AddScoped<TInterface, TImpl>`). No new conventions to discover — this phase's only structural addition is the `Repository/` folder holding both `SubmissionDbContext` (moved) and the new `SubmissionRepository` implementation, mirroring how `Clients/`/`Parsing/` already hold concrete implementations after Phase 1.

## Quality and Testing State

- Quality: approved — `plans/submission-layered-refactor/quality/phase-02-repository-quality-report.json`, receipt issued
- Testing: manual only (no automated test project). `dotnet build` passed 0/0; `grep SubmissionDbContext Endpoints/` returns zero matches. Live endpoint smoke test deferred to Phase 6 full regression per user's Phase 1 decision.

## Manual Verification

1. `dotnet build` — zero errors.
2. `grep -rn "SubmissionDbContext" Endpoints/` returns nothing.
3. Manually exercise all 4 routes end-to-end (via Swagger/Postman against a local run):
   - `GET /submissions?assignmentId=...` as student, as lecturer (with and without `studentId` filter), as admin — confirm identical result sets to pre-refactor.
   - `GET /submissions/{id}` — confirm 404/403/200 behave identically for student/lecturer/admin cases.
   - `POST /submissions/upload` twice in quick succession for the same assignment+student to confirm the attempt-limit `409 Conflict` and the concurrent-conflict `409 Conflict` (simulate by issuing two uploads near-simultaneously) both still fire with the same `usedAttempts`/`maxAttempts` payload shape.
   - `POST /submissions/{id}/retry` — confirm old artifacts are cleared and state resets to `Uploaded`, Hangfire job still gets enqueued (check Hangfire dashboard).
