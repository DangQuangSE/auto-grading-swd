# Phase 3: Service layer

## Requirements

Move the remaining logic out of `AuthEndpoints.cs` and `UsersEndpoints.cs` — business rules, authorization decisions, event publishing, and data transformations — into `Service/AuthService.cs` and `Service/UserService.cs`. After this phase, the endpoint handlers only extract data from the HTTP request/`ClaimsPrincipal`, build a `RequesterContext`, and hand it to the service. The auth logic itself (password hashing, JWT generation, Google token validation) stays **identical line-for-line**; only its location changes.

## Design Constraints

- `Service/` must not reference `Microsoft.AspNetCore.*` types (`ClaimsPrincipal`, `IFormFile`, `IResult`) — this is the actual "endpoint isn't the boss" boundary the whole refactor exists to create. If a Service method needs to signal "not found" or "forbidden", it throws a plain exception; the endpoint layer maps exceptions to `IResult` in Phase 4.
- **Auth logic is security-sensitive:** the password hashing, JWT generation, and Google token validation methods are ported from `AuthEndpoints.cs` line-for-line into `AuthService.cs` *with no refactoring*. The logic bodies never change. If you see an optimization opportunity, defer it to post-refactor; this phase is structural only.
- `RequesterContext` validation happens in the endpoint, before calling the service — if `ClaimTypes.NameIdentifier` is missing or not a valid `Guid` for a student role, the endpoint returns `Results.Forbid()` immediately and never constructs/calls into Service.
- `UserService.BulkImportAsync` must never call `SaveChangesAsync` itself — it builds the accepted rows' updates list and makes exactly ONE call to `repository.BulkUpdateRosterAsync(...)`, which handles the atomic commit.
- **Stream ownership** (for `BulkImportAsync`): `UserService` only reads from the file stream passed by the endpoint — it must never dispose it. The endpoint handler opens the stream in `await using` (same as today) so it's disposed on the way out regardless of whether `UserService` throws.
- `RosterFileParser` is called directly from `UserService` (not injected) — it's a static utility with no mock/interface needs.
- **Authorization helper visibility:** the per-row and per-user authorization logic from `RosterAuthorization.cs` becomes `UserService.AuthorizeRosterAccessAsync(RequesterContext caller, User target, CancellationToken ct)` → `RosterAuthorizationResult`, a **private method inside `UserService`**, not a separate interface. It calls `repository.IsClassLecturerAsync` and `repository.IsGraderForStudentAsync` for its data needs.

Preflight: `RequesterContext` is built from `ClaimsPrincipal` in the endpoint via a helper like `TryBuildRequesterContext(ClaimsPrincipal caller)` → `RequesterContext?` (returns null if the role claims are malformed; endpoint returns `Results.Forbid()` in that case). Two exception types from Phase 1 carry authorization failures: `RosterAuthorizationException(RosterAuthorizationResult actual)` is not used here — the result enum itself is private to the service, communicated to callers via exceptions thrown on authorization denial. If a write operation is unauthorized, throw `RosterAuthorizationException` (caught by endpoint in Phase 4, mapped to `403 Forbid`). The endpoint's role-based route protection (`RequireAuthorization(policy => policy.RequireRole(...))`) is a *first gate* (returns 401/403 before the handler is even called); service-level authorization (lecturer checking if they can edit a specific student) is the *second gate* (returns 403 if the requester's role is inadequate *for that specific operation*). Both gates stay in place.

**Corrections made during implementation:**
1. **Circular-dependency avoidance**: `IAuthService`/`IUserService` methods take primitive parameters (`string email, string password, ...`), not the endpoint's own request record types (`RegisterRequest`, `UpdateUserRequest`, etc.) — those still live in `Endpoints/`, and `Interfaces/` referencing them would create a backwards `Interfaces → Endpoints` dependency. Matches the established precedent from Submission's/Grading's Phase 3. New records `UserAuthResult`/`AuthTokenResult` (auth) and `UserSummaryData`/`RosterImportResult`/`RosterImportRowOutcome` (users) live in `Interfaces/` instead, mirroring the endpoint's shapes; the endpoint maps between them at the call site. Phase 4 may consolidate this via `Dto/` mappers.
2. **`IUserService.GetAsync` omitted** — the plan's Step 2 listed it, but the original code has no `GET /users/{userId}` route at all (only `GET /`, `PATCH /{userId}`, `POST /bulk-import`); adding it would be a phantom, uncalled method.
3. **`Authorization/RosterAuthorization.cs` deleted** (not just retired-in-place) — its `RosterAuthorizationResult` enum moved into `Interfaces/IUserService.cs` since `UserService.AuthorizeRosterAccessAsync` is now its only consumer, per this plan's own decision #2.
4. **`GoogleAuthOptions` accessed via `IOptions<GoogleAuthOptions>`** directly (a plain .NET options abstraction, not ASP.NET-Core-specific) — matches the original endpoint's dependency exactly, no new wrapper type invented.
5. **`UpdateRosterFieldsAsync`'s repository signature changed** (during a `/simplify` pass) to take the already-fetched `User` entity instead of a bare `Guid userId` — `UserService.UpdateAsync` already fetches+tracks the user via `GetByIdAsync` for the not-found/authorization checks; re-fetching by id inside `UpdateRosterFieldsAsync` was a redundant round-trip. Same fix pattern already applied to `BulkUpdateRosterAsync` in Phase 2.

## Steps

1. Create `Interfaces/IAuthService.cs`:
   ```
   Task<UserAuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct);
   Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
   Task<LoginResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken ct);
   ```
   where `UserAuthResult` is a record: `public sealed record UserAuthResult(Guid UserId, string Email, string Role);`.
   Define any new exception types here (e.g., `InvalidGoogleTokenException`, `UserAlreadyExistsException` if re-defined) or in `Domain/Exceptions/`.

2. Create `Interfaces/IUserService.cs`:
   ```
   Task<List<UserSummary>> ListAsync(IReadOnlyCollection<Guid>? ids, RequesterContext requester, CancellationToken ct);
   Task<UserSummary> GetAsync(Guid userId, RequesterContext requester, CancellationToken ct);
   Task<UserSummary> UpdateAsync(Guid userId, UpdateUserRequest request, RequesterContext requester, CancellationToken ct);
   Task<RosterImportReport> BulkImportAsync(Stream fileStream, string fileName, RequesterContext requester, CancellationToken ct);
   ```

3. Create `Service/AuthService.cs` implementing `IAuthService`, injecting `IUserRepository`, `IPasswordHasher<User>`, `JwtTokenGenerator`, `IEventBus`:
   - `RegisterAsync` — reimplements `AuthEndpoints.RegisterAsync` (lines 28–63), checking email existence, checking class existence, creating user with password hash, publishing `UserRegistered` event. Throw `UserAlreadyExistsException` if email exists; `ClassNotFoundException` if classId exists but class doesn't. Do NOT change any of the password hashing or event publishing logic — port it line-for-line.
   - `LoginAsync` — reimplements `AuthEndpoints.LoginAsync` (lines 65–89), looking up user by email, verifying password, generating JWT token. Throw `UserNotFoundException` if email doesn't exist or password is invalid (combined, matching current behavior: endpoint returns `401 Unauthorized` either way).
   - `GoogleLoginAsync` — reimplements `AuthEndpoints.GoogleLoginAsync` (lines 91–144), validating Google token (this is the security-critical part — keep it identical line-for-line), checking email verified/education domain, then exactly three distinct, non-collapsible branches matching lines 121–139: **(1)** `user is null` → create new user (with `GoogleSubjectId` set) via `repository.CreateUserAsync`, publish `UserRegistered`; **(2)** `user is not null && user.GoogleSubjectId is null` → call `repository.LinkGoogleSubjectIdAsync(user.Id, payload.Subject, ct)`, publish **no** event; **(3)** `user is not null && user.GoogleSubjectId is not null` → do nothing (already linked). `UserRegistered` must publish on branch (1) only, never on (2) or (3) — a refactor that collapses "link" and "create" into one path, or that publishes the event on link too, is a behavior regression. Generate JWT token, return `LoginResponse` regardless of which branch ran. Throw `InvalidGoogleTokenException` if token validation fails (replaces endpoint's `401 Unauthorized`); throw `EducationEmailNotVerifiedException` if email not verified or not education domain (replaces endpoint's `403 Forbid`).

4. Create `Service/UserService.cs` implementing `IUserService`, injecting `IUserRepository` only:
   - `ListAsync` — reimplements the `GET /users` handler (lines 17–29 of users endpoint): fetch requested user IDs (or all if null), filter by authorization if not admin (calling new private `AuthorizeRosterAccessAsync` for each user), resolve class names via repository batch lookup, return `UserSummary` list.
   - `GetAsync` — fetch user by id, throw `UserNotFoundException` if not found, check authorization, return `UserSummary` with class name resolved.
   - `UpdateAsync` — fetch user by id (throw `UserNotFoundException` if not found), check authorization via `AuthorizeRosterAccessAsync` (throw `RosterAuthorizationException` on denial), check classId exists if provided, apply mutations, call `repository.UpdateRosterFieldsAsync(...)`, which will throw `DbUpdateConcurrencyException` on conflict — let it bubble (endpoint maps to `409 Conflict`). Return updated `UserSummary`.
   - `BulkImportAsync` — reimplements `BulkImportAsync` handler (lines 50–120 of users endpoint): parse file via `RosterFileParser.Parse(fileStream, fileName)` (static call), loop through rows, per-row: look up class by name via repository, look up user by email via repository, check authorization via `AuthorizeRosterAccessAsync` (build `details` list with skip reasons), collect accepted row updates into a list. Then make exactly ONE call to `repository.BulkUpdateRosterAsync(acceptedRows, ct)`, which atomically persists all or none. Return `RosterImportReport` with the details and counts (exactly as today).
   - **Private method `AuthorizeRosterAccessAsync`** (replicates `RosterAuthorization.AuthorizeAsync` logic, lines 21–54 of auth endpoint): check if caller is admin (immediate grant), check if not lecturer (immediate deny), call `repository.IsClassLecturerAsync(targetUser.ClassId, caller.UserId)` if target has ClassId, call `repository.IsGraderForStudentAsync(targetUser.Id, caller.UserId)` if not a class lecturer, return the result enum `RosterAuthorizationResult`. Used by `ListAsync` (filter), `GetAsync` (per-item check), `UpdateAsync` (per-item check), and per-row in `BulkImportAsync` (fill skip reason).

5. Define `EducationEmailNotVerifiedException` exception if Phase 1 didn't already (thrown by `GoogleLoginAsync` when email is not verified or not an education domain). `InvalidGoogleTokenException` is thrown by `GoogleLoginAsync` when `GoogleJsonWebSignature.ValidateAsync` throws `InvalidJwtException`.

## Quality and Testing State

- Quality: approved — `plans/identity-layered-refactor/quality/phase-03-service-quality-report.json`, receipt issued
- Testing: manual only. `dotnet build` passed 0/0; `Service/` grep-confirmed free of ASP.NET Core transport types; Google login's 3-branch logic and password/JWT call fidelity independently re-verified by the quality reviewer against the original source. Live auth-flow smoke test deferred to Phase 6.

## Manual Verification

1. `dotnet build` — zero errors.
2. Confirm `Service/` contains zero `Microsoft.AspNetCore.*` imports (grep for `using Microsoft.AspNetCore` in `Service/AuthService.cs` and `Service/UserService.cs` should return nothing).
3. **Auth logic security-sensitive pass**: Re-test all auth flows end-to-end, paying special attention that logic is unchanged:
   - `POST /auth/register` with valid/duplicate email/classId cases — same behavior as before, same error messages.
   - `POST /auth/login` with valid/invalid credentials — same behavior, same `401 Unauthorized`.
   - `POST /auth/google` with valid token, invalid token, unverified email, non-education domain — same behavior and error codes as before; verify JWT token is valid (decode it, check userId/email/role claims match).
4. Repeat the same 6-route manual pass from Phase 2 (register/login/google/list/get/update/bulk-import, for student/lecturer/admin roles) — behavior must still be identical. Pay particular attention to:
   - Authorization deny cases (lecturer trying to update unauthorized student, student trying to list users) — should be `403 Forbid` or `401 Unauthorized` as before.
   - Bulk-import failure case: upload 10 rows, all valid; mid-loop simulate a `SaveChangesAsync` failure (e.g., truncate the `Users` table mid-import), confirm no rows were updated (all-or-nothing preserved).
5. Verify `RosterFileParser.Parse` is called once per bulk-import, not multiple times (grep for calls to `Parse` in the service, should be exactly one).
