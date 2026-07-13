# Brainstorm: AI-based rubric parsing (replace placeholder criteria)

**Date:** 2026-07-12

## Ideas Explored

- **Hardcoded OpenXML table parsing** — read `.docx` tables via DocumentFormat.OpenXml, map fixed column headers to criteria fields. Dismissed: user confirmed every lecturer formats their rubric differently, so no single fixed column schema would hold.
- **Hybrid: hardcoded parse with AI fallback** — try structured parse first, fall back to AI if the table doesn't match. Dismissed as unnecessary complexity once AI-first was confirmed as the primary path; no evidence yet that AI extraction quality needs a fallback.
- **AI-first extraction with lecturer review/edit before use** — user's stated direction: AI reads the uploaded `.docx`, extracts a draft criteria list, lecturer previews/edits it in the FE, then explicitly confirms. Only confirmed criteria are ever used for grading. This became the chosen direction.
- **Rubric ownership/scope** — repo currently has no `LecturerId` or scope concept on `Rubric`. Considered "null LecturerId = global" (implicit) vs. an explicit `Scope` enum (`Lecturer | SchoolWide`). Chose explicit field for clarity and future extensibility (e.g. per-class scope later).
- **Where does Grading fetch confirmed criteria from** — considered (a) Grading calls Catalog's HTTP API synchronously at grading time, vs (b) Grading maintains its own local copy of criteria, synced via a domain event when a rubric is confirmed. Chose (b): the repo already has an unused `RubricParsed` event on the bus, and grading jobs should not have a hard runtime dependency on Catalog being reachable.
- **Where does the AI/OpenRouter client live** — considered duplicating `OpenRouterClient` into Catalog vs. having Catalog call Grading's HTTP API vs. moving the client into `AutoGrading.Common` for both services to share. Chose the shared-`Common` option — it's a pure code move, not a new abstraction, and avoids a backwards service dependency (Catalog needing Grading, which itself will depend on Catalog for criteria).
- **Sync vs async AI call on upload** — considered blocking the upload request on the AI call (2-10s) vs. an async Hangfire job matching the existing `AiGradingJob` pattern. Chose async, consistent with existing architecture and to avoid blocking uploads on third-party API latency.
- **Re-upload behavior** — considered "always create a new rubric row" (keep history) vs. "in-place replace" (like a normal update endpoint). Chose in-place replace per user's explicit preference — simpler, one rubric per assignment.

## User's Direction

AI reads the uploaded rubric `.docx` and produces a draft/structured criteria list. The lecturer previews this draft in the UI, edits as needed, then explicitly confirms — only the confirmed version is persisted as "live" and usable for grading. This guarantees the AI grading step never grades against a template it guessed wrong, since it always reads back the lecturer-approved version.

Scoping: most rubrics belong to a specific lecturer (and are tied to a subject/assignment as today); school-wide exams use one shared template instead, needing an explicit scope rather than an implicit "no owner" convention.

Re-uploading a new file for the same assignment replaces the existing rubric in place (standard update semantics), rather than accumulating historical rows.

## Open Questions

- Exact schema/fields for the `Draft`/`Confirmed` status machine (does re-confirming after an edit re-publish the event? is there a distinct "Parsing" transient state while the background job runs?) — to be resolved in `/ck:plan`.
- Exact contract for the `RubricConfirmed` event payload (full criteria list vs. rubric ID + Grading queries once) — leaning toward full payload per the "no runtime dependency on Catalog" decision, to be finalized in planning.
- What happens to in-flight/already-graded submissions if a rubric is re-confirmed with different criteria after some submissions were already graded under the old version — not addressed by the user, flagged as an assumption/out-of-scope candidate.
- UI details for the criteria preview/edit screen (add/remove criterion rows, validation on max score sums, etc.) — left to planning/implementation.

## Risks

- **AI extraction quality/cost**: `deepseek/deepseek-chat` via OpenRouter may misparse unusual rubric layouts (scanned tables, merged cells, non-tabular formats) — mitigated by the mandatory lecturer review/edit step before confirmation, but still a UX risk if extraction is frequently wrong.
- **Cross-service event contract drift**: Grading service will hold a denormalized copy of criteria synced via event — if the event is ever missed/fails to publish (e.g. bus outage), Grading's local copy goes stale relative to Catalog's confirmed rubric. No reconciliation/replay mechanism has been discussed yet.
- **Migration/refactor surface**: moving `OpenRouterClient` out of Grading into `AutoGrading.Common` touches a service that already works; needs care not to break the existing `AiGradingJob` grading flow while relocating it.
