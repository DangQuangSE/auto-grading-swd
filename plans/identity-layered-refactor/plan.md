# Plan: Tách logic khỏi Endpoint — Layered Architecture cho Identity

**Date:** 2026-07-23
**Mode:** Hard
**Test flag:** default (no `--tdd` — `AutoGrading.Identity.Api.Tests/` directory exists but contains no `.csproj` or source files; verification is manual per phase, matching Submission and Grading precedent)
**Status:** 🟢 Done

---

## Scope

Refactor `AutoGrading.Identity.Api` (single-project Minimal API, no changes to other services) from "all logic in `Endpoints/{AuthEndpoints, UsersEndpoints}.cs`" into a Layered Architecture within the same project:
`Constant/ Endpoints/ Domain/ Dto/ Interfaces/ Repository/ Service/` + existing `Clients/ Handlers/ Migrations/ Auth/` kept in place, now implementing the new interfaces.

Behavior-preserving refactor — no API contract change, no new business rules. **Auth logic itself (password hashing, JWT generation, Google token validation) remains identical; only its location changes from Endpoints to Service layer.**

## Phases

- [x] Phase 1: [phase-01-constants-and-interfaces.md](./phase-01-constants-and-interfaces.md) — additive only, zero behavior change. Quality: approved. Testing: manual (build clean).
- [x] Phase 2: [phase-02-repository.md](./phase-02-repository.md) — move `IdentityDbContext` to `Repository/`, create `IUserRepository` with all data-access methods for auth, user management, and authorization checks. Quality: approved (corrected on re-verify). Testing: manual (build clean, grep-verified).
- [x] Phase 3: [phase-03-service.md](./phase-03-service.md) — create `IAuthService` and `IUserService`, move business logic, authorization helpers, event publishing; endpoints now inject services only. Quality: approved. Testing: manual (build clean, security-critical paths independently re-verified).
- [x] Phase 4: [phase-04-slim-endpoints-and-dto.md](./phase-04-slim-endpoints-and-dto.md) — endpoints become pure HTTP adapters; introduce `Dto/` for request/response shapes. Quality: approved. Testing: manual (build clean, DTO mirroring verified, RegisterResponse property-name near-miss caught).
- [x] Phase 5: [phase-05-update-handlers.md](./phase-05-update-handlers.md) — `ClassLecturerAssignedHandler`, `SubmissionUploadedHandler`, `GradePublishedHandler` depend on `IUserRepository` instead of `IdentityDbContext`. Quality: approved. Testing: manual (build clean, no EF Core in Handlers/).
- [x] Phase 6: [phase-06-di-wiring.md](./phase-06-di-wiring.md) — wire everything in `Program.cs`, full manual regression pass. Quality: approved. Testing: manual (full-solution build clean, folder structure + security-critical-path checks passed; live E2E regression left for user).

## Session Notes
<!-- Updated by cook automatically — do not edit manually -->

**Last active:** 2026-07-23
**Phase in progress:** none — all 6 phases complete
**Status:** Plan complete. All 6 phases implemented, quality-gate APPROVED (2 corrected on re-verify), full-solution build clean. Nothing committed — standing instruction is no auto-commit; changes are in the working tree for manual review.

### Decisions made this session
- User confirmed upfront: include `AuthEndpoints.cs` (register/login/Google OAuth) in scope with the same rigor as `UsersEndpoints.cs`, and include the 3 event handlers in a Phase 5 equivalent — both recommended options, both accepted.
- `RosterAuthorization.cs` (static class) was fully retired in Phase 3, not just adapted — its logic moved into `UserService.AuthorizeRosterAccessAsync` (private method) and its enum into `Interfaces/IUserService.cs`, per the plan's own decision.
- Circular-dependency avoidance: `IAuthService`/`IUserService` take primitive parameters, not the endpoint's own request record types, mirroring Submission's/Grading's Phase 3 precedent — `Interfaces/` must never depend on `Endpoints/`.
- Caught and corrected two real regressions from `/simplify` passes before they landed: a double-fetch in `BulkUpdateRosterAsync`/`UpdateRosterFieldsAsync` (fixed by passing already-tracked entities instead of re-fetching by id), and a near-miss JSON-contract change where `RegisterResponse` would have used `UserId` instead of the original anonymous object's `Id` property name.
- Two `ck:quality` gate runs (Phase 2, Phase 6) initially mis-scored due to the same root cause: nothing is committed between phases, so a `git diff`-based scope check conflates all uncommitted phases together. Both corrected by cross-referencing the actual prior phase's own quality report/receipt.
- Namespace planning error caught before implementation: the plan initially assumed `AutoGrading.IdentitySvc.Api` (copying Submission's "Svc" suffix pattern without verifying) — corrected to the actual `AutoGrading.Identity.Api` before any code was written.

## Research Summary

This plan is a direct adaptation of two completed, identical-structure refactors:
- `AutoGrading.Submission.Api` refactor: [plans/submission-layered-refactor/plan.md](../../submission-layered-refactor/plan.md) + 6 phase files (2026-07-23, all phases complete, quality APPROVED).
- `AutoGrading.Grading.Api` refactor: [plans/grading-layered-refactor/plan.md](../../grading-layered-refactor/plan.md) + 6 phase files (2026-07-23, all phases complete, quality APPROVED).

**Identity's deliberate structural difference from prior two services:**
- Two separate endpoint files (`AuthEndpoints.cs` and `UsersEndpoints.cs`) sharing the same data layer (`User`, `ClassLecturerCache`, `SubmissionStudent`, `SubmissionGrader`).
- Already returns purpose-built response shapes (`LoginResponse`, `UserSummary`, `RosterImportReport`, anonymous objects) rather than raw domain entities — Phase 4's DTO work here is lighter: mostly *moving* already-good DTOs into `Dto/`, not introducing new ones to fix a leaking-entity problem like Submission/Grading did.
- One real distinction for authorization: two existing raw EF queries in `RosterAuthorization` (lines 41–42, 49–51 of the source) checking class lecturers and graders *directly against the DbContext* — these must move into `IUserRepository` methods so the authorization logic (moving into `UserService` as a private method) never touches EF Core directly.

Applying the same 6-phase structure, conventions, and manual-verification discipline proven safe in both prior services.

## Real files involved (already inspected, not guessed)

**Endpoints:**
- `Endpoints/AuthEndpoints.cs` (153 lines) — 3 routes: `POST /auth/register`, `POST /auth/login`, `POST /auth/google`, plus request/response DTOs at bottom (`RegisterRequest`, `LoginRequest`, `GoogleLoginRequest`, `LoginResponse`).
- `Endpoints/UsersEndpoints.cs` (236 lines) — 3 routes: `GET /users`, `PATCH /users/{userId}`, `POST /users/bulk-import`, plus helpers (`ParseIds`, `FilterToAuthorizedAsync`, `ResolveClassNamesAsync`) and response DTOs (`UserSummary`, `UpdateUserRequest`, `BulkImportForm`, `RosterImportRowResult`, `RosterImportReport`).

**Authorization and Roster Import:**
- `Authorization/RosterAuthorization.cs` (55 lines) — static helper, `AuthorizeAsync` method with two raw EF queries: `IsClassLecturerAsync` (lines 41–42) and `IsGraderForStudentAsync` (lines 49–51).
- `RosterImport/RosterFileParser.cs` (191 lines) — static utility, no dependencies, pure parsing logic.

**Domain:**
- `Domain/User.cs`, `Domain/ClassLecturerCache.cs`, `Domain/SubmissionStudent.cs`, `Domain/SubmissionGrader.cs` — plain EF entities.

**Data:**
- `Data/IdentityDbContext.cs` — single DbContext with 4 DbSets; moves to `Repository/` in Phase 2.

**Handlers (all in scope for Phase 5):**
- `Handlers/ClassLecturerAssignedHandler.cs` (35 lines, lines 16, 20, 28 touch `db.ClassLecturerCaches`, `db.SaveChangesAsync`).
- `Handlers/SubmissionUploadedHandler.cs` (34 lines, lines 16, 23, 27 touch `db.SubmissionStudents`, `db.SaveChangesAsync`).
- `Handlers/GradePublishedHandler.cs` (44 lines, lines 18, 30, 34 touch `db.SubmissionGraders`, `db.SaveChangesAsync`).
- `Handlers/DbUpdateExceptionExtensions.cs` (12 lines) — static helper, pure exception inspection, no DbContext dependency; moves namespace-only or stays put.

**Auth and DI:**
- `Auth/JwtTokenGenerator.cs` — already injectable, stays in `Auth/`.
- `Auth/GoogleAuthOptions.cs` — configuration class, stays in `Auth/`.
- `Program.cs` (109 lines) — current DI: `AddDbContext<IdentityDbContext>`, `AddSingleton<IPasswordHasher<User>>`, `AddJwtTokenGenerator`, `AddJwtAuthentication`, `AddEventBus`, handler registrations.

## Deliberate Design Decisions (Resolved — Do Not Re-litigate)

### 1. One `IUserRepository`, not split by endpoint file

Both `AuthEndpoints.cs` and `UsersEndpoints.cs` operate on the same domain: `User` + `ClassLecturerCache` + `SubmissionStudent` + `SubmissionGrader`. A single repository covers the entire bounded context, mirroring the one-repo-per-context precedent from both prior services. Methods are named by use case, not by endpoint:
- `ExistsByEmailAsync(email)` — used by both register and Google login to check email existence.
- `ClassExistsAsync(classId)` — used by register to validate class exists.
- `CreateUserAsync(user)` — used by both register and Google-login-new-user paths.
- `GetByEmailAsync(email)` — used by login.
- `GetByGoogleSubjectOrEmailAsync(googleSubjectId, email)` — used by Google login's user lookup.
- `LinkGoogleSubjectIdAsync(userId, googleSubjectId)` — used by Google login's link-new-subject path.
- `GetByIdAsync(userId)` — used by single-user update path.
- `ListAsync(ids?)` — used by `GET /users` list endpoint.
- `GetClassLecturerCacheByNameAsync(className)` — used by bulk import per-row class lookup.
- `ResolveClassNamesAsync(classIds)` — batch lookup for the `GET /users` response formatter.
- `IsClassLecturerAsync(classId, lecturerId)` — used by authorization logic, extracted from `RosterAuthorization` line 41–42.
- `IsGraderForStudentAsync(studentId, lecturerId)` — used by authorization logic, extracted from `RosterAuthorization` line 49–51.
- `UpdateRosterFieldsAsync(userId, studentCode?, classId?)` — single-user update, throws `DbUpdateConcurrencyException` on conflict (same as today).
- `BulkUpdateRosterAsync(acceptedUpdates)` — bulk-import upsert list, preserves atomicity (one `SaveChangesAsync` for all rows or none).

### 2. `RosterAuthorization`'s logic moves into `UserService` as a private method

Instead of a separate `IAuthorizationService` interface, the authorization check logic becomes `AuthorizeRosterAccessAsync(RequesterContext, User target, CancellationToken)` → `RosterAuthorizationResult`, a private method inside `UserService`. It calls the new `repository.IsClassLecturerAsync` and `repository.IsGraderForStudentAsync` methods for its data needs. This mirrors Submission's `GetLecturerAllowedStudentIdsAsync` (moved into `SubmissionService` as a private helper, not extracted as a separate service). The logic is reused by 3 call sites within one file (`UsersEndpoints.cs`) — all 3 move into `UserService` together in Phase 3, maintaining logical cohesion. Delete `Authorization/RosterAuthorization.cs` once its logic has moved (or leave a thin marker file if preferred, but the goal is one canonical location).

### 3. `BulkImportAsync`'s all-or-nothing atomicity must be preserved byte-for-byte

Current behavior (lines 108–117 of `UsersEndpoints.cs`): the endpoint collects all accepted rows' mutations (via per-row validation loop), then calls `db.SaveChangesAsync()` exactly once. If that fails, none of the rows are updated.

Refactored behavior: `UserService.BulkImportAsync` performs the per-row validation loop (class lookup, user lookup, authorization check — all via repository read methods, building a `details` list of skip reasons exactly as today) and collects only the accepted rows' `(UserId, StudentCode?, ClassId)` into a list. Then it makes exactly ONE call to `repository.BulkUpdateRosterAsync(acceptedRows, ct)`, which fetches all those tracked `User` rows, applies the mutations, and does a single `SaveChangesAsync` — an all-or-nothing commit for the accepted rows, matching today's behavior byte-for-byte. **Do NOT call `SaveChangesAsync` per row inside the service or endpoint; the entire batch commits together or not at all.**

### 4. Two services: `IAuthService` and `IUserService`

A deliberate, non-artificial split reflecting two distinct route groups with different security postures and domain concerns:
- **`IAuthService`** (dependency: `IUserRepository`, `IPasswordHasher<User>`, `JwtTokenGenerator`, `IEventBus`) — handles `POST /auth/register`, `POST /auth/login`, `POST /auth/google`.
- **`IUserService`** (dependency: `IUserRepository` only — no external API clients, no messaging; roster sync is a write-only internal operation) — handles `GET /users` (list with authorization filter), `PATCH /users/{userId}` (single update with authorization), `POST /users/bulk-import` (bulk upsert with per-row authorization).

This mirrors a real, pre-existing structural distinction in the code. Do not try to merge them into one `IIdentityService` — that would obscure the cleaner, narrower dependencies each one actually needs.

### 5. `RosterFileParser` stays a plain static utility, not interface-ized

Unlike Submission's `IArtifactParser` (which is interface-ized because multiple concrete parsers — `DocxReportParser`/`DrawioDiagramParser` — implement it via an `ArtifactParser` dispatcher), `RosterFileParser` has exactly one implementation and nothing needs to mock it. Keep it static, called directly from `UserService.BulkImportAsync` (it has zero DB/HTTP dependencies, so calling it from Service doesn't violate the "Service has no ASP.NET Core types" rule — verify by reading the file; note only that `[FromForm]` binding types like `BulkImportForm` stay in `Dto/`, consumed and translated by the endpoint before the service call).

### 6. Auth logic itself does NOT change; only its location changes

The security-sensitive logic (password hashing via `IPasswordHasher<User>`, JWT generation via `JwtTokenGenerator`, Google token validation via `GoogleJsonWebSignature.ValidateAsync`) remains identical to today's implementation. Phase 3 only moves this logic out of `Endpoints/AuthEndpoints.cs` and into `Service/AuthService.cs` — the method bodies stay the same line-for-line, minus the `IResult` wrapping (which moves to the endpoint in Phase 4). **Do NOT refactor or "improve" this logic, even if you spot an opportunity.** This refactor's value is architectural separation, not algorithm improvement. Emphasize thorough manual verification of register/login/Google-login in Phase 3's Manual Verification section given the security-sensitive nature.

### 7. The 3 event handlers are IN SCOPE for Phase 5

`ClassLecturerAssignedHandler`, `SubmissionUploadedHandler`, and `GradePublishedHandler` all write to tables the repository covers (`ClassLecturerCache`, `SubmissionStudent`, `SubmissionGrader`). Phase 5 updates each handler to inject `IUserRepository` instead of `IdentityDbContext` and calls narrow, idempotent repository methods. Read each handler's existing idempotency comment and preserve it exactly (e.g., `ClassLecturerAssignedHandler` line 32: "row already inserted by a concurrent delivery"). `DbUpdateExceptionExtensions` is a small static helper — read it; if it has no DbContext dependency (it doesn't — it's pure exception inspection), move namespace-only or leave it in place; no change needed beyond a possible `using` update.

## Dependencies

External:
- `Microsoft.AspNetCore.Identity` (`IPasswordHasher<User>`) — already used, no new version required.
- `Google.Apis.Auth` (`GoogleJsonWebSignature`) — already used, no new version required.
- `DocumentFormat.OpenXml` (for Excel parsing in `RosterFileParser`) — already used, no new version required.
- RabbitMQ event bus via `AutoGrading.Common.Messaging` — already used, no new version required.

Blocked tasks:
- None — this is a pure refactoring, all code is already present and functional.

## Risks

- **`BulkImportAsync` atomicity split across layers** (Phase 2/3): If the per-row loop's mutations get committed piecemeal (per-row `SaveChangesAsync` calls), the all-or-nothing guarantee silently breaks. A second, subtle risk: if `UserService.BulkImportAsync` returns after collecting the `acceptedRows` list but before calling `repository.BulkUpdateRosterAsync`, and the handler crashes, the updates are lost — but this isn't a regression (today's endpoint has the same behavior). Mitigated by keeping the entire batch inside one repository method call, never split. Phase 3's Manual Verification must exercise a test case where the 3rd accepted row's `SaveChangesAsync` fails and confirm no rows were updated (not even the first two).

- **Authorization data-access split** (Phase 2): `RosterAuthorization`'s two EF queries (class lecturers, graders via join) currently execute within the same method call. Once these become separate `repository.IsClassLecturerAsync`/`repository.IsGraderForStudentAsync` methods in Phase 2, and `UserService` calls both in sequence (Phase 3), a state change between the two calls could theoretically change the outcome — but this isn't a race (the data never changes between the two checks; a lecturer's assignment is never revoked mid-call). Accepted as a known limitation per Submission/Grading precedent.

- **Auth logic is security-sensitive** (Phase 3): The password hashing, JWT token generation, and Google token validation are high-stakes code paths. A careless refactor of this phase could introduce a security bypass or data leak. Mitigated by: (1) the refactor is *structural only* — logic bodies never change, only location; (2) Phase 3's Manual Verification section includes explicit steps to re-verify register/login/Google-login behavior end-to-end, with attention to error codes and token validity; (3) code review (enforced by standing no-auto-commit instruction).

- **Manual-only verification** (all phases): No automated test project for Identity (same situation Submission and Grading had — directories exist but are empty/stale). Each phase's Manual Verification section is mandatory before proceeding — do not batch multiple phases' verification into one pass at the end. One person runs each phase's checklist before moving to the next; a phase isn't "done" until every item in its Manual Verification section passes, not just "compiles".

- **No compiler-enforced boundary** (per Submission/Grading precedent): a future edit could reintroduce `IdentityDbContext` into `Endpoints/` or `Service/` without any build error — only code review catches it. Out of scope to fix (same limitation both prior services accepted).

## Expected Timeline

Each phase: 1–2 hours implementation + 0.5–1 hour manual verification (per phase, sequential — no batching across phases). Phases are data-dependent (Phase 2 must compile before Phase 3 starts; Phase 3's service must work before Phase 4 can wire it), so all 6 phases typically run in one continuous session/branch. ~8–12 hours total if run continuously.

## Red-Team Review Notes

`plan-reviewer` ran against this plan (see conversation for full report). Verdict: WARN, 8 findings, **0 CRITICAL** (a meaningfully better result than Submission's/Grading's red-team passes, though still requiring real fixes). Adjudication:
- 2 HIGH findings (Phase 1's exception docstrings said `UserAlreadyExistsException`/`ClassNotFoundException` are "thrown by `CreateUserAsync`") — ACCEPTED as a real ambiguity, but the fix is a wording correction, not new logic: Phase 2/3 already correctly place these checks in `AuthService.RegisterAsync` *before* calling `CreateUserAsync` (which does no existence check of its own, preserving today's exact "check-then-create" pattern with no race-condition guard). The reviewer's suggested fix — adding `DbUpdateException` catching inside `CreateUserAsync` as a new safety net — was REJECTED: that would add race-condition protection that doesn't exist today, a genuine behavior change outside a structural refactor's scope, not a preservation.
- 1 HIGH finding (`BulkUpdateRosterAsync` atomicity not explicitly forbidding a per-row-save loop) — reviewed against the actual phase-02 text, which already states this explicitly ("This method is the bottleneck that preserves atomicity... never piecemeal") — no edit needed, already correct.
- 1 MEDIUM finding (Google login's create/link/no-op branches could collapse) — ACCEPTED, Phase 3 now spells out all three branches explicitly and states `UserRegistered` publishes on branch 1 only.
- 1 MEDIUM finding (`IdentityConstants.ConcurrentModificationError` missing the `userId` interpolation placeholder) — ACCEPTED, added `{0}` placeholder + `string.Format` note to Phase 1.
- 1 MEDIUM finding (`RosterAuthorization`'s join condition must be preserved exactly, not reimplemented as separate lookups) — NOTED, already will be preserved since Phase 2 cites the exact source lines; no plan edit needed, just an implementation reminder.
- 1 MEDIUM finding (handler idempotency mechanism — check-then-insert vs. catch-PK-violation — needs explicit specification) — NOTED, deferred to Phase 5 implementation time: read each handler's actual current code before writing its repository method, same practice used throughout this session, rather than over-specifying now from a summary.
- 1 MEDIUM finding (quality-gate criteria for Identity's security-sensitive paths aren't separately defined) — NOTED, no action: the standard `ck:quality --gate` contract applies uniformly; Phase 3's Manual Verification section already carries the extra auth-specific verification burden this plan needs.
