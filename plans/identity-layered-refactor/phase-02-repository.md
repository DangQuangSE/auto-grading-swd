# Phase 2: Repository layer

## Requirements

Move ALL EF Core access out of `AuthEndpoints.cs` and `UsersEndpoints.cs` into `Repository/UserRepository.cs`, implementing `IUserRepository` from Phase 1. Do this as **one complete pass across both endpoint files**, not endpoint-by-endpoint — a half-migrated state (some routes on `UserRepository`, others still on raw `IdentityDbContext`) is riskier than a single complete swap when there's no automated test net.

Endpoints in this phase **do** start calling the repository instead of `IdentityDbContext` — but authorization logic and business rules stay inline in the endpoints for now (that's Phase 3). This phase only relocates *data access*.

## Design Constraints

- Repository methods must not accept or return HTTP types (`IResult`, `ClaimsPrincipal`) — only domain types and plain scalars/exceptions.
- `IdentityDbContext` must no longer be referenced from `Endpoints/` after this phase (grep for `IdentityDbContext` in `Endpoints/` should return zero matches once this phase is done).
- All three handlers (`ClassLecturerAssignedHandler`, `SubmissionUploadedHandler`, `GradePublishedHandler`) still inject `IdentityDbContext` directly in this phase — Phase 5 will update them. Do not change handlers yet.
- `BulkUpdateRosterAsync` must preserve the "save all accepted rows in one `SaveChangesAsync`, or save none" atomicity exactly as today — internally track all rows, then one commit.

Preflight: same repo conventions as Phase 1 (root namespace `AutoGrading.Identity.Api.*`, primary-constructor DI, `Program.cs` top-level `AddScoped<TInterface, TImpl>`). No new conventions to discover — this phase's only structural addition is the `Repository/` folder holding both `IdentityDbContext` (moved) and the new `UserRepository` implementation, mirroring how Submission's `Repository/` works.

Two adaptations the plan text didn't spell out, resolved during implementation:
1. **`Authorization/RosterAuthorization.cs`'s signature changed** from `AuthorizeAsync(ClaimsPrincipal, User, IdentityDbContext, CancellationToken)` to `AuthorizeAsync(ClaimsPrincipal, User, IUserRepository, CancellationToken)` — necessary because `UsersEndpoints.cs` no longer has an `IdentityDbContext` to pass it once this phase's Design Constraint ("no `IdentityDbContext` in `Endpoints/`") takes effect. Its two raw EF queries now delegate to `repository.IsClassLecturerAsync`/`IsGraderForStudentAsync`. This is groundwork for Phase 3's eventual retirement of the static class, not a Phase 3 activity pulled forward — the method body/logic is unchanged, only its data-access mechanism.
2. **`BulkUpdateRosterAsync`'s signature takes already-fetched `User` entities**, not bare `Guid` ids — a `/simplify` pass caught that the original design (passing just `(Guid UserId, ...)` tuples) would force a second per-row fetch-by-id inside `BulkUpdateRosterAsync`, when the entity was already fetched (and already tracked, since `GetByEmailAsync` doesn't `AsNoTracking()`) during `BulkImportAsync`'s validation loop. Passing the tracked entity through avoids the redundant query — same pattern as Submission's `SaveUploadResultAsync(submission, ...)` taking an already-tracked entity from an earlier repository call.

## Steps

1. Move `Data/IdentityDbContext.cs` → `Repository/IdentityDbContext.cs`. Update namespace/usings everywhere it's referenced — **including `Handlers/ClassLecturerAssignedHandler.cs` line 4**, `Handlers/SubmissionUploadedHandler.cs` line 4, `Handlers/GradePublishedHandler.cs` line 4, `Program.cs` line 20, and `SeedTestAccountsAsync` method in `Program.cs` line 73 (they reference `IdentityDbContext` type, not import it, so make sure the `using` statements update). This must happen in this phase even though handlers still depend on `IdentityDbContext` directly until Phase 5 — otherwise the move breaks the build immediately.

2. Create `Repository/UserRepository.cs` implementing `IUserRepository`:
   - `ExistsByEmailAsync` → the `db.Users.AnyAsync(u => u.Email == email, ct)` call (line 37 of auth endpoint).
   - `ClassExistsAsync` → the `db.ClassLecturerCaches.AnyAsync(c => c.ClassId == classId, ct)` call (line 42 of auth endpoint).
   - `CreateUserAsync` → instantiate `db.Users.Add(user); await db.SaveChangesAsync(ct); return user.Id;` (lines 57–58 of auth endpoint); handle the case where `ExistsByEmailAsync` was already called by the caller (no duplicate email check in this method — that's the caller's responsibility, matching today's "check then create" pattern).
   - `GetByEmailAsync` → the `db.Users.SingleOrDefaultAsync(u => u.Email == email, ct)` (line 73 of auth endpoint).
   - `GetByGoogleSubjectOrEmailAsync` → the `db.Users.SingleOrDefaultAsync(u => u.GoogleSubjectId == googleSubjectId || u.Email == email, ct)` (line 119 of auth endpoint).
   - `LinkGoogleSubjectIdAsync` → fetch user by id, set `user.GoogleSubjectId`, `SaveChangesAsync` (lines 135–138 of auth endpoint).
   - `GetByIdAsync` → the `db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)` (line 129 of users endpoint).
   - `ListAsync` → the `db.Users.AsNoTracking()` chain with optional filter (lines 21–22 of users endpoint); return a tracked list (not `AsNoTracking()`) so the caller can mutate and save.
   - `GetClassLecturerCacheByNameAsync` → the `db.ClassLecturerCaches.FirstOrDefaultAsync(c => c.ClassName.ToLower() == className.ToLower(), ct)` (lines 75–76 of users endpoint).
   - `ResolveClassNamesAsync` → the `db.ClassLecturerCaches.Where(...).ToDictionaryAsync(...)` call (lines 208–210 of users endpoint).
   - `IsClassLecturerAsync` → the `db.ClassLecturerCaches.AnyAsync(c => c.ClassId == classId && c.LecturerId == lecturerId, ct)` query (lines 41–42 of RosterAuthorization.cs).
   - `IsGraderForStudentAsync` → the `db.SubmissionGraders.Join(...).AnyAsync(...)` query (lines 49–51 of RosterAuthorization.cs).
   - `UpdateRosterFieldsAsync` → fetch user by id (throw `UserNotFoundException` if not found), apply mutations (lines 146–153 of users endpoint), `SaveChangesAsync`; **catch `DbUpdateConcurrencyException` and re-throw it** so the endpoint can map it to `409 Conflict` (today's behavior on line 160–162).
   - `BulkUpdateRosterAsync` → fetch all requested users by ID, apply mutations in-place from the updates list (preserving order/exact semantics), `SaveChangesAsync` once. If `SaveChangesAsync` throws `DbUpdateException`, don't catch it — let it bubble (endpoint/caller decides how to handle). This method is the bottleneck that preserves atomicity: all rows in `updates` are persisted together or not at all, never piecemeal.

3. Update `AuthEndpoints.cs` and `UsersEndpoints.cs` to inject `IUserRepository` instead of `IdentityDbContext` in all route handlers, calling the new repository methods. Authorization checks, event publishing, file parsing, and response formatting **stay in the endpoints for now** — only the EF Core parts move. This is intentionally a temporary shape; Phase 3 moves the rest.

## Quality and Testing State

- Quality: approved — `plans/identity-layered-refactor/quality/phase-02-repository-quality-report.json`, receipt issued (first pass flagged `ListAsync`'s `.AsNoTracking()` against this phase file's own self-contradictory Steps text; corrected on re-verify — `ListAsync` has exactly one, read-only call site, so `.AsNoTracking()` is correct and matches original behavior exactly)
- Testing: manual only (no automated test project). `dotnet build` passed 0/0; `grep IdentityDbContext Endpoints/` returns zero matches. Live endpoint smoke test deferred to Phase 6.

## Manual Verification

1. `dotnet build` — zero errors.
2. `grep -rn "IdentityDbContext" Endpoints/` returns zero matches (confirms endpoints no longer touch DbContext directly).
3. `grep -rn "using.*Data;" Endpoints/` returns zero matches (confirms no stray namespace imports).
4. Manually exercise all 6 routes end-to-end (via Swagger/Postman against a local run):
   - `POST /auth/register` with valid email/password/classId — confirm user created, `UserRegistered` event published, response includes userId.
   - `POST /auth/register` with duplicate email — confirm `409 Conflict` with same message as before.
   - `POST /auth/login` with valid credentials — confirm JWT token returned with correct userId/email/role.
   - `POST /auth/login` with invalid password — confirm `401 Unauthorized`.
   - `POST /auth/google` with valid Google token — confirm JWT token returned; confirm user created if new; confirm `GoogleSubjectId` linked if existing email.
   - `GET /users` as admin — confirm full list returned.
   - `GET /users?ids=...` — confirm filter works.
   - `GET /users` as lecturer — confirm list filtered to authorized students (class lecturers and graded students).
   - `PATCH /users/{userId}` as lecturer updating their own class student — confirm `StudentCode`/`ClassId` updated, response includes className.
   - `PATCH /users/{userId}` as lecturer updating unauthorized student — confirm `403 Forbid`.
   - `PATCH /users/{userId}` with concurrent modification (simulate with two overlapping requests) — confirm `409 Conflict` with same message as before.
   - `POST /users/bulk-import` with valid roster file — confirm accepted rows updated atomically, skipped rows reported with reason, `RosterImportReport` structure unchanged.
   - `POST /users/bulk-import` with invalid file (missing column) — confirm parse error returned.
