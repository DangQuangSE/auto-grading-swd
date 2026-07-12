# Phase 6: Grading Batch Grades Endpoint

## Requirements

Implement a GET /grades/final?submissionIds=... batch endpoint in the Grading service that retrieves the latest published `FinalGrade` for each requested `SubmissionId`, deduplicates the input IDs, and returns results in a single query. This endpoint is stricter than the existing single-submission endpoint and is lecturer/admin-gated. It is called by admin-web (Phase 9) to fetch all grades for a filtered set of submissions.

## Steps

1. In `GradesEndpoints`, add a new GET route for the batch endpoint: `GET /grades/final` (note: different path from the existing `GET /grades/{submissionId}/final` single endpoint to avoid routing conflicts). Use lecturer/admin-gated authorization.

2. Parse the query string `?submissionIds=...` to extract a list of submission IDs. Support both comma-separated (`submissionIds=id1,id2,id3`) and repeated query parameters (`submissionIds=id1&submissionIds=id2&submissionIds=id3`). Deduplicate the IDs using a `HashSet<Guid>`.

3. If the deduplicated list is empty, return `Results.Ok(Array.Empty<FinalGradeResponse>())` (empty array).

4. Query the database using a single EF Core query: `db.FinalGrades.AsNoTracking().Where(f => ids.Contains(f.SubmissionId)).OrderByDescending(f => f.CreatedAt).ToListAsync()`. This returns all FinalGrades for the requested submissions, not just the latest per submission.

5. Post-process the results in-memory to pick the latest FinalGrade per SubmissionId: group by SubmissionId, take the first (already ordered by CreatedAt desc), and flatten back to a list. Alternatively, use LINQ `.GroupBy(f => f.SubmissionId).Select(g => g.First())` if the ORDER BY is applied before the grouping in EF.

6. Return the list of FinalGrades as JSON. Define a response record (e.g., `FinalGradeResponse(Guid SubmissionId, Guid FinalGradeId, decimal FinalScore, DateTimeOffset CreatedAt)`) with only the fields needed by admin-web; omit sensitive fields like `CreatedByUserId` if appropriate.

7. Create xUnit tests covering:
   - Single SubmissionId: returns the latest FinalGrade for that submission
   - Multiple SubmissionIds: returns the latest FinalGrade for each (sorted by SubmissionId or as-is)
   - Deduplicated input: submitting the same ID twice (or as comma-separated and repeated param) returns one result, not duplicates
   - Submissions with no published grade: omitted from the response (no null entries)
   - Empty input (no IDs): returns empty array
   - Large batch (100+ IDs): all returned in a single query (verify via query profiler or mock)
   - Unauthenticated caller: returns 401
   - Student role (not lecturer/admin): returns 403

8. Manual test: publish grades for 5 submissions; call `GET /grades/final?submissionIds=id1,id2,id3`; verify only 3 results are returned (for the requested IDs), each is the latest grade for that submission, and no duplicates appear even if an ID is repeated.

## Success Criteria

- GET /grades/final endpoint exists (lecturer/admin-gated)
- Accepts query parameter `?submissionIds=...` (comma-separated or repeated)
- Deduplicates input IDs (using HashSet or LINQ Distinct)
- Queries database with `WHERE id IN (...)` in a single EF query (never loops)
- Returns latest FinalGrade per SubmissionId (if multiple grades exist for one submission)
- Omits submissions with no published grade from the response
- Returns empty array for empty input
- Response includes SubmissionId, FinalGradeId, FinalScore, CreatedAt (or similar relevant fields)
- Unauthenticated callers get 401
- Non-lecturer/non-admin roles get 403
- Unit tests pass all scenarios (single, multiple, deduplication, no grade, empty, auth)
- Grading service compiles and starts without errors
- Manual test: batch call returns only requested submissions with latest grades

## Risks

- **Submissions Without Grades** — If a submission has no FinalGrade row, it's omitted from the response. The FE must handle this (show blank score for missing entries). *Mitigation:* Document this behavior in the endpoint's summary. The FE's join logic (Phase 9) will handle it by left-joining submissions to grades and showing blank if no grade is returned.
- **Large Batch Performance** — If a caller requests 10000+ submission IDs in a single batch, the SQL `IN (...)` clause could be very large. Some databases have limits on clause size. *Mitigation:* Spec assumes admin-scale requests (10–1000 IDs per assignment). Add a comment noting this. If real usage exceeds 5000 IDs, implement pagination in a future release.
- **Query Order Ambiguity** — If two FinalGrades for the same submission have the same CreatedAt timestamp (unlikely but possible in fast systems), the "latest" is ambiguous. *Mitigation:* Add `ThenBy(f => f.Id)` after `OrderByDescending(f => f.CreatedAt)` to break ties deterministically.

