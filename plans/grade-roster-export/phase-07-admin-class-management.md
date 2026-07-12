# Phase 7: Admin Class Management UI

## Requirements

Create an admin-web page that displays a list of existing Classes, allows admins to create new Classes (specifying name and assigning a lecturer), and allows reassigning a Class's lecturer. The page fetches the class list from Catalog's GET /classes, and uses POST and PATCH endpoints to create/update. Include a link to the Student Roster page (Phase 8).

## Steps

1. Create a new service file `fe/admin-web/src/services/classService.ts` with functions:
   - `async function getClasses()`: calls `GET /catalog/classes` and returns an array of Classes (Id, Name)
   - `async function createClass(params: { name: string; lecturerId: string })`: calls `POST /catalog/classes` and returns the created Class
   - `async function updateClassLecturer(classId: string, lecturerId: string)`: calls `PATCH /catalog/classes/{classId}` with `{ lecturerId }` and returns the updated Class
   - `async function fetchLecturers()`: calls `GET /identity/users?ids=...` with a filter for lecturers (or implement a separate endpoint in Identity that lists all lecturers) and returns an array of lecturers (Id, Email, FullName)

2. Create a new React component `fe/admin-web/src/pages/ClassManagementPage.tsx` (or `ClassesPage.tsx`) that:
   - Renders a table listing all Classes (Id, Name, current Lecturer)
   - Includes a form to create a new Class (inputs for Class Name and a dropdown to select the Lecturer)
   - On successful create, adds the new Class to the table and clears the form
   - Includes an inline edit option or a modal to reassign the Lecturer to a different Class
   - On successful reassign, updates the table immediately
   - Displays any errors (class name already exists, server errors) via FormMessage component
   - Includes a link/button to navigate to the Student Roster page

3. Use the admin-web UI kit patterns: Button, Field (TextInput, SelectInput), Panel, FormMessage, and Pagination if the class list is large. Mirror the style of existing pages (e.g., RubricManagementPage or similar).

4. Fetch the lecturer list on component mount or when the create form is first opened (use a custom hook `useLecturers()` following the `useRubrics()` pattern to cache the list).

5. On the create form submission:
   - Validate that the Class Name is not empty (client-side minimum check)
   - Call `classService.createClass()` with the form values
   - On success, add the returned Class to the local state (table updates immediately)
   - On error, display the error message via FormMessage

6. On the reassign (PATCH) submission:
   - Call `classService.updateClassLecturer()` with the classId and new lecturerId
   - On success, update the table row with the new lecturer name
   - On error, display the error message

7. Handle edge cases: loading states (disable buttons/inputs while fetching), race conditions (disable reassign button if already updating), empty class list (show a helpful message).

8. Add a route in `fe/admin-web/src/App.tsx` (or router config) to mount this page at `/admin/classes` or similar.

9. Create component tests using React Testing Library covering: rendering the class list, creating a new class, reassigning a lecturer, error handling.

10. Manual test: navigate to the class management page, create a new class with a lecturer, reassign the lecturer, verify the changes are reflected immediately in the UI.

## Success Criteria

- `classService.ts` exists with all four functions (getClasses, createClass, updateClassLecturer, fetchLecturers)
- `ClassManagementPage.tsx` component exists and renders
- Class list displays Id, Name, and current Lecturer name
- Create form accepts Class Name and Lecturer selection
- Create form validates input (non-empty name) and calls the service
- On create success, new Class is added to the table and form is cleared
- On create error, error message is displayed via FormMessage
- Reassign form/modal allows selecting a new Lecturer and calling the service
- On reassign success, table is updated immediately
- On reassign error, error message is displayed
- Loading states are shown (buttons disabled, spinner if applicable)
- Link/button to Student Roster page (Phase 8) is present
- Page is accessible via the admin-web routing at `/admin/classes` or similar
- Component tests pass (render, create, reassign, errors)
- admin-web compiles and builds without errors
- Manual test: create and reassign a class, verify changes persist

## Risks

- **Lecturer Dropdown Load** — Fetching the full list of lecturers on every component mount could be slow if there are many lecturers. *Mitigation:* Implement `useLecturers()` hook with caching (store in localStorage or React Query cache) so subsequent mounts use the cached list. Add a refresh button if needed.
- **Stale Lecturer List** — If a new lecturer is registered (via sign-up) after the page loads, they won't appear in the dropdown until a refresh. *Mitigation:* Add a small "Refresh" button next to the lecturer dropdown, or fetch the list periodically (e.g., every 30 seconds).
- **Same-Name Classes** — If two classes are created with identical names, the UI might confuse users. *Mitigation:* This is a backend concern (Phase 1 didn't enforce uniqueness). Add a note in the FE that names should be unique, or implement backend uniqueness in a future iteration.

