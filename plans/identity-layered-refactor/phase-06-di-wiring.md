# Phase 6: DI wiring + full regression pass

## Requirements

Register every new interface/implementation in `Program.cs` via clear extension methods, remove now-dead registrations, and run one final full manual pass against every route and integration point.

## Design Constraints

- No new package dependencies — everything here is existing ASP.NET Core DI conventions (`IServiceCollection` extension methods), consistent with how `AutoGrading.Common` already registers cross-cutting services.
- `grep -rn "AutoGrading.Identity.Api.Data"` project-wide should return zero matches once this phase is done (confirms the `Data/` → `Repository/` move is fully clean).

Preflight: `Data/` folder was already removed in Phase 2 (only its files moved to `Repository/`); this phase's cleanup is just confirming no stray namespace references remain — confirmed gone, zero `AutoGrading.Identity.Api.Data` references project-wide. Extension methods placed in `Extensions/IdentityServiceCollectionExtensions.cs`, mirroring `AutoGrading.Common.Extensions.ServiceCollectionExtensions` and both completed sibling services' own extension-method files.

## Steps

1. Add extension methods (in a new `Extensions/IdentityServiceCollectionExtensions.cs` or directly in `Program.cs`):
   ```
   static class IdentityServiceCollectionExtensions
   {
       public static IServiceCollection AddIdentityRepository(this IServiceCollection services)
           => services.AddScoped<IUserRepository, UserRepository>();
       
       public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
           => services.AddScoped<IAuthService, AuthService>()
               .AddScoped<IUserService, UserService>();
   }
   ```

2. Update `Program.cs`:
   - Keep `AddDbContext<IdentityDbContext>(...)` (now pointing at `Repository/IdentityDbContext.cs`, same registration, just moved namespace).
   - Keep existing registrations: `AddSingleton<IPasswordHasher<User>>`, `AddJwtTokenGenerator`, `AddJwtAuthentication`, `AddEventBus`, `Configure<GoogleAuthOptions>`.
   - Add `builder.Services.AddIdentityRepository().AddIdentityApplication();` (chained extension calls).
   - Keep handler registrations: `AddScoped<ClassLecturerAssignedHandler>`, `AddScoped<SubmissionUploadedHandler>`, `AddScoped<GradePublishedHandler>` — their constructors now ask for `IUserRepository` instead of `IdentityDbContext`, so DI will wire that automatically.
   - Confirm the `eventBus.Subscribe<...>()` calls at the bottom still work (they register the handlers as RabbitMQ consumers; the handlers' constructors change but their interface (`IIntegrationEventHandler<T>`) doesn't).

3. Delete the old `Data/` folder once `Repository/IdentityDbContext.cs` is confirmed to be the only copy (if it hasn't already been removed — Phase 2's file move may have left an empty `Data/` directory).

## Quality and Testing State

- Quality: approved — `plans/identity-layered-refactor/quality/phase-06-di-wiring-quality-report.json`, receipt issued
- Testing: manual only. Full-solution build passed 0 errors (2 pre-existing unrelated NU1504 warnings). Folder structure, dependency-direction, no-stray-`Data`-namespace, and security-critical-path checks all passed. Live end-to-end regression (register→login→Google OAuth, roster bulk-import, all 3 event handlers) left for the user, consistent with Submission's/Grading's precedent.

## Manual Verification — full spec Success Criteria re-check

1. `dotnet build` on the full solution (`AutoGrading.sln`) — zero errors (pre-existing NU1504 warnings in other services are OK, but Identity must contribute zero new errors).
2. `grep -rn "IdentityDbContext" Endpoints/ Service/` → zero matches (repository is the only place touching it).
3. `grep -rn "AutoGrading.Identity.Api.Data"` project-wide → zero matches (confirms no stray namespace references).
4. Folder structure matches spec exactly: `Constant/ Dto/ Endpoints/ Domain/ Handlers/ Interfaces/ Repository/ Service/` present; `Auth/ Migrations/ RosterImport/` still in place (or subsumed into `Dto/` for `BulkImportForm`, `Repositories/` for request forms — just ensure no files were accidentally deleted).
5. Full 6-route end-to-end pass, all roles (student/lecturer/admin):
   - `POST /auth/register` (valid, duplicate email, class not found) — same behavior as before.
   - `POST /auth/login` (valid, invalid email, invalid password) — same behavior.
   - `POST /auth/google` (valid Google token, invalid token, unverified email, non-education domain) — same behavior; JWT token identical to pre-refactor.
   - `GET /users` (empty ids, with ids, as admin, as lecturer) — same list/filter behavior.
   - `GET /users?ids=...` — filter works identically.
   - `PATCH /users/{userId}` (valid update, unauthorized, concurrent modification, class not found) — same behavior.
   - `POST /users/bulk-import` (valid file, invalid file, authorization denials, partial success) — same behavior; atomicity preserved (all-or-nothing commit).
6. Integration pass: trigger all 3 event handlers via their domain events (or real flows — register → `UserRegistered`, assignment → `ClassLecturerAssigned`, upload → `SubmissionUploaded`, grade publish → `GradePublished`). Confirm:
   - Handler idempotency works (redelivering same event doesn't duplicate/error).
   - Concurrent event delivery (two clients simultaneously hitting the handler) doesn't corrupt state.
   - Log messages match pre-refactor (debug logs for idempotent redeliveries, no error logs for expected concurrency).
7. Compare `swagger.json` (OpenAPI schema) for Identity service before Phase 1 started vs. after Phase 6 — routes, status codes, schemas, and tag organization unchanged.
8. **Docker regression (optional but recommended):** run the full docker-compose stack (Identity + Submission + Grading + Catalog + Gateway, with RabbitMQ), exercise the cross-service auth flow: register user, login, get JWT, upload submission as student, grade as lecturer, publish grade. Confirm end-to-end flow works and no warnings/errors in Identity service logs.
