# Spec: AI-based rubric parsing

**Date:** 2026-07-12
**Status:** Draft

---

## Problem Statement

Rubric upload currently creates a single hardcoded "Overall Quality" placeholder criterion regardless of file content, and the Grading service separately fabricates its own placeholder criterion instead of using real rubric data — so the auto-grading pipeline never actually grades against a lecturer's real rubric. This replaces both stubs with real criteria extracted from the uploaded `.docx` via AI, reviewed and confirmed by the lecturer, and reliably propagated to Grading.

---

## User Stories

<!-- P1 = MVP (must ship), P2 = nice-to-have, P3 = future/out-of-scope -->

- **[P1]** As a lecturer/admin, I want to upload a rubric `.docx` and have AI extract a draft list of criteria (name, description, max score) so that I don't have to manually re-type my rubric into the system.
  Accepted when: after upload, the rubric has status `Draft` and a criteria list populated by AI extraction (not the old hardcoded placeholder).

- **[P1]** As a lecturer/admin, I want to preview and edit the AI-extracted criteria before it's used for grading, so that AI extraction mistakes don't silently affect student scores.
  Accepted when: the FE shows the draft criteria list; edits (add/remove/change a criterion's name, description, or max score) are saved back before confirmation.

- **[P1]** As a lecturer/admin, I want to explicitly confirm a rubric so that only my reviewed version is ever used to grade submissions.
  Accepted when: a `Confirm` action transitions the rubric from `Draft` to `Confirmed` and publishes a `RubricConfirmed` event carrying the final criteria; grading only reads `Confirmed` rubrics.

- **[P1]** As a lecturer/admin, I want to unlock a `Confirmed` rubric back to `Draft` before editing its criteria, so that I never silently mutate the version currently used for grading.
  Accepted when: editing criteria is only allowed while `Status = Draft`; a `Confirmed` rubric requires an explicit `Unlock` action (transition back to `Draft`) before its criteria endpoint accepts edits; re-confirming afterward re-publishes `RubricConfirmed`.

- **[P1]** As the Grading service, I want to receive and store confirmed rubric criteria so that `AiGradingJob` grades against the lecturer's real rubric instead of a placeholder.
  Accepted when: `AiGradingJob` reads criteria from Grading's own local table (populated by consuming `RubricConfirmed`), with no live HTTP call to Catalog at grading time.

- **[P1]** As an admin, I want to mark a rubric as school-wide (shared) instead of tied to one lecturer, so that university-wide exams can use one common template.
  Accepted when: `Rubric` has an explicit `Scope` (`Lecturer` | `SchoolWide`); school-wide rubrics are visible/usable across lecturers for the relevant subject.

- **[P1]** As a lecturer, I want re-uploading a rubric file for the same assignment to replace the existing rubric in place, so that I don't accumulate duplicate/stale rubric rows.
  Accepted when: uploading a new file for an assignment that already has a rubric updates that same rubric row (new file, re-triggers AI parse, resets status to `Draft`) rather than creating a new row.

- **[P2]** As a lecturer, I want to see a "Parsing..." status while AI extraction runs in the background, so that I know the upload succeeded even before criteria are ready.
  Accepted when: rubric has a transient state (e.g. `Parsing`) between upload and draft-criteria-ready, visible in the FE.

- **[P3]** _(out of scope — noted for future)_ Rubric criteria versioning/history so that re-confirming after some submissions were already graded doesn't silently change past grades' basis.

- **[P3]** _(out of scope — noted for future)_ Fallback to structured/hardcoded table parsing if AI extraction repeatedly fails for a given lecturer's format.

---

## Functional Requirements

<!-- Number each. Be specific. -->

1. FR-01: `Rubric` domain entity gains: `LecturerId` (nullable Guid, owner), `Scope` (enum: `Lecturer`, `SchoolWide`), `Status` (enum: `Parsing`, `Draft`, `Confirmed`).
2. FR-02: `POST /catalog/rubrics/upload` stores the file, sets `Status = Parsing`, enqueues an async background job (Hangfire, matching the `AiGradingJob` pattern) to call the AI extraction client, and returns immediately with the rubric id.
3. FR-03: The AI extraction background job calls a shared `AutoGrading.Common` AI client (moved out of Grading's `OpenRouterClient`) with the rubric document text, parses the response into a criteria list (name, description, max score, order), persists it against the rubric, and sets `Status = Draft`.
4. FR-04: `PATCH /catalog/rubrics/{id}/criteria` (or equivalent) lets a lecturer/admin edit the draft criteria list (add, remove, update fields) while `Status = Draft`.
5. FR-05: `POST /catalog/rubrics/{id}/confirm` transitions `Status` from `Draft` to `Confirmed` and publishes a `RubricConfirmed` event on the bus carrying the rubric id, subject id, assignment id, scope, and the full final criteria list.
5a. FR-05a: `POST /catalog/rubrics/{id}/unlock` transitions `Status` from `Confirmed` back to `Draft`. The criteria-edit endpoint (FR-04) only accepts changes while `Status = Draft`; editing a `Confirmed` rubric requires calling unlock first.
6. FR-06: Grading service subscribes to `RubricConfirmed`, upserts the criteria into a new local table in `GradingDbContext` (keyed by rubric/assignment id), replacing any prior local copy for that assignment.
7. FR-07: `AiGradingJob` reads criteria from Grading's local table instead of building a hardcoded placeholder; if no confirmed criteria exist yet for the assignment, the job fails/retries rather than grading against a fabricated criterion.
8. FR-08: Re-uploading a file for an assignment that already has a rubric updates the existing `Rubric` row in place (new `FileObjectKey`, `Status` reset to `Parsing`, old criteria cleared/replaced once re-parsed) instead of inserting a new row.
9. FR-09: `Scope = SchoolWide` rubrics are creatable only by `admin` role; `Scope = Lecturer` rubrics keep today's `lecturer`/`admin` authorization.

---

## Non-Functional Requirements

- Performance: AI extraction runs as a background job, not blocking the upload HTTP response (upload responds in the same order of magnitude as today, before this change).
- Security: rubric confirm/edit endpoints require the same `lecturer`/`admin` role policy as upload today; `SchoolWide` rubric creation is restricted to `admin`.
- Availability: `AiGradingJob` must not have a hard runtime dependency on the Catalog service being reachable at grading time (criteria are read from Grading's own local copy).

---

## Success Criteria

- [ ] A rubric upload with a real (non-trivial) `.docx` produces more than one AI-extracted criterion, replacing the old single hardcoded "Overall Quality" placeholder.
- [ ] A lecturer can edit AI-extracted criteria and the edited values persist before confirmation.
- [ ] After confirming a rubric, `AiGradingJob` for a submission under that assignment grades against the confirmed criteria (verifiable via `AiCriterionScores` rows matching the confirmed criteria names), not the old placeholder.
- [ ] Re-uploading a rubric file for an assignment that already has one results in exactly one `Rubric` row for that assignment (no duplicate rows).
- [ ] A `SchoolWide`-scoped rubric is usable/visible regardless of which lecturer is grading a school-wide exam's submissions.

---

## Out of Scope

- Rubric criteria versioning/history across re-confirmations (P3 above).
- Fallback to non-AI (hardcoded table) parsing (P3 above).
- Handling of already-graded submissions when a rubric is later re-confirmed with different criteria (flagged as an assumption below).

---

## Assumptions

- It is acceptable for already-graded submissions to keep their existing scores unchanged even if the rubric is later re-confirmed with different criteria (no automatic re-grading triggered by re-confirmation).
- The AI extraction step reuses the same OpenRouter account/model (`deepseek/deepseek-chat`) already configured for Grading; no new provider or credentials are introduced.
- "School-wide" rubrics are scoped per subject like today's rubrics (still tied to a `SubjectId`), just without a specific owning lecturer — not a single global rubric across all subjects.

---

