# Phase 10: Admin Grade Table & Export UI

## Requirements

Create an admin-web page that displays a filterable grade table for a selected assignment. The table is built by joining three backend queries (submissions, batch grades, batch users) client-side and shows student name, MSSV, class name, and published final score. Support filtering by MSSV (partial/exact match) and Class name (partial/exact match, combined with AND logic). Export the currently-visible filtered rows to a real `.xlsx` file via SheetJS.

## Steps

1. Create a new service file `fe/admin-web/src/services/gradeExportService.ts` with functions:
   - `async function getAssignments()`: calls `GET /catalog/assignments` and returns a list of assignments (Id, Name, SubjectId)
   - `async function getSubmissions(assignmentId: string)`: calls `GET /submissions?assignmentId={assignmentId}` and returns an array of submissions (Id, StudentId, AssignmentId, UploadedAt)
   - `async function batchGetGrades(submissionIds: string[])`: calls `GET /grading/grades/final?submissionIds=...` with the deduplicated list and returns grades (SubmissionId, FinalGradeId, FinalScore, CreatedAt)
   - `async function batchGetUsers(userIds: string[])`: calls `GET /identity/users?ids=...` with the deduplicated list and returns users (Id, Email, FullName, StudentCode, ClassName)

2. Create a custom hook `fe/admin-web/src/hooks/useGradeTable.ts` that:
   - Manages the state for selected assignment, submissions, grades, users, and joined table
   - On assignment selection, fetches submissions, then in parallel fetches grades and users
   - Performs a client-side join: map submissions to grades (by SubmissionId), map submissions to users (by StudentId), combine into one table
   - Returns the joined table and any loading/error states

3. Create a new React component `fe/admin-web/src/components/GradeTable.tsx` (or in a new page `GradeExportPage.tsx`) that:
   - Renders a dropdown to select an assignment (initially empty, loads on mount or on demand)
   - Below that, renders a table with columns: Student Name, MSSV, Class Name, Final Score
   - Initially, the table is empty (before an assignment is selected)
   - On assignment selection, loads submissions, grades, and users (show a spinner during fetch)
   - Populates the table with the joined data
   - For rows with no published grade, shows a blank or "Not graded" placeholder in the Final Score column
   - Includes two filter inputs:
     a. MSSV filter (text input, case-insensitive, partial match)
     b. Class Name filter (text input, case-insensitive, partial match)
   - Filtering logic: when both filters are provided, rows must match BOTH (AND logic)
   - On filter change, updates the table in real-time (filter client-side)

4. Add an Export button above the table:
   - On click, generates a `.xlsx` file using SheetJS (`xlsx` package)
   - The file contains a header row (Student Name, MSSV, Class Name, Final Score)
   - Followed by the currently-filtered rows (exactly what's visible in the table)
   - Filename format: `{assignment-name}-grades-{YYYY-MM-DD}.xlsx` or similar
   - Triggers a browser download

5. Implement the Excel export logic using SheetJS:
   ```
   const ws = XLSX.utils.json_to_sheet(filteredRows, {
     header: ["studentName", "mssv", "className", "finalScore"],
     ...other options
   });
   const wb = XLSX.utils.book_new();
   XLSX.utils.book_append_sheet(wb, ws, "Grades");
   XLSX.writeFile(wb, filename);
   ```
   Ensure the header row is visible and formatted nicely (optional: add formatting like bold header, column width auto-fit).

6. Handle edge cases:
   - No assignment selected: show "Select an assignment to view grades"
   - Assignment selected but no submissions: show "No submissions for this assignment"
   - Submissions loaded but no grades published: show table with all rows, blank Final Score column
   - Filter matches no rows: show "No results match the current filters"
   - Export with no rows: either disable the Export button or export an empty file (with header only)

7. Loading and error states:
   - Show a spinner while fetching assignment list
   - Show a spinner while fetching submissions/grades/users after assignment selection
   - If any fetch fails (500 error), display error message and allow retry
   - Disable buttons during fetch

8. Add a route in `fe/admin-web/src/App.tsx` at `/admin/grades` or `/admin/grade-export`, with a link from the main admin menu or the Roster page.

9. Create component tests covering:
   - Rendering assignment dropdown
   - Selecting an assignment and loading the table
   - Filtering by MSSV alone, by Class alone, by both (AND logic)
   - Exporting to Excel (verify file structure and content)
   - Error handling (network errors, 500 server errors)
   - Edge cases (no submissions, no grades, no filter matches)

10. Manual test: select an assignment with graded submissions, apply various filters, export the table, open the `.xlsx` file in Excel and verify it contains the correct filtered rows with headers.

## Success Criteria

- `gradeExportService.ts` exists with getAssignments, getSubmissions, batchGetGrades, batchGetUsers functions
- `useGradeTable.ts` hook exists and manages state
- `GradeTable.tsx` component exists and renders
- Assignment dropdown loads and displays available assignments
- On assignment selection, fetches data and renders grade table
- Table shows columns: Student Name, MSSV, Class Name, Final Score
- Rows with no published grade show blank/placeholder in Final Score
- MSSV filter (text input) works, case-insensitive, partial match
- Class Name filter (text input) works, case-insensitive, partial match
- Both filters combined use AND logic (rows match both filters)
- Filter updates table in real-time
- Export button generates `.xlsx` file with header row + filtered rows
- Exported file has correct filename format (assignment-name-grades-YYYY-MM-DD.xlsx)
- Exported file opens correctly in Excel/LibreOffice
- Loading spinners shown during fetch
- Error messages displayed on network/server errors
- Retry functionality available on errors
- Edge cases handled (no assignment, no submissions, no grades, no filter matches)
- Route in App.tsx at `/admin/grades` or similar
- Component tests pass (select, filter, export, errors, edge cases)
- admin-web compiles and builds without errors
- Manual test: select assignment, apply filters, export, verify `.xlsx` file contains correct filtered rows

## Risks

- **Stale Assignment List** — If a new assignment is created after the page loads, it won't appear in the dropdown until a refresh. *Mitigation:* Add a "Refresh" button next to the assignment dropdown, or fetch the list on every component mount (accept minor performance cost for freshness).
- **Large Class Grades Performance** — If an assignment has 5000+ submissions, loading and filtering them all in memory could be slow. *Mitigation:* Spec assumes class-scale assignments (<500 submissions). For larger datasets, implement server-side filtering (add query parameters to GET /submissions and batch endpoints) in a future release.
- **Excel Export Formatting** — Very wide tables or many rows could produce a messy Excel file. *Mitigation:* SheetJS can auto-fit column widths and freeze the header row (optional enhancements). For MVP, basic export is acceptable.
- **No Authorization on Export** — Any lecturer with access to this page can export grades for any assignment, even if they don't teach the class. *Mitigation:* Spec does not require per-assignment authorization (only requires lecturer/admin role). If school policy requires stricter control (lecturer can only see grades for their own students), implement that in a future release by filtering the table server-side or adding an authorization check.
- **Filter Confusion (AND vs OR)** — Users might expect OR logic instead of AND. *Mitigation:* Add a clear label: "Showing rows where MSSV contains X AND Class contains Y".

