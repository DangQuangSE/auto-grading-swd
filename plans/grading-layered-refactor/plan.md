# Plan: Tách logic khỏi Endpoint — Layered Architecture cho Grading

**Date:** 2026-07-23
**Mode:** Hard
**Test flag:** default (no `--tdd` — no automated test project for Grading; verification is manual per phase)
**Status:** 🟢 Done

---

## Scope

Refactor `AutoGrading.Grading.Api` (single-project Minimal API, no changes to other services) from "all logic in `Endpoints/GradesEndpoints.cs`" into a Layered Architecture within the same project:
`Constant/ Endpoints/ Domain/ Dto/ Interfaces/ Repository/ Service/` + existing `Clients/ Jobs/ Migrations/ Handlers/` kept in place, now implementing the new interfaces.

Behavior-preserving refactor — no API contract change, no new business rules.

## Phases

- [x] Phase 1: [phase-01-constants-and-interfaces.md](./phase-01-constants-and-interfaces.md) — additive only, zero behavior change. Quality: approved. Testing: manual (build clean).
- [x] Phase 2: [phase-02-repository.md](./phase-02-repository.md) — move EF Core access behind `IGradingRepository`. Quality: approved (corrected on re-verify). Testing: manual (build clean, grep-verified).
- [x] Phase 3: [phase-03-service.md](./phase-03-service.md) — move authorization decisions and business logic into `GradingService`. Quality: approved. Testing: manual (build clean, ASP.NET-free Service/ confirmed).
- [x] Phase 4: [phase-04-slim-endpoints-and-dto.md](./phase-04-slim-endpoints-and-dto.md) — endpoints become pure HTTP adapters (bind → call service → map result). Quality: approved. Testing: manual (build clean, DTO mirroring verified, FinalGrade split corrected).
- [x] Phase 5: [phase-05-update-jobs.md](./phase-05-update-jobs.md) — `AiGradingJob` and `GradePublishedOutboxDispatcher` depend on `IGradingRepository`, not `GradingDbContext`. Quality: approved. Testing: manual (build clean, no EF Core in Jobs/).
- [x] Phase 6: [phase-06-di-wiring.md](./phase-06-di-wiring.md) — wire everything in `Program.cs`, full manual regression pass. Quality: approved. Testing: manual (full-solution build clean, folder structure + dependency checks passed; live E2E regression left for user).

## Session Notes
<!-- Updated by cook automatically — do not edit manually -->

**Last active:** 2026-07-23
**Phase in progress:** none — all 6 phases complete
**Status:** Plan complete. All 6 phases implemented, quality-gate APPROVED, full-solution build clean. Nothing committed — standing instruction is no auto-commit; changes are in the working tree for manual review.

### Decisions made this session
- Two red-team-flagged "CRITICAL" findings during planning were traced to actual source and rejected: adding `IsolationLevel.Serializable` to `PublishAsync` (current code uses default `ReadCommitted`; upgrading it would be an undocumented behavior change, not a structural refactor) and restricting `AiGradingRun.Scores` out of the student-facing `/result` DTO (the code's own comment — "Student-safe projection: only the run selected by a publication is returned" — confirms this is deliberate reveal-after-publish design, not a leak). Both confirmed with the user before locking the plan.
- Phase 2's `ck:quality --gate` run initially mis-scoped: since nothing is committed between phases, its `git diff`-based scoping conflated Phase 1's already-approved `Interfaces/`/`Constant/` folders with Phase 2's actual diff, producing 3 false CHANGES_REQUIRED findings. Corrected by re-invoking the gate with an explicit cross-reference to Phase 1's own receipt; re-verified APPROVED.
- Phase 6's quality report needed to be regenerated once (Submission's session hit the same schema-mismatch issue) — the fix is always to match `report-schema.json`'s exact top-level shape (`mode`/`target`/`reviewed_at`/...), not to alter the review's actual findings/verdict.
- `/simplify` passes each phase caught a few real, in-scope issues (a genuine over-fetch in `GetPublishedResultAsync`, a missing `FromData` mapper for consistency) and correctly rejected several false positives (cross-service duplication with the already-shipped Submission service, which is out of this plan's authority to touch).

## Research Summary

This plan is a direct adaptation of the completed `AutoGrading.Submission.Api` refactor (plans/submission-layered-refactor/), applying the same 6-phase structure, patterns, and conventions:
- One repository per bounded context (despite multiple aggregates: `AiGradingRun`+`Scores`; `FinalGrade`+`GradePublication`+`GradePublishedOutbox`).
- No separate authorization service — helpers move into `Service` as private methods (mirrors `Submission`'s `GetLecturerAllowedStudentIdsAsync`).
- Exception-based signaling from repository/service layers, mapped to `IResult` at the endpoint boundary.
- DTOs with `FromDomain(...)` mappers, mirroring domain entities 1:1.
- Primary-constructor DI, extension-method `AddXRepository()`/`AddXApplication()` in `Program.cs`.
- Atomic 3-table write ownership (for `PublishAsync`) stays entirely inside repository, never split across layers (same *pattern* as Submission's `CreateWithAttemptCheckAsync` — but Grading's current code uses plain `BeginTransactionAsync(ct)` at default `ReadCommitted` isolation, not `Serializable`; this refactor preserves that exact isolation level, it does not import Submission's `Serializable` choice, which was solving a different problem — an attempt-limit race — that doesn't exist here).

Grading has the same "no automated test net" situation Submission had — `AutoGrading.Grading.Api.Tests/` exists as a directory but contains no `.csproj` or source files (confirmed empty, stale `bin`/`obj` only) — so this plan follows the exact manual-verification precedent already proven safe in Submission's full 6-phase refactor (completed 2026-07-23, all phases approved, full-solution build clean).

## Real files involved (already inspected, not guessed)

- `Endpoints/GradesEndpoints.cs` (286 lines):
  - 7 route handlers: `GetRunsAsync` (lines 45–54), `GetPublishedResultAsync` (56–86), `GetFinalGradeAsync` (88–102), `GetFinalGradesBatchAsync` (128–147), `RegradeAsync` (191–200), `PublishGradeAsync` (202–219), `PublishAllAsync` (221–259).
  - Helper methods: `IsLecturerAllowedAsync` (114–126), `CanReadSubmissionAsync` (104–112), `FilterAllowedForLecturerAsync` (152–181), `ParseIds` (183–189), `FindPublishedGradeAsync` (261–265), `PublishOneAsync` (267–279).
  - DTOs at bottom (lines 282–286): `RegradeRequest`, `PublishGradeRequest`, `FinalGradeResponse`, `PublishedGradeResult`, `PublishAllResponse`.

- `Domain/` — 8 entity classes: `AiGradingRun`, `AiCriterionScore`, `FinalGrade`, `GradePublication`, `GradePublishedOutbox`, `AiGradingRunStatus` (enum), `LocalRubric`, `LocalRubricCriterion`.

- `Data/GradingDbContext.cs` — single DbContext, will move to `Repository/`.

- `Clients/CatalogApiClient.cs` — interface `ICatalogApiClient` (already declared alongside impl); will move interface to `Interfaces/`.
  `Clients/SubmissionApiClient.cs` — interface `ISubmissionApiClient` (already declared alongside impl); will move interface to `Interfaces/`.

- `Jobs/AiGradingJob.cs` — Hangfire job, currently injects `GradingDbContext` directly (lines 18, 34–35, 74–86, 90). Will decouple to `IGradingRepository` in Phase 5.
  `Jobs/GradePublishedOutboxDispatcher.cs` — background service, currently injects `GradingDbContext` directly (line 17, 19). Will decouple to `IGradingRepository` in Phase 5.
  `Jobs/ArtifactsExtractedHandler.cs` — RabbitMQ consumer, already thin (confirmed by reading, only enqueues `AiGradingJob`). No change needed.

- `Handlers/RubricConfirmedHandler.cs` — RabbitMQ consumer for `LocalRubric`/`LocalRubricCriterion` events, currently injects `GradingDbContext` (line 13). **OUT OF SCOPE — see "Deliberate deviation" below.**

- `Program.cs` — current DI: `AddDbContext<GradingDbContext>`, `AddHttpClient<ICatalogApiClient, CatalogApiClient>`, `AddHttpClient<ISubmissionApiClient, SubmissionApiClient>`, `AddScoped<AiGradingJob>`, `AddScoped<ArtifactsExtractedHandler>`, `AddScoped<RubricConfirmedHandler>`, `AddHostedService<GradePublishedOutboxDispatcher>`.

## Deliberate deviation from generic Layered Architecture

### 1. No separate `IAuthorizationService`

The existing authorization checks (`IsLecturerAllowedAsync`, `CanReadSubmissionAsync`, `FilterAllowedForLecturerAsync`) are private helper methods (lines 104–181 in current endpoint). Per Phase 3, these move into `GradingService` as private methods, not factored into a separate service class. Reasoning: they're only used by this one service's endpoint handlers — no other part of Grading or any other service calls them. Creating an `IAuthorizationService` would be a premature abstraction nothing else needs. This mirrors how `Submission` handled `GetLecturerAllowedStudentIdsAsync` (moved into `SubmissionService` as a private helper, not extracted as `IAuthorizationService`).

### 2. `AiGradingJob` and `GradePublishedOutboxDispatcher` explicitly included in Phase 5

Both currently inject `GradingDbContext` directly and will be updated to depend on `IGradingRepository` instead. This is not a separate phase after Phase 6 — it's Phase 5's own work, same as how Submission's `ExtractionJob` was decoupled in Phase 5. Do not scope these out.

### 3. `RubricConfirmedHandler` is deliberately out of scope

This RabbitMQ consumer (lines 1–61 in Handlers/) operates on `LocalRubric`/`LocalRubricCriterion`, a separate aggregate that the endpoint (`GradesEndpoints.cs`) never touches at all. The endpoints only interact with `AiGradingRun`, `AiCriterionScore`, `FinalGrade`, `GradePublication`, and `GradePublishedOutbox`. Because `RubricConfirmedHandler` belongs to a different bounded context (the rubric confirmation flow), it will keep injecting `GradingDbContext` directly, unchanged, without participating in this refactor. This is a documented, plan-sanctioned exception, not scope creep or an oversight — it prevents "pulling in" a whole separate domain concept to hit an artificial completeness metric.

## Dependencies

External:
- Hangfire (background job framework) — already used, no new version/setup required.
- RabbitMQ (event bus) — already used, no new version/setup required.
- HttpClient for Catalog/Submission APIs — already used, no new version/setup required.

Blocked tasks:
- None — this is a pure refactoring, all code is already present and functional.

## Risks

- **Atomic 3-table write split across layers, or dirty `ChangeTracker` after a failed publish** (Phase 2/3): `PublishAsync`'s creation of `FinalGrade` + `GradePublication` + `GradePublishedOutbox` (lines 271–278 in current endpoint) must stay entirely inside one repository method — if split between Repository and Service, or committed from Endpoints, the atomicity silently breaks. A second, less obvious risk: the current code's `PublishAllAsync` batch loop calls `db.ChangeTracker.Clear()` after each failed `PublishOneAsync` so the next iteration's `SaveChangesAsync` doesn't also try to persist the previous failure's still-tracked entities — once `Service` no longer touches `DbContext` directly, this cleanup must move *inside* `PublishAsync` itself (`catch { db.ChangeTracker.Clear(); throw; }`), or a multi-run `PublishAllAsync` batch will corrupt itself after the first failure. Mitigated: Phase 2 makes `PublishAsync` self-cleaning on failure; Phase 3's `PublishAllAsync` loop just catches and counts, no EF-Core-specific knowledge needed.

- **`IsolationLevel.Serializable` must NOT be added** (Phase 2) — an earlier draft of this plan incorrectly copied Submission's `Serializable` isolation level onto `PublishAsync` by pattern-matching the template too literally. The current code uses plain `BeginTransactionAsync(ct)` (default `ReadCommitted`); this refactor is behavior-preserving, so it keeps that exact isolation level. Adding `Serializable` would be an undocumented concurrency-behavior change (could introduce new serialization failures/retries under load that don't happen today) smuggled into a structural refactor — corrected during red-team review, verified against actual source line 271.

- **AI-visibility DTO risk — verified NOT a bug, do not "fix" it** (Phase 4): line 19's comment ("AI output is review material. It is never exposed directly to students") describes the `/runs` route only. The `/result` route's own comment (line 23, "Student-safe projection: only the run selected by a publication is returned") documents that a lecturer-published run's full detail is *meant* to reach the student as their official feedback. An earlier red-team pass flagged this as a leak requiring a DTO restriction; verified against the actual code and rejected — restricting it would be an undocumented behavior change to a security-relevant endpoint, which this refactor must not introduce. Phase 4 preserves the exact current shape for all roles, verified byte-for-byte.

- **Manual-only verification** (all phases): no automated test project for Grading (`AutoGrading.Grading.Api.Tests/` confirmed empty — no `.csproj`, no source files). Each phase's Manual Verification checklist is mandatory before proceeding — do not batch multiple phases' verification into one pass at the end. A phase isn't "done" until every item in its Manual Verification section passes.

- **No compiler-enforced boundary** (per Submission's plan): a future edit could reintroduce `GradingDbContext` into `Endpoints/` or `Service/` without any build error — only code review catches it. Out of scope to fix (same limitation Submission accepted).

## Red-Team Review Notes

`plan-reviewer` ran against this plan (see conversation for full report). Verdict: WARN, 8 findings. Adjudication:
- 2 findings the reviewer marked CRITICAL were themselves based on a misreading of the current code (see the two Risks entries above starting "must NOT be added" and "verified NOT a bug") — REJECTED as originally proposed, but used to drive corrections that keep behavior exactly as-is rather than the reviewer's suggested fixes (which would have introduced real behavior changes).
- 1 HIGH finding (Phase 5 listed a bogus `GetForExtractionAsync(Guid): Task<Submission?>` method — wrong service's domain type — plus a `UpdateRunStateAsync`/`UpdateRunStatusAsync` naming mismatch) — ACCEPTED, Phase 5 corrected to reuse Phase 1's already-defined methods, no new ones added.
- 1 HIGH finding (`PublishAllAsync`'s per-run failure isolation underspecified once Service has no DbContext access) — ACCEPTED, resolved via the `ChangeTracker.Clear()`-moves-into-`PublishAsync` fix above (a different, more architecturally consistent fix than what the reviewer proposed).
- 1 HIGH finding (RubricConfirmedHandler scope assumption should be verified, not just asserted) — ACCEPTED, added a grep-based Manual Verification step to Phase 2.
- 1 MEDIUM finding (`PublishGradeAsync` idempotency should be explicit) — ACCEPTED, Phase 3 now states it explicitly.
- 2 MEDIUM findings (Constants scaffolding ambiguity, `Skipped`/`Published`/`Failed` count semantics) — NOTED only, low-risk wording clarifications already resolved inline in Phase 1/3 where relevant.
