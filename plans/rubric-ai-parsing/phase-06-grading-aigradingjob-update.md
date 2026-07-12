# Phase 6: Grading AiGradingJob Update

## Requirements

Refactor `AiGradingJob` to read confirmed rubric criteria from the Grading service's new local table (populated by the Catalog `RubricConfirmed` event consumer) instead of using a hardcoded placeholder criterion. If no confirmed criteria exist for the assignment yet, the job must fail with a clear error message and rely on Hangfire's retry mechanism, rather than silently grading against a fabricated criterion.

## Steps

1. In `AiGradingJob.cs`, locate the Execute method and identify the section where the hardcoded placeholder criterion is currently created or retrieved.
2. Add a step that queries the local criteria table (via `GradingDbContext`) by the assignment's `RubricId` to retrieve the confirmed criteria.
3. If criteria are found, pass them to the existing grading logic (ensure the logic already supports a list of criteria, not just a single placeholder).
4. If no criteria are found, throw an exception with a clear message (e.g., "No confirmed rubric criteria found for assignment {assignmentId}. Confirm the rubric in Catalog first.") and let Hangfire retry the job.
5. Remove all references to the hardcoded "Overall Quality" placeholder creation from the job.
6. Verify the job's scoring logic still works correctly when given a list of multiple real criteria (not just one placeholder).
7. Test end-to-end: confirm a rubric with multiple criteria in Catalog, then run `AiGradingJob` for a submission under that assignment and verify the grading scores align with the actual confirmed criteria.

## Success Criteria

- `AiGradingJob.cs` no longer contains hardcoded placeholder criterion creation
- The job queries the local criteria table by assignment/rubric ID
- If criteria exist, grading proceeds with those criteria
- If criteria do not exist, the job throws a descriptive error and Hangfire retries
- A test submission under an assignment with a confirmed rubric is graded successfully
- The resulting `AiCriterionScores` rows match the actual confirmed criteria names/max scores
- Grading service compiles and Hangfire job execution completes without errors

## Risks

- **Null pointer/missing criteria in production** — If an assignment is submitted for grading before its rubric is confirmed, `AiGradingJob` will fail. If the rubric is never confirmed, the job will retry indefinitely (Hangfire default). *Mitigation:* The fail/retry behavior is intentional; add logging so administrators are alerted; consider adding a "soft deadline" in Hangfire (e.g., move to dead-letter after 3 failed retries, then alert the lecturer that confirmation is needed).
- **Criteria deserialization errors** — If the criteria are stored in JSON and the schema changes, deserialization might fail. *Mitigation:* Use defensive parsing when deserializing criteria from the local table; log any schema mismatches and skip those criteria gracefully.
- **Race condition with re-confirmation** — If a rubric is re-confirmed while a job for an older submission is running, the job might read the new criteria instead of the old ones. *Mitigation:* This is accepted as correct behavior (the job should always use the latest confirmed criteria); if historical grading tracking is needed, that is a separate versioning feature (P3 in the spec, out of scope).

---
