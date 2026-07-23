# Plan: Tách logic khỏi Endpoint — Layered Architecture cho Submission

**Spec:** [spec.md](./spec.md)
**Date:** 2026-07-23
**Mode:** Hard
**Test flag:** default (no `--tdd` — no automated test project for Submission this round; verification is manual per phase)
**Status:** Ready

---

## Scope

Refactor `AutoGrading.Submission.Api` (single-project Minimal API, no changes to other services) from "all logic in `Endpoints/SubmissionsEndpoints.cs`" into a Layered Architecture within the same project:
`Constant/ Endpoints/ Domain/ Dto/ Interfaces/ Repository/ Service/` + existing `Clients/ Parsing/ Jobs/ Migrations/` kept in place, now implementing the new interfaces.

Behavior-preserving refactor — no API contract change, no new business rules.

## Phases

1. [phase-01-constants-and-interfaces.md](./phase-01-constants-and-interfaces.md) — additive only, zero behavior change
2. [phase-02-repository.md](./phase-02-repository.md) — move EF Core access behind `ISubmissionRepository`
3. [phase-03-service.md](./phase-03-service.md) — move business logic into `SubmissionService`
4. [phase-04-slim-endpoints-and-dto.md](./phase-04-slim-endpoints-and-dto.md) — endpoints become bind → call service → map response
5. [phase-05-update-jobs.md](./phase-05-update-jobs.md) — `ExtractionJob` depends on `ISubmissionRepository`, not `SubmissionDbContext`
6. [phase-06-di-wiring.md](./phase-06-di-wiring.md) — wire everything in `Program.cs`, full manual regression pass

Order matters: each phase depends on the previous one compiling and working. No phase moves to the next until its Manual Verification passes.

## Real files involved (already inspected, not guessed)

- `Endpoints/SubmissionsEndpoints.cs` (209 lines) — 4 routes: `GET /`, `GET /{id}`, `POST /upload`, `POST /{id}/retry`, plus private helper `GetLecturerAllowedStudentIdsAsync` and the `UploadSubmissionForm` binding type at the bottom of the file.
- `Domain/Submission.cs` — plain EF entity, public settable properties, `Artifacts` navigation list. This is currently serialized directly as the HTTP response body (no DTO today) — response DTOs introduced in phase 4 must mirror this shape exactly.
- `Data/SubmissionDbContext.cs` → moves to `Repository/`.
- `Clients/CatalogApiClient.cs` — already exposes `ICatalogApiClient`; only the interface declaration moves to `Interfaces/`, implementation stays in `Clients/`.
- `Parsing/IArtifactParser.cs` — already an interface, dispatched to by `ArtifactParser` (registered in DI) which internally uses `DocxReportParser`/`DrawioDiagramParser`. Interface moves to `Interfaces/`, implementations stay in `Parsing/`.
- `Jobs/ExtractionJob.cs` — Hangfire job, currently injects `SubmissionDbContext` directly alongside `IObjectStorage`, `IArtifactParser`, `IEventBus` (both already interfaces).
- `Jobs/SubmissionUploadedHandler.cs` — RabbitMQ consumer (`IIntegrationEventHandler<SubmissionUploaded>`), already thin (only enqueues the Hangfire job) — **no change needed**, confirmed by reading the file.
- `Program.cs` — current DI: `AddDbContext<SubmissionDbContext>`, `AddHttpClient<ICatalogApiClient, CatalogApiClient>`, `AddScoped<IArtifactParser, ArtifactParser>`, `AddScoped<ExtractionJob>`, `AddScoped<SubmissionUploadedHandler>`, etc.

## Deliberate deviation from the spec's illustrative example

Spec FR-01 mentions "e.g. `SubmissionService`, `ExtractionOrchestrator`" as example Service class names. Since `Jobs/ExtractionJob.cs` **already is** a well-scoped orchestrator (download → parse → save artifacts → publish events) and `Jobs/SubmissionUploadedHandler.cs` is already a one-line pass-through, adding a separate `Service/ExtractionOrchestrator.cs` wrapping `ExtractionJob` would be a redundant layer with no behavior it needs to hide. Phase 5 instead updates `ExtractionJob` to depend on `ISubmissionRepository` (interface) instead of `SubmissionDbContext` (concrete class) directly — same effect (extraction logic decoupled from EF Core specifics), no extra class. This keeps scope to what FR-01..FR-08 actually require without inventing an abstraction nothing calls into.

## Risks

- **No compiler-enforced boundary** (per brainstorm): a future edit could reintroduce `SubmissionDbContext` into `Endpoints/` or `Service/` without any build error — only code review catches it. Out of scope to fix here (see spec's Out of Scope).
- **API contract drift via new DTOs** (phase 4): introducing `SubmissionResponse` instead of serializing the EF entity directly risks silently changing the JSON shape (property casing, `Artifacts` shape) if the DTO doesn't mirror the entity exactly. Mitigated by comparing `GET /submissions/{id}` response body before/after refactor byte-for-byte (ignoring key order) as part of Phase 4 Manual Verification.
- **Serializable transaction split across layers** (phase 2/3): `UploadSubmissionAsync`'s attempt-limit-check-then-insert must stay inside a single `IsolationLevel.Serializable` transaction. If the transaction boundary gets split between `Repository` and `Service`, the concurrency guarantee silently breaks. Mitigated by keeping the entire transaction inside one repository method (`CreateWithAttemptCheckAsync`), never opened/committed from `Service` or `Endpoints`.
- **Manual-only verification**: no automated regression net. Each phase's Manual Verification section is mandatory before proceeding — do not batch multiple phases' verification into one pass at the end. One person runs each phase's checklist before moving to the next; a phase isn't "done" until every item in its Manual Verification section passes, not just "compiles".
- **[NOTED — plan-reviewer, resolved]** Phase 2 moves `SubmissionDbContext` before Phase 5 decouples `ExtractionJob` from it. Confirmed with user: all 6 phases run continuously in one session/branch, git merge/deploy handled separately by the user — not a staged production rollout. No mitigation needed for this pilot; revisit only if the same pattern is later applied to Grading/Catalog under a real CI/CD staged-release process.
- **[NOTED — plan-reviewer]** The `IsolationLevel.Serializable` transaction boundary (Phase 2) has no compiler enforcement — only the inline warning comment and code review catch a future accidental split. Accepted as a known limitation of Layered Architecture (vs. physical Clean Architecture), consistent with the brainstorm's tradeoff analysis.
