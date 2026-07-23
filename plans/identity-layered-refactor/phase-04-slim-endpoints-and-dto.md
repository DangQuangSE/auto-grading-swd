# Phase 4: Slim endpoints + Dto layer

## Requirements

`AuthEndpoints.cs` and `UsersEndpoints.cs` become pure HTTP adapters: extract request → build `RequesterContext` → call `IAuthService`/`IUserService` → map result/exception → `IResult`. Introduce `Dto/` for request/response shapes, without changing the wire format of any response.

## Design Constraints

- Response JSON shape must not change — this is a behavior-preserving refactor, not an API redesign. If `UserSummary` or `LoginResponse` needs a property the entity doesn't expose 1:1, stop and flag it rather than silently changing the contract.
- `Endpoints/{AuthEndpoints, UsersEndpoints}.cs` should contain no business logic other than the exception→`IResult` mapping — no loops, no EF Core, no direct repository calls, no event publishing.
- `BulkImportForm` stays in `Dto/` because it needs `IFormFile` — this is the one DTO that's allowed to touch ASP.NET Core types (consumed and translated to a file stream at the top of the endpoint handler, never passed into Service).

Preflight: Identity's endpoint DTOs are already well-shaped compared to Submission/Grading's problems:
- Request DTOs at bottom of current endpoint files: `RegisterRequest`, `LoginRequest`, `GoogleLoginRequest`, `UpdateUserRequest`, `BulkImportForm` (all good, move to `Dto/`, unchanged).
- Response DTOs: `LoginResponse`, `UserSummary`, `RosterImportRowResult`, `RosterImportReport` (all already in current endpoint files, move to `Dto/`, unchanged).
- One DTO not in the endpoint file but inferred from domain: `UserAuthResult` (created in Phase 3 for service return value) — add a `FromDomain` mapper if needed, or inline the mapping at the endpoint's `Results.Created` call site.
- No response DTO for the `GET /users` list itself (just returns a list of `UserSummary` records).

Exception→`IResult` mapping needed in both endpoint files:
- Auth: `UserAlreadyExistsException` → `409 Conflict(...)`, `ClassNotFoundException` → `400 BadRequest(...)`, `InvalidGoogleTokenException` → `401 Unauthorized()`, `EducationEmailNotVerifiedException` → `403 Forbid()`, `UserNotFoundException` → `401 Unauthorized()` (for login failures).
- Users: `UserNotFoundException` → `404 NotFound()`, `RosterAuthorizationException` → `403 Forbid()`, `DbUpdateConcurrencyException` → `409 Conflict(...)`.

Preflight: exception→`IResult` mapping was already correct and complete in Phase 3 (service threw exceptions, endpoints in Phase 2 already had `catch` blocks with appropriate return codes), so this phase's endpoint changes are purely additive: move DTOs to `Dto/`, slim the handler bodies to call service and map responses, use exception handling from Phase 3's interim implementation.

**Corrections/additions made during implementation:**
1. Catch blocks previously hardcoded literal messages that duplicated what the exception already carries (sourced from `IdentityConstants` since Phase 1) — switched to `ex.Message`, matching Submission's/Grading's Phase 4 precedent.
2. Formalized Register's anonymous-object response (`new { user.Id, user.Email, Role = ... }`) into a named `Dto/RegisterResponse.cs` — **critical detail**: the anonymous object's property is `Id`, not `UserId`; `RegisterResponse` uses `Id` too, to avoid silently renaming the JSON property (a `/simplify` pass initially suggested `UserId` for consistency with the service-layer `UserAuthResult.UserId`, caught and corrected before it became a live contract change).
3. Added `LoginResponse.FromData(AuthTokenResult)` for consistency with the other DTOs' `FromData` mapper pattern.
4. Rejected a `/simplify` suggestion to make Register's response shape match `LoginResponse` — they're deliberately different (Register returns no token, since the user isn't authenticated yet; that's exactly why the original endpoint used a different anonymous shape for it).

## Steps

1. Create `Dto/` folder and move all request/response DTOs into it (from current endpoints):
   - `Dto/RegisterRequest.cs`
   - `Dto/LoginRequest.cs`
   - `Dto/GoogleLoginRequest.cs`
   - `Dto/LoginResponse.cs`
   - `Dto/UpdateUserRequest.cs`
   - `Dto/UserSummary.cs`
   - `Dto/BulkImportForm.cs` (keep `IFormFile` here, even though it's an ASP.NET Core type, because `[FromForm]` binding requires it; translate it to a file stream at the top of the endpoint handler, never pass the form object into Service).
   - `Dto/RosterImportRowResult.cs`
   - `Dto/RosterImportReport.cs`

2. Add static `FromDomain` mappers where needed:
   - `UserSummary.FromDomain(User user, string? className)` if not already present.
   - If `UserAuthResult` (from Phase 3 service) has a different shape than what the register/login endpoints return, create a mapper or inline the translation at the response site.

3. Rewrite `Endpoints/AuthEndpoints.cs`:
   - Each handler now takes `IAuthService` (and no longer `IdentityDbContext`, `IPasswordHasher<User>`, `JwtTokenGenerator`, `IEventBus` — those all moved behind the service).
   - `RegisterAsync`: call `authService.RegisterAsync(request, ct)`, catch `UserAlreadyExistsException` → `Results.Conflict(new { message = ex.Message })`, catch `ClassNotFoundException` → `Results.BadRequest(new { message = ex.Message })`, success → `Results.Created($"/auth/users/{result.UserId}", new LoginResponse(...))` (map `UserAuthResult` to the response DTO).
   - `LoginAsync`: call `authService.LoginAsync(request, ct)`, catch `UserNotFoundException` → `Results.Unauthorized()`, success → `Results.Ok(response)`.
   - `GoogleLoginAsync`: call `authService.GoogleLoginAsync(request, ct)`, catch `InvalidGoogleTokenException` → `Results.Unauthorized()`, catch `EducationEmailNotVerifiedException` → `Results.Forbid()`, success → `Results.Ok(response)`.
   - Use `Constant/IdentityConstants.cs` (from Phase 1) for error message strings.

4. Rewrite `Endpoints/UsersEndpoints.cs`:
   - Each handler builds `RequesterContext` from `ClaimsPrincipal` at the top (via helper: `RequesterContext? requester = TryBuildRequesterContext(caller); if (requester is null) return Results.Forbid();`).
   - `GET /users`: call `userService.ListAsync(ids, requester, ct)`, catch no exceptions (list returns a filtered result, no errors), return `Results.Ok(result)`.
   - `PATCH /users/{userId}`: call `userService.UpdateAsync(userId, request, requester, ct)`, catch `UserNotFoundException` → `Results.NotFound()`, catch `RosterAuthorizationException` → `Results.Forbid()`, catch `DbUpdateConcurrencyException` → `Results.Conflict(new { message = ex.Message })`, success → `Results.Ok(result)`.
   - `POST /users/bulk-import`: bind `BulkImportForm form`, build requester context, call `userService.BulkImportAsync(form.File.OpenReadStream(), form.File.FileName, requester, ct)` inside an `await using` block (stream disposal owned by endpoint), catch `RosterFileParseException` (custom exception thrown by service if `RosterFileParser.Parse` returns an error) → `Results.BadRequest(new { message = ex.Message })`, success → `Results.Ok(report)`.

5. Use `Constant/IdentityConstants.cs` for all error message strings instead of inline literals.

## Quality and Testing State

- Quality: approved — `plans/identity-layered-refactor/quality/phase-04-slim-endpoints-and-dto-quality-report.json`, receipt issued
- Testing: manual only. `dotnet build` passed 0/0; DTO 1:1 mirroring verified, including the `RegisterResponse.Id`-not-`UserId` near-miss. Endpoints grep-confirmed free of EF Core/event-bus calls. Live smoke test deferred to Phase 6.

## Manual Verification

1. `dotnet build` — zero errors.
2. Compare `swagger.json` or OpenAPI schema for Auth/Users endpoints before this phase vs. after — routes, status codes, and schemas must be unchanged.
3. Full 6-route manual pass again (register/login/google/list/get/update/bulk-import × student/lecturer/admin), plus the edge cases from Phase 3:
   - Duplicate email, class not found, invalid Google token, unverified email, concurrent modification, bulk-import file parse error, bulk-import authorization denial.
   - Verify error response bodies match pre-refactor format exactly (especially `409 Conflict` responses with `usedAttempts`/`maxAttempts` — Identity has no such fields, but other error bodies must be byte-identical).
4. Confirm `Endpoints/` files contain no business logic loops, no direct EF Core references (grep for `db.` should return zero), no event bus calls (grep for `eventBus.Publish` should return zero in endpoints).
5. Byte-diff the JWT token returned by `POST /auth/login` and `POST /auth/google` before/after refactor — payload and signature should be identical (token claims, expiration, issuer).
