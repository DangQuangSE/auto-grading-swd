# Phase 9: Admin Bulk Roster Import UI

## Requirements

Create an admin-web page that allows uploading an Excel or CSV file containing a class roster (Email, StudentCode, ClassName columns). Display a detailed report of the upload results showing how many students were updated, how many rows were skipped, and the reason for each skipped row (email not registered, unknown class, not authorized). Link back to the Roster page.

## Steps

1. Create a new service file `fe/admin-web/src/services/bulkImportService.ts` with a function:
   - `async function uploadRosterFile(file: File)`: calls `POST /identity/users/bulk-import` with the file as multipart form data and returns the result `{ totalRows, updatedCount, skippedCount, details: [{ rowNumber, email, status, reason }] }`

2. Create a new React component `fe/admin-web/src/pages/BulkImportPage.tsx` that:
   - Renders a file input field (accept `.xlsx`, `.xls`, `.csv`)
   - Renders a submit button (Upload or Import)
   - Initially, before upload, shows instructions (e.g., "File must contain columns: Email, StudentCode, ClassName")
   - On file selection, optionally preview the first few rows (parse the file client-side using `xlsx` library to show expected structure)
   - On Upload, sends the file to bulkImportService.uploadRosterFile()
   - On success (200 response), displays a success summary: "Successfully updated N students. M rows were skipped."
   - Below the summary, displays a detailed report table: rowNumber, email, status (updated/skipped), skip reason
   - Color-code or icon-mark the status (green checkmark for updated, red X for skipped)
   - On error (400, 500), displays the error message (e.g., "Invalid file format: missing column Email")
   - Includes a link back to the Roster page

3. Implement file validation (client-side):
   - Check file extension (must be .xlsx, .xls, or .csv)
   - Reject files larger than a reasonable size (e.g., 10 MB) with a clear error
   - Validate that required columns are present (optional, since the server also validates)

4. For the file preview (optional but helpful UX):
   - Use the `xlsx` library (already available via `npm`) to parse the uploaded file
   - Display the first 3–5 data rows in a small table (Email, StudentCode, ClassName)
   - Show a note: "Preview: first 5 rows shown"

5. On upload:
   - Disable the submit button and file input while uploading (show a spinner)
   - Estimated time: <1 second for class-sized rosters, so no need for a progress bar
   - After upload completes, re-enable the input for another upload (allow batch multiple files in sequence)

6. For the detailed report table:
   - Render as a sortable table (by rowNumber, status, or reason)
   - Show a summary row at the top with the counts (Total rows: X, Updated: Y, Skipped: Z)
   - Optionally allow filtering (show only skipped rows, for example) or searching by email
   - For skipped rows, show the reason prominently (e.g., "email not registered")

7. Handle errors gracefully:
   - File format errors (400): "Invalid file format: {server-provided reason}"
   - Server errors (500): "Upload failed. Please try again or contact support."
   - Network errors: "Connection lost. Please check your internet and retry."

8. Add a route in `fe/admin-web/src/App.tsx` at `/admin/bulk-import` or similar, with a link from the Roster page (Phase 8).

9. Create component tests covering: file input interaction, preview generation, successful upload with mixed results, error handling, filtering of results table.

10. Manual test: create a CSV file with 10 rows (5 matching students and known classes, 3 with unknown classes, 2 with unregistered emails). Upload the file and verify the report shows correct counts and reasons.

## Success Criteria

- `bulkImportService.ts` exists with uploadRosterFile function
- `BulkImportPage.tsx` component exists and renders
- File input accepts .xlsx, .xls, .csv files
- Client-side file validation (extension, size) works
- File preview displays first few rows with Email, StudentCode, ClassName
- Upload sends file to `/identity/users/bulk-import` via multipart form
- On success (200), displays summary: "Updated N students, M rows skipped"
- Detailed report table shows rowNumber, email, status, reason for each row
- Skipped rows show reason: "email not registered", "unknown class", "not authorized for this student"
- Updated rows show "updated" status (visually distinct, e.g., green checkmark)
- Sorting/filtering of report table works (if implemented)
- On error (400, 500), displays error message clearly
- Buttons disabled during upload, re-enabled after completion
- Link back to Roster page is present
- Route in App.tsx at `/admin/bulk-import` or similar
- Component tests pass (file input, preview, upload, errors, report)
- admin-web compiles and builds without errors
- Manual test: upload mixed file, verify accurate report with correct skip reasons

## Risks

- **File Size Limits** — Very large files (>100 MB) could crash the browser or exceed server upload limits. *Mitigation:* Implement client-side size check (e.g., reject files >10 MB) with clear error message. Add a server-side limit (e.g., via IFormFile max request size in Program.cs).
- **Excel vs CSV Encoding** — Files with non-ASCII characters (Vietnamese names) might have encoding issues when parsed by `xlsx` or CsvHelper. *Mitigation:* Test with Vietnamese file samples during manual QA. `xlsx` and `CsvHelper` both handle UTF-8 well; ensure files are saved in UTF-8 format.
- **Preview Parsing Different From Server** — If the client-side `xlsx` parser interprets the file differently than the server's parser (ClosedXML or CsvHelper), the preview might mislead the user. *Mitigation:* Make preview optional (or non-blocking). The server's parse is the source of truth; if there's a mismatch, the upload will catch it.
- **Missing Header Row** — If the file doesn't have a header row, both client and server parsing might fail or produce nonsensical results. *Mitigation:* Document that the first row must be a header. The server validates this; if missing, return 400 with "Invalid file format: missing header row".
- **User Confusion on "Not Authorized" Rows** — If a lecturer uploads a file and several rows are skipped with reason "not authorized for this student", the lecturer might not understand why. *Mitigation:* Add a note in the instructions: "You can only update students you have a relationship with (your class or students you've graded)." Make the reason message clickable or expandable to show more details (optional enhancement).

