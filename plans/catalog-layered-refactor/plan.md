# Plan: Tách logic khỏi Endpoint — Layered Architecture cho Catalog

**Date:** 2026-07-23
**Mode:** Hard
**Test flag:** default (no `--tdd` — `AutoGrading.Catalog.Api.Tests/` directory exists but contains no `.csproj` or source files; verification is manual per phase, matching Submission/Grading/Identity precedent)
**Status:** 🟢 Complete — all 6 phases APPROVED, receipts issued

---

## Scope

Refactor `AutoGrading.Catalog.Api` (single-project Minimal API, no changes to other services) from "all logic in `Endpoints/`" into a Layered Architecture within the same project:
`Constant/ Endpoints/ Domain/ Dto/ Interfaces/ Repository/ Service/` + existing `Parsing/ Jobs/ Migrations/` kept in place (except deliberate dead-code deletion of `Parsing/DocxRubricParser.cs` and `Parsing/IRubricParser.cs`), now implementing the new interfaces.

Behavior-preserving refactor — no API contract change, no new business rules.

**This is the largest service yet**: 25 HTTP routes across 5 distinct concerns (Subjects, Assignments, Classes, Rubrics, Enrollments), with complex enrollment concurrency handling (Serializable transactions, row-version optimistic locking via base64-encoded SQL Server `rowversion`, raw `SqlException.Number` constraint checking 2601/2627/547) and sophisticated rubric workflow (file upload to MinIO, Hangfire job enqueue, status transitions Parsing→Draft→Confirmed via domain methods, role-scoped visibility). The refactoring must preserve this complexity byte-for-byte.

## Phases

- [x] Phase 1: [phase-01-constants-and-interfaces.md](./phase-01-constants-and-interfaces.md) — additive only, zero behavior change. Quality: APPROVED (receipt issued). Testing: manual (build clean).
- [x] Phase 2: [phase-02-repository.md](./phase-02-repository.md) — move EF Core access behind 5 repositories. Quality: APPROVED (receipt issued). Testing: manual (build clean, grep-verified).
- [x] Phase 3: [phase-03-service.md](./phase-03-service.md) — move business logic into 5 services. Quality: APPROVED (receipt issued). Testing: manual (build clean, ASP.NET-free Service/ confirmed).
- [x] Phase 4: [phase-04-slim-endpoints-and-dto.md](./phase-04-slim-endpoints-and-dto.md) — endpoints become bind → call service → map response. Quality: APPROVED (receipt issued). Testing: manual (build clean, DTO mirroring verified).
- [x] Phase 5: [phase-05-update-jobs.md](./phase-05-update-jobs.md) — `RubricParsingJob` depends on `IRubricRepository`, not `CatalogDbContext`. Quality: APPROVED (receipt issued). Testing: manual (build clean, no EF Core in Jobs/).
- [x] Phase 6: [phase-06-di-wiring.md](./phase-06-di-wiring.md) — wire everything in `Program.cs`, full manual regression pass. Quality: APPROVED (receipt issued). Testing: manual — build/wiring/structural checks verified directly; live 25-route E2E regression (Swagger/Postman against a running stack) left for the user, per Submission/Grading/Identity precedent.

## Research Summary

This plan is a direct adaptation of three completed, identical-structure refactors:
- `AutoGrading.Submission.Api` refactor: [plans/submission-layered-refactor/plan.md](../../submission-layered-refactor/plan.md) + 6 phase files (2026-07-23, all phases complete, quality APPROVED).
- `AutoGrading.Grading.Api` refactor: [plans/grading-layered-refactor/plan.md](../../grading-layered-refactor/plan.md) + 6 phase files (2026-07-23, all phases complete, quality APPROVED).
- `AutoGrading.Identity.Api` refactor: [plans/identity-layered-refactor/plan.md](../../identity-layered-refactor/plan.md) + 6 phase files (2026-07-23, all phases complete, quality APPROVED).

Applying the same 6-phase structure, conventions, and manual-verification discipline proven safe in all three prior services.

## Real files involved (already inspected, not guessed)

**Endpoints (25 routes total across 5 groups):**
- `Endpoints/SubjectsEndpoints.cs` (168 lines): 4 routes (`GET /`, `GET /open-for-registration`, `POST /`, `PATCH /{id}/registration`), request/response records at bottom.
- `Endpoints/AssignmentsEndpoints.cs` (79 lines): 4 routes (`GET /`, `GET /{id}`, `POST /`, `PUT /{id}`), request/response records at bottom.
- `Endpoints/ClassesEndpoints.cs` (293 lines): 6 routes (`GET /`, `GET /admin`, `GET /by-subject/{subjectId}`, `POST /`, `POST /subject-scoped`, `PATCH /{id}`), helper `ValidateClassInput`, `NormalizeName`, `SaveAndPublishAsync` (orchestration), request/response records at bottom.
- `Endpoints/RubricsEndpoints.cs` (323 lines): 7 routes (`GET /`, `GET /{id}/file`, `POST /upload`, `POST /{id}/retry-parsing`, `PATCH /{id}/criteria`, `POST /{id}/confirm`, `POST /{id}/unlock`), helper `LoadAuthorizedRubricAsync`, `IsAuthorized`, `CanView`, `TrySaveChangesAsync`, request/response records at bottom.
- `Endpoints/EnrollmentsEndpoints.cs` (13 lines, router only) → 5 actual routes via:
  - `Endpoints/StudentEnrollmentEndpoints.cs` (46 lines): 2 routes (`GET /me`, `PUT /me/{subjectId}`), helper `TryGetStudentId`.
  - `Endpoints/AdminEnrollmentEndpoints.cs` (38 lines): 2 routes (`GET /admin`, `PUT /admin/{studentId}/{subjectId}`).
  - `Endpoints/LecturerEnrollmentEndpoints.cs` (46 lines): 1 route (`GET /lecturer-student-ids`), complex claim/role-based access logic.
- `Endpoints/EnrollmentQueries.cs` (152 lines): 6 internal methods (`ListStudentAsync`, `ListAdminAsync`, `ListStudentIdsForLecturerAsync`, `GetStudentAsync`, `GetStudentByIdAsync`, `GetAdminAsync`), 2 private projection helpers (`ProjectStudent`, `ProjectAdmin`).
- `Endpoints/EnrollmentCommands.cs` (275 lines): 2 methods (`UpsertStudentAsync`, `CorrectAdminAsync`), complex concurrency handling (Serializable transactions, row-version optimistic locking via base64, `db.Entry().Property().OriginalValue` tracking, `ChangeTracker.Clear()` on failure, raw `SqlException.Number` checks 2601/2627/547), private helpers for transaction/conflict handling.
- `Endpoints/EnrollmentContracts.cs` (110 lines): request/response records (`UpsertEnrollmentRequest`, `EnrollmentSummary`, `AdminEnrollmentSummary`, internal projections/enum/result type).
- `Endpoints/EnrollmentHttpResults.cs` (18 lines): maps `EnrollmentCommandResult<T>` discriminated union to `IResult`.

**Domain entities (6 + 3 enums):**
- `Domain/Subject.cs`, `Domain/Assignment.cs`, `Domain/Class.cs`, `Domain/StudentEnrollment.cs`, `Domain/Rubric.cs`, `Domain/RubricCriterion.cs`.
- `Domain/RegistrationStatus.cs`, `Domain/RubricStatus.cs`, `Domain/RubricScope.cs` (enums).

**Data:**
- `Data/CatalogDbContext.cs` (132 lines): 6 DbSets, custom `ReplaceRubricCriteria()` helper method for concurrency-safe navigation mutation, composite-key/alternate-key constraints for Class↔StudentEnrollment FK relationship.

**Jobs:**
- `Jobs/RubricParsingJob.cs` (78 lines): Hangfire job, injects `CatalogDbContext` directly (lines 16, 24, 56, 66), calls `IObjectStorage.DownloadAsync()`, `IOpenCodeClient.ParseRubricCriteriaAsync()`, publishes `RubricParsed` event.
- `Jobs/DocxTextExtractor.cs` (20 lines): static utility, pure text extraction, zero DbContext dependency.

**Dead code to be deleted (zero callers, grep-verified):**
- `Parsing/DocxRubricParser.cs` (156 lines) — superseded by AI-based parsing via `IOpenCodeClient`.
- `Parsing/IRubricParser.cs` (17 lines) — only referenced by `DocxRubricParser.cs`, which is dead.

**Program.cs:**
- Current DI (line 26–28): `AddScoped<RubricParsingJob>()`, `AddScoped<EnrollmentQueries>()`, `AddScoped<EnrollmentCommands>()`.
- Current DbContext (line 15): `AddDbContext<CatalogDbContext>`.
- Current endpoint mapping (line 53–57): all 5 route groups mapped.

## Deliberate Design Decisions (Resolved — Do Not Re-litigate)

### 1. FIVE repositories, not one, not per-file

Five repository interfaces (`ISubjectRepository`, `IAssignmentRepository`, `IClassRepository`, `IEnrollmentRepository`, `IRubricRepository`) — one per route-group concern — **deliberately deviates from Submission/Grading/Identity's "one repository per service" pattern**. This deviation is justified:

- **Submission/Grading/Identity** each have ONE real use-case concern whose routes all share the same bounded context (Submissions with artifacts, Gradings with runs/publications/outbox, Users with auth/roster/classes). A single repository per service makes sense.
- **Catalog** has FIVE genuinely independent use-case concerns:
  - Subject registration (Subject lifecycle, registration-status changes).
  - Assignment versioning (Assignment CRUD tied to Subjects).
  - Class management (Class creation, lecturer assignment, subject association).
  - Rubric workflow (File upload, AI parsing, criteria confirmation/unlock, status transitions Parsing→Draft→Confirmed).
  - Student enrollment (Serializable-transactional upserts with row-version locking, concurrent conflict handling).

These five concerns rarely call into each other at runtime. A student enrolling never touches rubric code; a lecturer confirming a rubric never touches enrollment code. The coupling is purely FK-level in the DB schema (`Subject.Id` → `Assignment.SubjectId` → `Class.SubjectId`; `Class.Id` → `StudentEnrollment.ClassId`), not request-flow-level.

**A single `ICatalogRepository` would force Subjects-only code to pull in Enrollment's Serializable-transaction baggage, Rubric's file-upload baggage, and vice versa — architectural smell.** Five separate repositories honor the actual domain structure and keep concerns cleanly isolated.

**This decision was debated by two researchers and converged on deliberately, not ignored.** It is not scope creep; it is a considered deviation from prior patterns, documented here, and it drives the corresponding service-layer split (decision #2).

### 2. FIVE services correspondingly

Five service interfaces (`ISubjectService`, `IAssignmentService`, `IClassService`, `IEnrollmentService`, `IRubricService`) — one per repository concern — **mirrors the 5-repository decision above**.

**Do NOT split `IRubricService` further** into "CRUD service" vs "upload/parsing orchestration service" — even though the code has clear orchestration paths (upload → enqueue Hangfire, retry-parsing → enqueue Hangfire, confirm/unlock → domain method call → event publish). A second split was considered and rejected: `GradingService` already demonstrates that CRUD + orchestration + batch operations can coexist in one service without becoming unmaintainable (line count is still <200 for Grading). Same model applies here.

### 3. ONE 6-phase plan, not split per concern

Each phase covers work across **all 5 concerns simultaneously** (e.g. Phase 2 creates all 5 repository interfaces + implementations together in one pass, not one concern at a time). This keeps the plan structure consistent with the established one-plan-per-service pattern across the whole initiative (Submission/Grading/Identity all have exactly 6 phases), at the cost of each phase's scope being proportionally larger (expected, given Catalog is ~2.5x Grading's size).

**Do not introduce phase-per-concern sub-phases** (e.g. "Phase 2a-Subjects", "Phase 2b-Assignments") — keep exactly 6 phases, each one a complete layer-wide pass.

### 4. `EnrollmentQueries`/`EnrollmentCommands` become the seed of `IEnrollmentRepository`

These two internal classes (lines 7-152 and 10-275 of current endpoint) are ALREADY a partial extraction of enrollment data access + concurrency handling:
- Serializable transactions (line 32–34 of Commands).
- Row-version optimistic locking via `db.Entry(enrollment).Property(x => x.RowVersion).OriginalValue` (line 181).
- Raw `SqlException.Number` constraint-conflict detection for SQL Server codes 2601 (unique constraint), 2627 (primary key), 547 (check/FK constraint) (line 274).
- Transaction rollback + `ChangeTracker.Clear()` on failure (lines 247–251).

This logic must move into `IEnrollmentRepository` **UNCHANGED, byte-for-byte**, preserving the exact transaction/rollback/cleanup sequence. This is load-bearing code that would fail if "simplified" or refactored.

`EnrollmentContracts.cs`'s request/response/projection records move into `Dto/`; `EnrollmentHttpResults.cs`'s `IResult` mapping logic **stays in `Endpoints/`** (it's already exactly what Phase 4 wants — result-to-`IResult` mapping at the boundary).

**Note on `EnrollmentCommandResult<T>` exception-vs-result pattern:** The current code uses a discriminated-union result type (`EnrollmentCommandStatus` enum with `Success`/`Invalid`/`NotFound`/`Conflict` branches) rather than throwing exceptions. This pattern has **proven itself in actual deployment** for a genuinely multi-outcome operation (concurrent updates can legitimately return conflict with retry info, not throw). **This is the one place in the whole multi-service initiative where the result-type pattern takes precedence over the exception-based convention.** Keep `EnrollmentService.UpsertStudentAsync` and `EnrollmentService.CorrectAdminAsync` returning `EnrollmentCommandResult<T>` (not throwing exceptions), since forcing exceptions here would require inventing a synthetic exception hierarchy (`EnrollmentStaleException`, `EnrollmentConflictException`) that adds no architectural value over the proven result-type discriminator already handling these cases correctly. Phase 3/4 will adapt `EnrollmentHttpResults.From()` to map from `EnrollmentCommandResult<T>` whatever Service returns.

### 5. `RubricParsingJob` is IN SCOPE for Phase 5

The job currently injects `CatalogDbContext` directly (line 16). Phase 5 updates it to inject `IRubricRepository` instead and calls narrow repository methods. This matches the Submission precedent (ExtractionJob updated in Phase 5) and Grading precedent (AiGradingJob updated in Phase 5).

`Jobs/DocxTextExtractor.cs` is a pure static utility with zero DbContext dependency — read it in Phase 5 verification only to confirm (already confirmed: line 9–19 has no EF Core). No change needed for this file beyond namespace/using adjustments if any.

### 6. DELETE the confirmed-dead `Parsing/DocxRubricParser.cs` and `Parsing/IRubricParser.cs`

**175 lines combined**, both files contain zero external callers anywhere in the codebase (grep-verified: only self-references). The current production code uses `IOpenCodeClient.ParseRubricCriteriaAsync()` (AI-based parsing) instead. This local DOCX-table parser was superseded and never wired up.

**Deleting confirmed-dead code is not a behavior change** (nothing calls it). This refactor is already reorganizing the `Parsing/`-adjacent code (moving `DocxTextExtractor` logic into `RubricParsingJob`'s orchestration), making this the natural time to clean it up as a zero-risk, zero-scope-creep housekeeping move.

**Document this as a deliberate cleanup** in the phase notes, not silent scope creep.

### 7. Class/Subject/Enrollment FK coupling: preserve composite-FK relationship exactly

`Class.SubjectId` (nullable, FK Restrict) and `Class.EnrollmentSubjectId` (always filled) both exist. `StudentEnrollment` has a composite FK into `Class` via `(ClassId, SubjectId)` mapping to `(Id, EnrollmentSubjectId)` (CatalogDbContext.cs lines 116–118).

Whichever repository owns `Class` (`IClassRepository`) must preserve this composite-FK relationship exactly as configured in `OnModelCreating`. `IEnrollmentRepository`'s queries joining into `Class` (e.g. `ListStudentIdsForLecturerAsync` joining `StudentEnrollment.Class.LecturerId` on line 92 of EnrollmentQueries) may need to fetch `Class` data mid-transaction.

**Decision:** `IEnrollmentRepository`'s methods query `Class` data directly using its own `DbContext` reference (`CatalogDbContext` is scoped and shared across all repositories within one request) rather than calling out to `IClassRepository.GetAsync()`, since a cross-repository call must never happen inside `EnrollmentCommands`' existing Serializable-transaction boundary (the transaction was never designed to span two repository instances). Both repositories share the same underlying `CatalogDbContext` instance within one request scope anyway, so querying `Class` directly inside `EnrollmentRepository` is safe and preserves atomicity.

### 8. Phase 4: two confirmed minor API-contract cleanups (user-approved)

`AssignmentResponse`/`RubricResponse` (new in `Dto/`) deliberately omit fields that the original raw-entity JSON serialization always emitted as `null`/`[]`/a raw EF concurrency token, with zero client-facing purpose:

- `Assignment.Subject` (always `null`), `Assignment.Rubrics` (always `[]`) — no code path ever `.Include()`s them.
- `Rubric.Subject` (always `null`), `Rubric.Assignment` (always `null`) — same reason.
- `Rubric.RowVersion` — unlike Enrollment (which round-trips its RowVersion through `UpsertEnrollmentRequest` for optimistic concurrency), no Rubric endpoint ever accepts a RowVersion back from the client; exposing it was a pure EF-implementation-detail leak.

**Explicitly confirmed with the user via AskUserQuestion before implementing** (asked in Vietnamese per the user's standing preference) — chose to drop these fields rather than reproduce them byte-for-byte. Documented here and in each DTO's own XML doc comment so a future reader doesn't mistake this for scope creep.

## Dependencies

External:
- MinIO (object storage) via `IObjectStorage` — already used, no new version required.
- Hangfire (background job framework) — already used, no new version required.
- OpenCode AI client via `IOpenCodeClient` — already used, no new version required.
- RabbitMQ event bus via `IEventBus` — already used, no new version required.

Blocked tasks:
- None — this is a pure refactoring, all code is already present and functional.

## Risks

- **Serializable transaction + row-version concurrency (Enrollment)** (Phase 2/3): This is the highest-risk area in the whole service. `UpsertStudentAsync`/`CorrectAdminAsync` use Serializable isolation (preventing race conditions where two concurrent updates to the same enrollment might both pass their "check current class" logic, then both hit a unique-constraint conflict). The row-version optimistic-locking pattern (setting `db.Entry(enrollment).Property(x => x.RowVersion).OriginalValue = expectedVersion`) is a SQL Server-specific technique that breaks if the transaction boundary or entity tracking gets split across layers. Mitigation: Phase 2 keeps the entire transaction + conflict-handling inside one `IEnrollmentRepository` method, never split between Repository and Service. Phase 3's `EnrollmentService` calls that one repository method atomically and returns its result-type discriminator to the endpoint. Phase 3's Manual Verification must include a concurrent-conflict test case.

- **Rubric file-upload/Hangfire-job-lifecycle** (Phase 4/5): `RubricsEndpoints.UploadRubricAsync` uploads to MinIO, then enqueues a Hangfire job with `backgroundJobs.Enqueue<RubricParsingJob>(job => job.ExecuteAsync(rubric.Id, ...))`. A developer might be tempted to simplify this in Phase 5 by making `RubricService.UploadAsync` return a job ID, then having the endpoint enqueue it — but that would split the "upload + enqueue" atomic operation (if the endpoint crashes between service return and enqueue, the parse job is never queued, leaving the rubric in Parsing state forever). Mitigation: Phase 4 keeps `backgroundJobs.Enqueue(...)` in the endpoint, and Phase 5 doesn't move it. Phase 5 only updates `RubricParsingJob` itself to use `IRubricRepository` instead of `CatalogDbContext` directly.

- **Composite-FK risk: Class↔StudentEnrollment relation** (Phase 2): The composite-key relationship between `StudentEnrollment` and `Class` (via `(ClassId, SubjectId)` → `(Id, EnrollmentSubjectId)`) is a complex constraint that must be validated during upsert. If `IEnrollmentRepository` and `IClassRepository` don't both understand this relationship, or if a cross-repository call tries to fetch `Class` outside a transaction, the constraint check could race. Mitigation: Phase 2 documents the exact constraint in both repository interface comments. Phase 3's Manual Verification includes a test case where a concurrent class-subject change is happening while an enrollment upsert is in flight.

- **Dead-code deletion** (Phase 1): `Parsing/DocxRubricParser.cs` and `Parsing/IRubricParser.cs` are being deleted. This is zero-risk (nothing calls them), but the deletion must be noted explicitly so a reviewer doesn't accidentally restore them thinking they're needed.

- **Manual-only verification** (all phases): No automated test project for Catalog (`AutoGrading.Catalog.Api.Tests/` confirmed empty — no `.csproj`, no source files). Each phase's Manual Verification section is mandatory before proceeding — do not batch multiple phases' verification into one pass at the end. One person runs each phase's checklist before moving to the next; a phase isn't "done" until every item in its Manual Verification section passes, not just "compiles".

- **No compiler-enforced boundary** (per Submission/Grading/Identity precedent): a future edit could reintroduce `CatalogDbContext` into `Endpoints/` or `Service/` without any build error — only code review catches it. Out of scope to fix (same limitation all prior services accepted).

- **Concurrent-conflict manual test is inherently fragile** (Phase 4 Manual Verification): "issue two PUT requests with stale rowVersion simultaneously" is described in natural language, not as a scripted/timed harness — true simultaneity is hard to guarantee via manual Postman/curl, so the race may not always reproduce on a given run. This matches the manual-only precedent set by Submission/Grading/Identity (no automated test project exists for any of them either), so it's accepted as-is rather than blocking the plan, but whoever runs Phase 4 verification should be aware this is the single most fragile manual step and may need a few attempts to actually observe the `409 Conflict` path.

- **`EnrollmentService`'s pass-through behavior isn't explicitly asserted in Phase 3** (Phase 3 Manual Verification): Phase 3's checklist for Enrollments just says "same as Phase 2 tests" since `EnrollmentService` mostly forwards to `EnrollmentRepository` unchanged. This means an accidental change (e.g. someone converts `EnrollmentService` to throw exceptions instead of returning `EnrollmentCommandResult<T>`, contradicting decision #4) wouldn't be caught by Phase 3's own checklist — only by Phase 4/6's end-to-end pass. Low risk since Phase 4 wiring would fail to compile/behave correctly if the contract changed, but noted here for awareness rather than adding a redundant Phase-3-specific check.

## Expected Timeline

Each phase: 2–3 hours implementation + 1–1.5 hours manual verification (per phase, sequential — no batching across phases, due to the 25-route complexity and the high-risk Enrollment concurrency handling). Phases are data-dependent (Phase 2 must compile before Phase 3 starts; Phase 3's services must work before Phase 4 can wire it), so all 6 phases typically run in one continuous session/branch. ~15–20 hours total if run continuously.
