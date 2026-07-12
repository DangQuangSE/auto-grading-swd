# Phase 8: Admin Student Roster UI

## Requirements

Create an admin-web page that lists all registered students with their email, full name, MSSV (StudentCode), and resolved class name. Allow inline or modal-based editing of individual students' MSSV and Class (via PATCH /users/{userId}). Include a link to the Bulk Import page (Phase 9) for mass updates. Display authorization errors clearly if a lecturer tries to edit a student outside their scope.

## Steps

1. Create a new service file `fe/admin-web/src/services/rosterService.ts` with functions:
   - `async function listUsers()`: calls `GET /identity/users` and returns an array of users (Id, Email, FullName, StudentCode, ClassId, ClassName)
   - `async function getUser(userId: string)`: calls `GET /identity/users?ids={userId}` and returns a single user
   - `async function updateUser(userId: string, params: { studentCode?: string; classId?: string })`: calls `PATCH /identity/users/{userId}` with the provided fields and returns the updated User
   - `async function fetchClasses()`: calls `GET /catalog/classes` (anonymously, no auth) and returns an array of classes (Id, Name) — reuse from Phase 7 or create here

2. Create a custom hook `fe/admin-web/src/hooks/useRoster.ts` (following the `useRubrics()` pattern) that manages fetching and caching the student list, with methods to refetch or update a single student.

3. Create a new React component `fe/admin-web/src/pages/RosterPage.tsx` (or `StudentRosterPage.tsx`) that:
   - Renders a table listing all students (Email, Full Name, MSSV, Class Name)
   - Each row includes an Edit button (or the row is clickable)
   - On Edit, opens a modal with fields for StudentCode and Class (dropdown or text input)
   - The Class dropdown is populated from the classes list (fetched via rosterService.fetchClasses())
   - On Save in the modal, calls rosterService.updateUser() with the new values
   - On success, closes the modal and updates the table row immediately (reuse the optimistic update pattern from admin-web's existing code if available)
   - On error (403, 400, 500), displays the error message in the modal (e.g., "Not authorized for this student" for 403, "Class not found" for 400)
   - Optionally includes filters (by Email, by Class, by MSSV) applied client-side on the loaded list
   - Includes a link/button to the Bulk Import page

4. Implement pagination if the student list is large (e.g., >100 students). Fetch page-by-page or load all and paginate client-side.

5. In the edit modal:
   - Make StudentCode field optional (can be left blank)
   - Make Class dropdown optional (can be "None" or empty)
   - Validate client-side: non-empty StudentCode if provided (no spaces-only values)
   - On submit, call updateUser() with only the fields that changed (optimization)

6. Handle loading states and errors:
   - Show a spinner/skeleton while loading the student list
   - Disable the Edit button while a save is in progress
   - Display 403 errors with a clear message: "You are not authorized to edit this student"
   - Display 400 errors with the server's reason: "Class not found" or similar
   - Display server 500 errors with a generic "Something went wrong" message and a Retry button

7. Add a route in `fe/admin-web/src/App.tsx` to mount this page at `/admin/roster` or similar, with a link from the Class Management page (Phase 7).

8. Create component tests covering: rendering the student list, opening the edit modal, updating a student, error handling (403, 400, 500), filtering.

9. Manual test: as an admin, navigate to the roster page, open a student's edit modal, change their MSSV and Class, verify the table updates. Try editing a student as a lecturer with no relationship to them (should fail with 403 if authorization checks are working; this requires setting up test data with lecturers and classes).

## Success Criteria

- `rosterService.ts` exists with listUsers, getUser, updateUser, fetchClasses functions
- `useRoster.ts` custom hook exists and manages roster state
- `RosterPage.tsx` component exists and renders
- Student table displays Email, Full Name, MSSV, Class Name
- Each row has an Edit button/link
- Edit modal includes StudentCode and Class dropdown fields
- Modal form validates input (StudentCode is optional but non-empty if provided)
- On Save, calls updateUser() and updates the table row on success
- On error, displays error message in modal (403, 400, 500 all handled)
- Pagination or load-all strategy works for large student lists
- Filtering by Email/Class/MSSV works (client-side or server-side as appropriate)
- Link/button to Bulk Import page is present
- Loading states and disabled buttons while fetching/saving
- Route in App.tsx at `/admin/roster` or similar
- Component tests pass (render, edit, errors, filters)
- admin-web compiles and builds without errors
- Manual test: edit student, verify table updates; attempt unauthorized edit as lecturer (should fail with 403)

## Risks

- **Large Student List Performance** — If the school has 5000+ students, loading and rendering them all could be slow. *Mitigation:* Implement server-side pagination in Phase 4 (GET /users with page/limit parameters), or use virtual scrolling in React (via a library like `react-window`). For now, assume <500 students per admin session.
- **Stale Class List** — If a new class is created after the page loads, it won't appear in the edit modal's dropdown until a refresh. *Mitigation:* Add a "Refresh classes" button in the modal, or fetch the list every time the modal opens.
- **Race Condition on Concurrent Edits** — If two admins edit the same student simultaneously, one edit might be overwritten. *Mitigation:* This is an accepted concurrency concern; add optimistic locking (ETag or version field) in a future release. For now, accept last-write-wins.
- **Authorization Regression** — If the Phase 4 PATCH authorization logic is broken, this UI will silently fail to enforce it (users will see 403 errors but the code will appear correct). *Mitigation:* Ensure Phase 4 unit tests are thorough. Add an integration test here that verifies a lecturer cannot edit an unrelated student.

