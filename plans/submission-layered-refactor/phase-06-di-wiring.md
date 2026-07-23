# Phase 6: DI wiring + full regression pass

**Maps to:** all P1/P2 stories — this phase closes out the refactor and re-verifies the whole spec's Success Criteria at once.

## Goal

Register every new interface/implementation in `Program.cs` via clear extension methods, remove now-dead registrations, and run one final full manual pass against every Success Criterion in `spec.md`.

## Steps

1. Add extension methods (in a new `Extensions/SubmissionServiceCollectionExtensions.cs` or directly in `Program.cs` if the team prefers — keep consistent with how `AddEventBus`/`AddObjectStorage` from `AutoGrading.Common` are already called):
   - `AddSubmissionRepository(this IServiceCollection services)` → `services.AddScoped<ISubmissionRepository, SubmissionRepository>()`
   - `AddSubmissionApplication(this IServiceCollection services)` → `services.AddScoped<ISubmissionService, SubmissionService>()`

2. Update `Program.cs`:
   - Keep `AddDbContext<SubmissionDbContext>(...)` (now pointing at `Repository/SubmissionDbContext.cs`, same registration, just moved namespace).
   - Keep `AddHttpClient<ICatalogApiClient, CatalogApiClient>(...)` — same registration, interface now resolved from `Interfaces/`.
   - Keep `AddScoped<IArtifactParser, ArtifactParser>()` — same registration, interface now resolved from `Interfaces/`.
   - Add `builder.Services.AddSubmissionRepository().AddSubmissionApplication();`
   - Confirm `AddScoped<ExtractionJob>()` and `AddScoped<SubmissionUploadedHandler>()` still resolve (their constructors changed in Phase 5 but DI just needs the new dependencies registered, which they already are).

3. Delete the old `Data/` folder once `Repository/SubmissionDbContext.cs` is confirmed to be the only copy (should already be empty after Phase 2's move — this step is just cleanup/verification that nothing still points at `Data.SubmissionDbContext`).

## Design Constraints

- No new package dependencies — everything here is existing ASP.NET Core DI conventions (`IServiceCollection` extension methods), consistent with how `AutoGrading.Common` already registers cross-cutting services.
- `grep -rn "AutoGrading.SubmissionSvc.Api.Data"` across the whole project should return zero matches once this phase is done (confirms the `Data/` → `Repository/` move is fully clean, no stale namespace references).

Preflight: `Data/` folder was already removed in Phase 2 (empty dir, `git mv`'d clean) — this phase's cleanup step is a no-op verification, not new work. Extension methods placed in a new `Extensions/SubmissionServiceCollectionExtensions.cs`, mirroring the style of `AutoGrading.Common.Extensions.ServiceCollectionExtensions` (static class, `this IServiceCollection services` extension methods returning `services` for chaining, one XML summary line per method). `Extensions/` is not one of spec's originally-listed folders but is explicitly called for by this phase's own Step 1, so it's plan-sanctioned scope, not creep.

## Quality and Testing State

- Quality: approved — `plans/submission-layered-refactor/quality/phase-06-di-wiring-quality-report.json`, receipt issued (report was regenerated to match the schema; the reviewer's original content/verdict was preserved verbatim)
- Testing: manual only. `dotnet build` on the full solution passed 0 errors (2 pre-existing unrelated NU1504 warnings in Catalog.Api). Folder structure, dependency-direction, and no-stray-`Data`-namespace checks all passed. Full live end-to-end regression pass (upload→list→get→retry→extraction, Swagger diff) deferred to the user per the Phase 1 decision to skip live smoke tests throughout this session.

## Manual Verification — full spec Success Criteria re-check

1. `grep -rn "SubmissionDbContext" Endpoints/ Service/` → zero matches (repository is the only place touching it).
2. `grep -rn "AutoGrading.SubmissionSvc.Api.Data"` project-wide → zero matches.
3. Folder structure matches spec exactly: `Constant/ Endpoints/ Domain/ Dto/ Interfaces/ Repository/ Service/` present, `Clients/ Parsing/ Jobs/ Migrations/` still in place.
4. `dotnet build` on the full solution (`AutoGrading.sln`) — zero errors, confirms no other service/reference broke.
5. Full end-to-end pass one more time: upload → list → get → retry → extraction (Hangfire) → RabbitMQ consumer, for student/lecturer/admin roles, including the attempt-limit/attempt-conflict/storage-rollback edge cases exercised in earlier phases.
6. Compare `swagger.json` for the Submission service before the whole refactor (git stash/checkout the pre-refactor commit if needed) vs. after — routes, status codes, and schemas unchanged.
