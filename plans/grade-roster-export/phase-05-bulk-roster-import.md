# Phase 5: Bulk Roster Import Endpoint

## Requirements

Implement a POST endpoint in Identity that accepts a file upload (Excel/CSV) with columns for Email, StudentCode, and ClassName, processes rows atomically with authorization checks, and returns a detailed report of updated and skipped rows (with skip reasons). Each row is processed independently; a lecturer can only update rows for students they have a relationship with, applying the same authorization logic as Phase 4's PATCH endpoint.

## Steps

1. Create a new endpoint POST /users/bulk-import in the Identity UsersEndpoints group (lecturer/admin-gated via `RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"))`). Accept a multipart form with a single file field (accept `.xlsx`, `.xls`, `.csv`).

2. Parse the file to extract rows, with support for header-name-mapped columns (not positional). For Excel files, use a library like EPPlus or ClosedXML to read sheets; for CSV, use CsvHelper or similar. The first row is treated as a header; look for columns named "Email", "StudentCode", "ClassName" (case-insensitive). If any required column is missing, return 400 with "Invalid file format: missing column Email" or similar.

3. For each data row (starting from row 2):
   a. Extract Email, StudentCode, ClassName
   b. Normalize Email to lowercase and trim (same as registration flow)
   c. Resolve ClassName to a ClassId by querying ClassLecturerCache case-insensitively (e.g., `FirstOrDefaultAsync(c => c.ClassName.ToLower() == className.Trim().ToLower())`)
   d. If ClassName is unresolvable, skip the row with reason "unknown class" (store the row number and reason for the report)
   e. Resolve Email to an existing User (e.g., `FirstOrDefaultAsync(u => u.Email == email)`)
   f. If Email is unresolvable, skip the row with reason "email not registered"
   g. Check authorization for the current caller to edit this User (using the Phase 4 authorization helper). If denied, skip the row with reason "not authorized for this student"
   h. If all checks pass, update the User's StudentCode and ClassId and mark as "updated" for the report

4. Collect all row results in a list: each result contains (row number, email, status: "updated" or "skipped", skip reason if skipped).

5. After processing all rows (all validation and authorization checks), apply the updates atomically: call `db.SaveChangesAsync()` once to persist all updated users in a single transaction. If SaveChangesAsync fails (DB error), return 500 and roll back all changes.

6. Return a successful 200 response with a summary: `{ totalRows: N, updatedCount: M, skippedCount: N-M, details: [{ rowNumber, email, status, reason }] }`.

7. Create xUnit tests covering:
   - File parsing (Excel and CSV with header-mapped columns)
   - Happy path: 10 rows, all match existing users and known classes, all authorized → all 10 updated
   - Mixed results: 10 rows, 7 updated, 1 email not registered, 1 unknown class, 1 not authorized → report with correct reasons
   - Authorization enforcement: lecturer uploading a file with rows outside their scope → those rows skipped with "not authorized" reason
   - Admin uploading the same file → all rows updated (no authorization restrictions)
   - Invalid file format (missing column) → 400 error before processing any rows
   - CSV and Excel file format both handled correctly

8. Document the expected file format in the endpoint's summary comment (columns: Email, StudentCode, ClassName; case-insensitive header names; StudentCode can be blank/empty to leave it unchanged).

9. Manual test: create a file with 5 students (3 matching, 2 not); upload as lecturer; verify the report shows correct skip reasons; verify the 3 matched students are updated in the database.

## Success Criteria

- POST /users/bulk-import endpoint exists (lecturer/admin-gated)
- Accepts file uploads (Excel/CSV)
- Parses header-name-mapped columns (Email, StudentCode, ClassName — case-insensitive)
- Returns 400 if required columns are missing
- Resolves ClassName to ClassId using ClassLecturerCache (case-insensitive lookup)
- Resolves Email to existing User (case-insensitive)
- Checks authorization per row using Phase 4 helper
- Updates matching rows atomically in a single `SaveChangesAsync()` call
- Returns detailed report with row number, email, status (updated/skipped), skip reason
- Skipped rows have correct reasons: "email not registered", "unknown class", "not authorized for this student"
- Lecturer can only update rows they have a relationship with
- Admin can update all rows regardless of relationship
- Unit tests pass all scenarios (CSV, Excel, mixed results, auth checks, invalid format)
- Identity service compiles and starts without errors
- Manual test: upload a file, verify correct rows are updated and report is accurate

## Risks

- **Large File Upload Performance** — Processing 10000+ row files synchronously could block the request thread and exceed HTTP timeouts. *Mitigation:* Spec assumes class-sized rosters (20–50 rows). Add a comment documenting this assumption. If real deployments exceed 500 rows regularly, refactor to a background job (Hangfire) in a future release.
- **Memory Usage** — Parsing a large Excel file into memory could exhaust RAM if the file is very large. *Mitigation:* Use streaming/chunked parsing if the library supports it. For typical class rosters, this is not a concern.
- **Partial Failure on SaveAsync** — If `SaveChangesAsync()` fails after processing 500 rows, all changes are rolled back, but the report has already been generated. *Mitigation:* Call `SaveChangesAsync()` before generating the report, and catch any DB exceptions to return 500 with a clear error message. The FE can retry the upload.
- **Case-Insensitive ClassName Lookup Ambiguity** — If two classes have the same name with different casing ("Class A" and "CLASS A"), the lookup is ambiguous. *Mitigation:* Database should enforce unique class names (add constraint in Phase 1 if clarified later). For now, assume class names are unique. If lookup returns multiple matches, pick the first and log a warning.

