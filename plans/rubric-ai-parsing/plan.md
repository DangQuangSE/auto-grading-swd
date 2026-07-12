# Plan: AI-based Rubric Parsing

Status: 🟡 In Progress
Date: 2026-07-12
Mode: Hard

## Overview

This plan replaces the hardcoded single "Overall Quality" placeholder criterion with real AI-extracted criteria from uploaded rubric `.docx` files, reviewed and confirmed by lecturers before use. It spans shared infrastructure, Catalog service (upload, background job, status machine, confirmation), Grading service (event consumption, local copy, AiGradingJob update), and frontend UI.

## Session Notes
<!-- Updated by cook automatically — do not edit manually -->

**Last active:** 2026-07-12 (session)
**Phase in progress:** phase-02-catalog-domain-status-machine
**Status:** Phase 1 complete (build + tests green, simplify applied); starting Phase 2.

### Decisions made this session
- Registered `OpenRouterClient` in Catalog/Grading via `AddHttpClient<IOpenRouterClient, OpenRouterClient>()` (typed client), not a singleton as the phase file literally said — matches the existing working Grading pattern.
- Factored OpenRouter DI registration into a shared `AddOpenRouterClient(services, configuration)` extension in `AutoGrading.Common.Extensions.ServiceCollectionExtensions`, used by both services, during the mandatory Step 3.S simplify pass.
- `ParseRubricCriteriaAsync` stub fallback (no API key) returns a single generic "Overall Quality" placeholder criterion — intentionally not derived from document text, since there's no non-AI way to do that.
- Added `OpenRouter__*` env vars to `catalog-api` in `docker-compose.yml` and an `OpenRouter` section to Catalog's `appsettings.json`, mirroring Grading's existing setup (reuses the same `OPENROUTER_API_KEY` from `.env`).

### Next immediate action
Implement Phase 2 (Catalog Domain & Status Machine): add `Status`/`Scope`/`LecturerId` to `Rubric` entity.

## Phases

- [x] Phase 1: Shared AI Infrastructure — Move `OpenRouterClient` to `AutoGrading.Common`, add rubric-criteria extraction method
- [ ] Phase 2: Catalog Domain & Status Machine — Add `Status` (Parsing/Draft/Confirmed), `Scope` (Lecturer/SchoolWide), `LecturerId` to `Rubric` entity
- [ ] Phase 3: Catalog Upload & Background Job — Update upload endpoint to store file and enqueue async Hangfire extraction job
- [ ] Phase 4: Catalog Edit, Confirm & Unlock Endpoints — Add criteria edit (Draft-only), confirm (Draft→Confirmed), unlock (Confirmed→Draft) endpoints; confirm publishes `RubricConfirmed` event
- [ ] Phase 5: Event Contract & Grading Consumer — Define `RubricConfirmed` event contract, create event handler in Grading, add local criteria table to `GradingDbContext`, subscribe handler in `Program.cs`
- [ ] Phase 6: Grading AiGradingJob Update — Refactor `AiGradingJob` to read criteria from local table instead of placeholder; fail/retry if no confirmed criteria exist
- [ ] Phase 7: Frontend UI — Add criteria preview/edit screen, confirm/unlock buttons, scope selector on rubric upload; handle Parsing status indicator

## Research Summary

**AI Extraction Approach:** User confirmed AI-first (DeepSeek via OpenRouter) with mandatory lecturer review/edit before confirmation, rejecting hardcoded table parsing due to rubric format variability.

**Grading Criteria Sync:** Chose event-driven local copy over synchronous HTTP call to avoid runtime Catalog dependency during grading. User accepted the inherent risk of event delivery failures given the existing codebase has no dead-letter queue or idempotency framework — mitigated by making the `RubricConfirmed` consumer upsert logic idempotent (keyed by `RubricId`).

**Status Machine:** `Parsing` (transient, job running) → `Draft` (ready for edit) → `Confirmed` (locked, published); `Unlock` action transitions `Confirmed` back to `Draft` for re-editing.

**Re-upload Behavior:** Updating the rubric file for an existing assignment replaces the same `Rubric` row in place (new `FileObjectKey`, `Status` reset to `Parsing`) rather than creating a new row.

**Shared OpenRouter Client:** Moved out of Grading into `AutoGrading.Common` to eliminate duplication; both Catalog (for extraction) and Grading (for grading) call the same client.

## Dependencies

**External:** OpenRouter account (already configured for Grading, reused here).
**Internal Blocking:** None — all phases ordered by logical dependency.
**Assumes:** `OpenRouterOptions` already bound in Grading's `appsettings.json`; Hangfire already registered in Catalog; RabbitMQ event bus already operational.

## Risks

**HIGH:**
- **Event delivery loss during Grading RubricConfirmed consumption** — If RabbitMQ/event bus loses or delays the `RubricConfirmed` event, Grading's local criteria table never gets populated, and `AiGradingJob` fails/retries indefinitely. No dead-letter queue or replay mechanism exists in the codebase. *Mitigation:* Make the upsert logic idempotent (keyed by `RubricId`) so manual replay is safe; document the limitation; add a manual "retry event consumption" admin endpoint (future, not in this plan).
- **AI extraction quality failures** — DeepSeek may misparse rubric formats with merged cells, scanned tables, or non-tabular layouts. *Mitigation:* Mandatory lecturer review/edit in the `Draft` state before confirmation; if extraction is frequently unusable, the lecturer can manually add/edit criteria.

**MEDIUM:**
- **Breaking change to existing grading flow** — Moving `OpenRouterClient` and updating `AiGradingJob` to read from a local table instead of a placeholder could introduce regressions if existing grading tests or production assignments depend on the placeholder's exact behavior. *Mitigation:* Test that `AiGradingJob` still works end-to-end with confirmed criteria; if an assignment has no confirmed rubric yet, the job must fail with a clear error, not silently use a fallback.
- **Concurrent re-upload race condition** — If a lecturer uploads a new rubric file while the previous background job is still running, both jobs might attempt to update the same `Rubric` row. *Mitigation:* In the background job, add a check that the job's input `rubricId` still has `Status = Parsing`; if already `Draft` or `Confirmed`, the job was superseded and should exit early.
- **Rubric stuck in `Parsing` forever** — If AI extraction fails permanently (bad `.docx`, OpenRouter API down/out of quota), Hangfire's built-in retries eventually exhaust and the rubric has no automatic recovery. *Mitigation (this plan):* Phase 3 adds `POST /rubrics/{id}/retry-parsing` so a lecturer/admin can manually re-trigger the job without re-uploading; log clearly on final failure so it's also diagnosable from logs. *Deferred to v2:* a lecturer-visible "parsing failed" state distinct from `Parsing` (currently both look the same in the UI while a job is pending or has exhausted retries).
- **Cross-service stale data on rubric deletion** — There is no rubric-delete feature in this spec, but if one is added later, deleting a `Confirmed` rubric in Catalog would not cascade to Grading's local criteria copy — Grading would keep grading against a rubric that no longer exists in Catalog. *Out of scope for this plan* (no delete endpoint exists); flagged here so a future delete feature accounts for it (e.g. publish a `RubricDeleted` event Grading also subscribes to).

**LOW:**
- **Existing demo rubrics deleted by migration** — Adding non-nullable `Status`/`Scope` columns to `Rubric` requires either backfill or clearing existing rows. User confirmed current rubric data is demo-only, so the migration clears `Rubrics`/`RubricCriteria` instead of backfilling — lecturers must re-upload after this ships.

## User Story Mapping

| Phase | P1 Story Coverage |
|-------|-------------------|
| 1, 2, 3 | Upload + AI extract (Story 1); provides foundation for edit/confirm (Stories 2, 3, 4) |
| 4 | Edit criteria (Story 2); confirm (Story 3); unlock (Story 4); re-upload replaces (Story 7) |
| 5, 6 | Grading consumes confirmed criteria (Story 5) |
| 2, 4, 7 | SchoolWide scope (Story 6) |
| 3, 7 | Parsing status visibility (P2 Story) |

---
