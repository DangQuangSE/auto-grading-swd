# Phase 1: Constants + Interfaces (additive only)

## Requirements

Create the new folders and contracts with zero behavior change. Nothing that currently works stops working — this phase only adds files and moves two interface *declarations* (not their implementations). After this phase, both endpoint files still call `IdentityDbContext` directly; `Interfaces/` is scaffolding for Phase 2+.

## Design Constraints

- Zero behavior change — this phase must compile and the app must run identically to before, since nothing yet calls the new repository interface or uses the new exception types.
- `IUserRepository` methods are named after what the caller needs (e.g., `ExistsByEmailAsync`, `IsClassLecturerAsync`), not after raw CRUD.
- Do not touch `Endpoints/`, `Handlers/`, or `Program.cs` in this phase — this is groundwork only.
- `RequesterContext` is a record with no dependency on `ClaimsPrincipal` or any ASP.NET Core type; it's the "pure data carrier" endpoints build before calling services.

Preflight: **Correction to this plan's own earlier assumption**: root namespace is `AutoGrading.Identity.Api.*` — plain, matching the project folder exactly, verified directly against `Endpoints/AuthEndpoints.cs`, `Endpoints/UsersEndpoints.cs`, `Auth/GoogleAuthOptions.cs`, `Authorization/RosterAuthorization.cs`. Unlike Submission (`AutoGrading.SubmissionSvc.Api`), Identity does **not** use an "Svc" suffix — do not carry that pattern over. Existing convention colocates interface + concrete implementation in one file (none currently — Identity has no interfaces yet, only concrete `IdentityDbContext` and static helpers). DI registration style: top-level `Program.cs`, `builder.Services.AddScoped<TInterface, TImpl>()`. Primary-constructor DI (`class Foo(Dep dep) : IFoo`) is the established style for new classes — note the existing `IdentityDbContext` itself uses a traditional constructor body, not primary-constructor syntax (unlike Submission's/Grading's `DbContext(options)` one-liners) — leave it as-is when moving it in Phase 2, don't rewrite its constructor style as part of the move. No custom exception types exist yet in this service; `UserAlreadyExistsException`, `ClassNotFoundException`, `UserNotFoundException`, `RosterAuthorizationException` are new precedent, kept `sealed`, messages sourced from `IdentityConstants`.

## Steps

1. Create `Constant/IdentityConstants.cs` with the error message strings currently inlined in `AuthEndpoints.cs` and `UsersEndpoints.cs`:
   - `EmailAlreadyRegistered = "Email already registered."`
   - `ClassNotFoundOrNotSynced = "Class not found or not yet synchronized; please try again or contact your administrator."`
   - `UserNotFound = "User not found."`
   - `RosterAuthorizationDenied = "Not authorized to modify this student's roster fields."`
   - `UnknownClass = "unknown class"` (used in bulk-import row skip reason)
   - `EmailNotRegistered = "email not registered"` (used in bulk-import row skip reason)
   - `NotAuthorizedForStudent = "not authorized for this student"` (used in bulk-import row skip reason)
   - `ConcurrentModificationError = "User {0} was modified concurrently; reload and try again."` (current code interpolates `userId` directly into the string at `UsersEndpoints.cs:162` — use `string.Format(IdentityConstants.ConcurrentModificationError, userId)` at the Phase 4 call site to preserve the exact message text)
   - (Do not use these yet — endpoints still have their own inline strings until Phase 4.)

2. Create `Interfaces/IUserRepository.cs` — new contract, methods derived from what both endpoints currently do inline against `IdentityDbContext`. Derive exact signatures from the actual code (cite line numbers the way prior phase files did):
   ```
   // Auth/register path (AuthEndpoints line 37)
   Task<bool> ExistsByEmailAsync(string email, CancellationToken ct);
   
   // Auth/register path (AuthEndpoints line 42)
   Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct);
   
   // Auth/register and Google-new-user paths (AuthEndpoints lines 47–58, 123–131)
   Task<Guid> CreateUserAsync(User user, CancellationToken ct);
   
   // Auth/login path (AuthEndpoints line 73)
   Task<User?> GetByEmailAsync(string email, CancellationToken ct);
   
   // Auth/Google-login path (AuthEndpoints line 119)
   Task<User?> GetByGoogleSubjectOrEmailAsync(string googleSubjectId, string email, CancellationToken ct);
   
   // Auth/Google-new-subject-link path (AuthEndpoints lines 135–138)
   Task LinkGoogleSubjectIdAsync(Guid userId, string googleSubjectId, CancellationToken ct);
   
   // Users/list and single-update paths (UsersEndpoints lines 21–22, 129)
   Task<User?> GetByIdAsync(Guid userId, CancellationToken ct);
   Task<List<User>> ListAsync(IReadOnlyCollection<Guid>? ids, CancellationToken ct);
   
   // Users/bulk-import per-row class lookup (UsersEndpoints lines 75–81)
   Task<ClassLecturerCache?> GetClassLecturerCacheByNameAsync(string className, CancellationToken ct);
   
   // Users/list response formatter (UsersEndpoints lines 207–210)
   Task<Dictionary<Guid, string>> ResolveClassNamesAsync(IReadOnlyCollection<Guid> classIds, CancellationToken ct);
   
   // Users/bulk-import authorization check (extracted from RosterAuthorization lines 41–42, 49–51)
   Task<bool> IsClassLecturerAsync(Guid classId, Guid lecturerId, CancellationToken ct);
   Task<bool> IsGraderForStudentAsync(Guid studentId, Guid lecturerId, CancellationToken ct);
   
   // Users/single-update (UsersEndpoints lines 156–162) — throws DbUpdateConcurrencyException on conflict
   Task UpdateRosterFieldsAsync(Guid userId, string? studentCode, Guid? classId, CancellationToken ct);
   
   // Users/bulk-import atomic upsert (UsersEndpoints lines 108–117)
   Task BulkUpdateRosterAsync(IReadOnlyList<(Guid UserId, string? StudentCode, Guid ClassId)> updates, CancellationToken ct);
   ```

3. Define exception types in `Domain/Exceptions/` or alongside `Interfaces/IUserRepository.cs`:
   - `UserAlreadyExistsException(string email)` — thrown by **`AuthService.RegisterAsync`** (Phase 3), *after* calling `repository.ExistsByEmailAsync` and finding it `true`, *before* calling `CreateUserAsync` (replaces endpoint's `409 Conflict`). **Not** thrown by `CreateUserAsync` itself — `CreateUserAsync` performs no existence check of its own, matching today's plain "check then create" pattern with no race-condition guard (the original code has no `try/catch` around its `SaveChangesAsync` either; this refactor preserves that exact, pre-existing TOCTOU behavior rather than hardening it, since adding new race protection would be a behavior change outside this refactor's scope).
   - `ClassNotFoundException(Guid classId)` — thrown by **`AuthService.RegisterAsync`** the same way, after `repository.ClassExistsAsync` returns `false` for a supplied `classId` (replaces endpoint's `400 BadRequest`). Also **not** thrown by `CreateUserAsync`.
   - `UserNotFoundException(Guid userId)` — thrown by `UserService.UpdateAsync`/`GetAsync` after `repository.GetByIdAsync` returns `null` (replaces endpoint's `404 NotFound`). `UpdateRosterFieldsAsync` itself only throws `DbUpdateConcurrencyException` (an EF Core type, allowed to bubble since it's not HTTP-specific) — the not-found check happens one level up, in Service, via the same fetch-then-check pattern.
   - All carry no HTTP-specific data; service/endpoint layer maps them to `IResult` in Phase 4.

4. Create `RequesterContext.cs` (record): `public sealed record RequesterContext(Guid? UserId, bool IsStudent, bool IsLecturer, bool IsAdmin);`. This is the "pure data carrier" that endpoints build from `ClaimsPrincipal` before calling services — keeps services free of ASP.NET Core auth types.

## Quality and Testing State

- Quality: approved — `plans/identity-layered-refactor/quality/phase-01-constants-and-interfaces-quality-report.json`, receipt issued
- Testing: manual only (no automated test project). `dotnet build` passed 0/0.

## Manual Verification

1. `dotnet build` on `AutoGrading.Identity.Api` — must compile with zero errors/warnings introduced.
2. Run the service locally (`dotnet run` or via docker-compose), confirm `POST /auth/register`, `POST /auth/login`, `POST /auth/google`, `GET /users`, `PATCH /users/{userId}`, `POST /users/bulk-import` all behave exactly as before (nothing in this phase should have changed runtime behavior — this is a smoke check that the folder/namespace structure is correct).
