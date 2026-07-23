# Phase 4: Slim endpoints + Dto layer

**Maps to:** Plan stories "endpoints become pure HTTP adapters" and "move DTOs to `Dto/` folder".

## Requirements

`GradesEndpoints.cs` becomes pure HTTP adapter: bind request → build `RequesterContext` → call `IGradingService` → map result/exception → `IResult`. Introduce `Dto/` for request/response shapes, without changing the wire format of any response.

## Design Constraints

- Response JSON shape must not change — this is a behavior-preserving refactor. If a DTO response needs a property the entity doesn't expose 1:1, stop and flag it rather than silently changing the contract.
- `Endpoints/GradesEndpoints.cs` should contain no `catch` blocks around business logic other than the exception → `IResult` mapping — no loops, no EF Core, no direct repository/service access beyond calling `IGradingService`.
- **AI-visibility rule — already resolved, do not change behavior**: line 19's comment ("AI output is review material. It is never exposed directly to students") describes the **`/runs`** route only (`GetRunsAsync`, restricted to lecturer/admin — all raw runs for a submission, pre-publish). The **`/result`** route (`GetPublishedResultAsync`) is a *different, deliberately-designed* route — its own comment (line 23, "Student-safe projection: only the run selected by a publication is returned") documents that once a lecturer publishes a grade by selecting a specific `AiGradingRun`, that one run's full detail (`Scores` incl. `Evidence`/`Comment`) becomes the student's official feedback and is meant to be visible to them — this is the reveal-after-publish workflow, not a leak. `PublishedGradeResult`'s DTO must preserve this exactly: same fields, same visibility, for every role. Do **not** restrict `AiGradingRun`/`Scores` out of the DTO for students — that would be an undocumented behavior change to a security-relevant response, which this behavior-preserving refactor must not introduce.

Preflight: exception → `IResult` mapping and `TryBuildRequesterContext` will already be correct from Phase 3 (any exceptions were already being thrown and mapped per Manual Verification requirements). This phase's endpoint changes are purely additive: introduce DTOs in `Dto/` and wire them into the return statements. `RegradeRequest` (input), `PublishGradeRequest` (input), `PublishAllResponse` moved unchanged. **Correction to this phase's own text**: the original endpoint's `FinalGradeResponse` (4 fields: SubmissionId, FinalGradeId, FinalScore, CreatedAt) is only used by the *batch* route (`GetFinalGradesBatchAsync`) — it was already a deliberate narrower projection in the pre-refactor code, not a full mirror. `GetFinalGradeAsync`, `PublishGradeAsync`, and `PublishedGradeResult` all return the **full** `FinalGrade` entity today (7 fields, including `GradingRunId`/`Notes`/`CreatedByUserId`). Reusing the narrow `FinalGradeResponse` for those three would have silently dropped 3 fields from the wire format — introduced a new `Dto/FinalGradeDetailResponse.cs` (full 1:1 mirror) for those instead, and kept `FinalGradeResponse` only for the batch route. `AiGradingRunResponse`/`AiCriterionScoreResponse` mirror their entities 1:1, including `AiCriterionScoreResponse.GradingRunId` (present on the entity, not JsonIgnore'd, but omitted from this phase's own original property list — added back to avoid a silent contract change). A `/simplify` pass added `FinalGradeResponse.FromData(FinalGradeData)` for mapper-pattern consistency with the other DTOs (the batch endpoint's Service-layer return type is `FinalGradeData`, a plain contract record from Phase 3 — Service must not reference `Dto/` types, so this mapping happens at the endpoint, same layering as Submission's `SubmissionResponse.FromDomain`).

## Steps

1. Create `Dto/AiGradingRunResponse.cs` (mirrors `AiGradingRun` 1:1) and `Dto/AiCriterionScoreResponse.cs` (mirrors `AiCriterionScore` 1:1):
   - `AiGradingRunResponse` properties: `Id, SubmissionId, Model, Status, RequestMetadata, CreatedAt, CompletedAt, Scores` (nested list).
   - `AiCriterionScoreResponse` properties: `Id, SubmissionId, RubricCriterionId, MaxScore, SuggestedScore, Deductions, Evidence, Comment, Confidence`.
   - Both as `public sealed record` with `init` properties.
   - Add static `FromDomain(AiGradingRun)`/`FromDomain(AiCriterionScore)` mappers.

2. Move response DTOs from bottom of `GradesEndpoints.cs` into `Dto/`:
   - `RegradeRequest` → `Dto/RegradeRequest.cs` (unchanged).
   - `PublishGradeRequest` → `Dto/PublishGradeRequest.cs` (unchanged).
   - `FinalGradeResponse` → `Dto/FinalGradeResponse.cs` (unchanged).
   - `PublishedGradeResult` → `Dto/PublishedGradeResult.cs` — mirrors the current record exactly: `FinalGrade` (or its DTO once mapped), `PublishedAt`, and the full `AiGradingRun` (via `AiGradingRunResponse`, including `Scores`) when the published grade has a `GradingRunId`. This is deliberate (see Design Constraints above) — verify by byte-diffing the response before/after for all three roles (student/lecturer/admin): must match exactly, no new restriction introduced.
   - `PublishAllResponse` → `Dto/PublishAllResponse.cs` (unchanged).

3. Rewrite `Endpoints/GradesEndpoints.cs`:
   - Each handler now takes `IGradingService` (and no longer `GradingDbContext`, `ISubmissionApiClient`, `ICatalogApiClient`, `IBackgroundJobClient` — those are behind the service).
   - Add a helper method `TryBuildRequesterContext(ClaimsPrincipal user)` that returns `RequesterContext?` — validate `ClaimTypes.NameIdentifier` parses to a `Guid` if needed, return null if validation fails.
   - Wrap each service call in exception mapping:
     - `GradingForbiddenException → Results.Forbid()`
     - `GradeNotFoundException → Results.NotFound()`
     - `InvalidGradingRunException → Results.BadRequest(new { error = ex.Message })`
     - Success → `Results.Ok(DTO.FromDomain(...))` / `Results.Created(...)` / `Results.Accepted()`
   - For batch/complex returns (e.g., `GetFinalGradesBatchAsync` returning a list of `FinalGradeData`), map each item to the DTO.

## Success Criteria

- `dotnet build` compiles with zero errors.
- `Dto/` folder exists with all request/response DTOs moved into it.
- All response DTOs have `FromDomain(...)` static mappers.
- `Endpoints/GradesEndpoints.cs` has zero direct EF Core references, zero direct client (`ISubmissionApiClient`, `ICatalogApiClient`) references.
- Byte-diff (ignoring JSON key order) of response bodies for all 7 endpoints before/after this phase — must match (wire format unchanged), including `GET /grades/{submissionId}/result` for the student role (which legitimately includes the published run's `Scores` — see Design Constraints).

## Quality and Testing State

- Quality: approved — `plans/grading-layered-refactor/quality/phase-04-slim-endpoints-and-dto-quality-report.json`, receipt issued
- Testing: manual only. `dotnet build` passed 0/0; DTO property mirroring verified against Domain entities (including the `FinalGradeDetailResponse`/`FinalGradeResponse` split and `AiCriterionScoreResponse.GradingRunId` fix). Byte-diff / live endpoint smoke test deferred to Phase 6.

## Manual Verification

1. `dotnet build` — zero errors.
2. Byte-diff (ignoring JSON key order) each endpoint's response:
   - `GET /grades/{submissionId}/runs` — captured before Phase 2 vs now.
   - `GET /grades/{submissionId}/result` — compare before/after for student/lecturer/admin roles; the published run's full `Scores` are expected to appear for the student who owns the submission (deliberate, see Design Constraints) — confirm this is unchanged, not newly restricted.
   - `GET /grades/{submissionId}/final` — compare before/after.
   - `GET /grades/final?submissionIds=...` — batch response, compare before/after.
   - `POST /grades/{submissionId}/publish` — response shape, compare before/after.
   - `POST /grades/publish-all` — response shape (Published/Skipped/Failed), compare before/after.
   - `POST /grades/{submissionId}/regrade` — response shape, compare before/after.
3. Full 7-route manual pass again (all role cases) — all must still behave identically, including 403/404/201/200/202/409 status codes.
4. Confirm Swagger/OpenAPI schema for response types is unchanged (compare generated `swagger.json` before/after refactor).
