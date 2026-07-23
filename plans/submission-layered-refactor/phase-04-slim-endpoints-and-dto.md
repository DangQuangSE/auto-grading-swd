# Phase 4: Slim endpoints + Dto layer

**Maps to:** P1 story 1 (endpoint chỉ bind/gọi/map), P2 story (Dto tách khỏi Endpoint).

## Goal

`SubmissionsEndpoints.cs` becomes pure HTTP adapter: bind request → build `RequesterContext` → call `ISubmissionService` → map result/exception → `IResult`. Introduce `Dto/` for request/response shapes, without changing the wire format of any response.

## Steps

1. Create `Dto/SubmissionResponse.cs` and `Dto/ExtractedArtifactResponse.cs` as `public sealed record` types with `init` properties (per this repo's C# style rules — record for immutable value-like models), mirroring `Domain/Submission.cs`/`Domain/ExtractedArtifact.cs` exactly (same property names/types/casing, so `System.Text.Json` produces byte-identical output): `Id, AssignmentId, StudentId, AttemptNumber, ReportObjectKey, DiagramObjectKey, State, CreatedAt, UpdatedAt, Artifacts`. Add a static `FromDomain(Submission)` mapper.
   - **This is the risk called out in plan.md** — verify with a real before/after JSON diff, not just "looks right". Before wiring the mapper in, diff `Domain/Submission.cs` and `Domain/ExtractedArtifact.cs` property-by-property against the two new DTOs to confirm nothing was dropped — a byte-diff of the JSON alone can hide a missing property if the test data happens not to exercise it (e.g. a null/empty `Artifacts` list wouldn't reveal a missing `ExtractedArtifactResponse` field).

2. Move `UploadSubmissionForm` (currently declared at the bottom of `SubmissionsEndpoints.cs`) into `Dto/UploadSubmissionForm.cs`, unchanged (still needs `IFormFile` — this is the one DTO that's allowed to touch ASP.NET Core types, since `[FromForm]` binding requires it; it's consumed and translated to `UploadSubmissionCommand` right at the top of the endpoint handler, never passed into `Service/`).

3. Rewrite `Endpoints/SubmissionsEndpoints.cs`:
   - Each handler now takes `ISubmissionService` (and no longer `SubmissionDbContext`/`ICatalogApiClient`/`IObjectStorage`/`IEventBus`/`IBackgroundJobClient` — those all moved behind the service).
   - Build `RequesterContext` from `ClaimsPrincipal` at the top of each handler.
   - Wrap the service call, map thrown exceptions to `IResult`: `SubmissionNotFoundException → Results.NotFound()`, `SubmissionForbiddenException → Results.Forbid()`, `SubmissionAttemptLimitReachedException/SubmissionAttemptConflictException → Results.Conflict(new { error = ex.Message, usedAttempts = ex.Used, maxAttempts = ex.Max })` (same shape as today), success → `Results.Ok(SubmissionResponse.FromDomain(result))` / `Results.Created(...)` / `Results.Accepted()`.
   - Use `Constant/SubmissionConstants.cs` (from Phase 1) for the error message strings instead of the inline literals.

## Design Constraints

- Response JSON shape must not change — this is a behavior-preserving refactor, not an API redesign. If `SubmissionResponse` needs a property the entity doesn't expose 1:1, stop and flag it rather than silently changing the contract.
- `Endpoints/SubmissionsEndpoints.cs` should contain no `catch` blocks around business logic other than the exception→`IResult` mapping — no loops, no EF Core, no direct storage/event-bus calls.

## Quality and Testing State

- Quality: not evaluated
- Testing: not started

## Manual Verification

1. `dotnet build` — zero errors.
2. Byte-diff (ignoring JSON key order) the response body of `GET /submissions/{id}` for an existing submission, captured before Phase 2 started vs. after this phase — must match.
3. Full 4-route manual pass again (list/get/upload/retry × student/lecturer/admin), plus the attempt-limit, attempt-conflict, and storage-failure-rollback cases from Phase 3 — all must still behave identically.
4. Confirm Swagger/OpenAPI schema for `Submission`-shaped responses is unchanged (compare generated `swagger.json` before/after).
5. Confirm all four routes still carry the same `.RequireAuthorization(...)` role policy as before the rewrite (`student/lecturer/admin` on list/get/upload/retry, plus `service` additionally on `GET /{id}`) — a careless rewrite of the `MapGroup`/route chain can silently drop this.
