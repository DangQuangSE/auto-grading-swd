# Phase 6: DI wiring + full regression pass

**Maps to:** Plan stories — this phase closes out the refactor and re-verifies the whole plan at once.

## Requirements

Register every new interface/implementation in `Program.cs` via clear extension methods, remove now-dead registrations, and run one final full manual pass against every success criterion in the plan.

## Design Constraints

- No new package dependencies — everything here is existing ASP.NET Core DI conventions (`IServiceCollection` extension methods), consistent with how `AutoGrading.Common` already registers cross-cutting services.
- `grep -rn "AutoGrading.Grading.Api.Data"` across the whole project should return zero matches once this phase is done (confirms the `Data/` → `Repository/` move is fully clean).

Preflight: `Data/` folder was already removed in Phase 2 (confirmed gone). Extension methods placed in `Extensions/GradingServiceCollectionExtensions.cs`, mirroring `AutoGrading.Common.Extensions.ServiceCollectionExtensions` and Submission's own `Extensions/SubmissionServiceCollectionExtensions.cs`. Also corrected this phase's own Manual Verification step (previously said the published-result endpoint returns "no raw scores exposed" to the student — that contradicted the AI-visibility design decision confirmed during planning; corrected to state the published run's full `Scores` legitimately appear, by design).

## Steps

1. Create `Extensions/GradingServiceCollectionExtensions.cs` with extension methods:
   ```csharp
   public static IServiceCollection AddGradingRepository(this IServiceCollection services) =>
       services.AddScoped<IGradingRepository, GradingRepository>();
   
   public static IServiceCollection AddGradingApplication(this IServiceCollection services) =>
       services.AddScoped<IGradingService, GradingService>();
   ```

2. Update `Program.cs`:
   - Keep `AddDbContext<GradingDbContext>(...)` (now pointing at `Repository/GradingDbContext.cs` namespace, same registration, just moved namespace).
   - Keep `AddHttpClient<ICatalogApiClient, CatalogApiClient>(...)` — same registration, interface now resolved from `Interfaces/`.
   - Keep `AddHttpClient<ISubmissionApiClient, SubmissionApiClient>(...)` — same registration, interface now resolved from `Interfaces/`.
   - Add `builder.Services.AddGradingRepository().AddGradingApplication();` (or inline the calls if the team prefers).
   - Confirm `AddScoped<AiGradingJob>()` still resolves (its constructor changed in Phase 5 but DI just needs the new dependencies registered, which they already are via `AddGradingRepository`).
   - Confirm `AddScoped<ArtifactsExtractedHandler>()` still resolves (no changes to this handler).
   - Confirm `AddScoped<RubricConfirmedHandler>()` still resolves (no changes to this handler).
   - Confirm `AddHostedService<GradePublishedOutboxDispatcher>()` still resolves (its constructor now gets `IGradingRepository` from the scope).

3. Verify and clean:
   - Delete the old `Data/` folder once `Repository/GradingDbContext.cs` is confirmed to be the only copy.
   - Grep project-wide for stray `AutoGrading.Grading.Api.Data` namespace references — should be zero after Phase 2's move was complete.

## Success Criteria

- `dotnet build` on the full solution (`AutoGrading.sln`) compiles with zero errors.
- `grep -rn "AutoGrading.Grading.Api.Data"` across the whole project returns zero matches.
- All dependencies resolve correctly at runtime (no DI composition errors).
- All 7 routes work identically to pre-refactor behavior.

## Quality and Testing State

- Quality: approved — `plans/grading-layered-refactor/quality/phase-06-di-wiring-quality-report.json`, receipt issued
- Testing: manual only. Full-solution build passed 0 errors (2 pre-existing unrelated NU1504 warnings in Catalog.Api). Folder structure, dependency-direction, no-stray-`Data`-namespace, and `GradingConstants` usage checks all passed. Live end-to-end regression (upload→regrade→publish→outbox-dispatch, Swagger diff) left for the user, consistent with the Submission service's precedent.

## Manual Verification — full plan Success Criteria re-check

1. `grep -rn "GradingDbContext" Endpoints/ Service/` → zero matches (repository is the only place touching it).
2. `grep -rn "AutoGrading.Grading.Api.Data"` project-wide → zero matches.
3. Folder structure matches plan exactly: `Constant/ Endpoints/ Domain/ Dto/ Interfaces/ Repository/ Service/` present, `Clients/ Jobs/ Migrations/ Handlers/` still in place.
4. `dotnet build` on the full solution (`AutoGrading.sln`) — zero errors, confirms no other service/reference broke.
5. Full end-to-end pass:
   - Create a submission (via Submission service, out of scope here but prerequisite).
   - `GET /grades/{submissionId}/runs` as lecturer — returns empty array (no runs yet).
   - `POST /grades/{submissionId}/regrade` — enqueues `AiGradingJob`, returns `202 Accepted`.
   - Wait for Hangfire job to complete (watch dashboard or logs).
   - `GET /grades/{submissionId}/result` as student — returns `404` with `{"gradingDone": true}` (grading done, awaiting lecturer to publish).
   - `GET /grades/{submissionId}/runs` as lecturer — returns one completed run with scores.
   - `POST /grades/{submissionId}/publish` as lecturer with `GradingRunId` from the run — returns `201 Created` with published grade.
   - `GET /grades/{submissionId}/result` as student — returns the published grade **including** the published run's full `Scores` (Evidence/Comment) — this is the deliberate reveal-after-publish design confirmed in Phase 4's Design Constraints, not a leak to guard against.
   - `GET /grades/{submissionId}/final` as student — returns published final grade.
   - `POST /grades/{submissionId}/publish` again as lecturer — returns `200 OK` (idempotent).
   - `POST /grades/final?submissionIds=...` as lecturer with batch of submission ids — returns batch of final grades.
   - Verify `GradePublishedOutboxDispatcher` poll ran and published the `GradePublished` event (check RabbitMQ or event logs).
6. Authorization edge cases:
   - Student accessing submission of another student: `403 Forbidden`.
   - Lecturer accessing submission outside enrolled students: `403 Forbidden`.
   - Lecturer accessing submission within enrolled students: `200 OK`.
   - Admin accessing any submission: `200 OK`.
7. Compare `swagger.json` generated before the whole refactor (if available from git history) vs. after — routes, status codes, and schemas unchanged.
8. Confirm `Constant/GradingConstants.cs` is only referenced in the codebase (grep `-rn "GradingConstants"`), and all error strings are centralized.
