# Phase 11: User Registration Form (MSSV & Class)

## Requirements

Update the user-web registration form to include an MSSV (StudentCode) text input field and a Class dropdown (populated anonymously from GET /catalog/classes before the user has a JWT). Both fields are optional at the form level, but if a student fills in both and selects a valid class, they are persisted via the Phase 3 registration endpoint. After successful registration, display a confirmation message showing the student's class (if selected).

## Steps

1. Update `fe/user-web/src/services/authService.ts`:
   - Modify the `signUpWithEmail()` function to accept optional `studentCode` and `classId` parameters
   - Pass these fields in the POST /identity/auth/register request body alongside email, password, fullName, role
   - Ensure the updated RegisterRequest payload matches the Phase 3 backend contract

2. Create a new service file `fe/user-web/src/services/classService.ts` with a function:
   - `async function getClasses()`: calls `GET /catalog/classes` (no auth required) and returns an array of classes (Id, Name) — this call happens before the user has a JWT, so it's truly anonymous

3. Update `fe/user-web/src/pages/LoginPage.tsx` (which currently shows both login and signup modes):
   - In the signup form section, add two new fields below the Role selector:
     a. StudentCode (TextInput, placeholder: "e.g., 1A2B3C4D", optional, appears only in signup mode)
     b. Class (SelectInput with options loaded from classService.getClasses(), optional, appears only in signup mode)
   - The Class dropdown loads on component mount (or when signup mode is first entered) via useEffect/useState pattern
   - While loading, show a spinner or "Loading..." placeholder in the Class dropdown
   - If loading fails, show an error message: "Could not load classes. You can skip this field for now." with a Retry button
   - The class is optional (include a "None / Skip" or empty value at the top of the dropdown)

4. Update the signup form submission logic:
   - Extract StudentCode and ClassId from the form state
   - Pass them to signUpWithEmail() along with other fields
   - Handle the Phase 3 validation errors:
     a. If the backend returns 400 with "Class not found", show the error message: "Selected class not found. Please try again or contact your administrator."
     b. If any other 400 error, display the server's error message

5. On successful registration:
   - After the "Account created" success message (line 39 in current LoginPage), additionally display the student's class if one was selected: "You've been assigned to class: {ClassName}."
   - Or integrate into the success message: "Account created. You're in class {ClassName}." (only if classId was provided)

6. Update the StudentCode field behavior:
   - Make it optional (no `required` attribute)
   - Add client-side validation: if provided, must not be blank or spaces-only
   - Placeholder and label are clear (e.g., "Student ID (MSSV) - optional")

7. Update the registration request handling to handle cases where classId is not provided (both optional):
   - If StudentCode is provided but ClassId is not, that's OK (StudentCode can be set later)
   - If ClassId is provided but StudentCode is not, that's OK (StudentCode can be set later via admin roster edit)
   - Both can be filled in together

8. Handle the cache lag race condition (Phase 3 risk):
   - If a student tries to select a class immediately after it's created (before ClassLecturerAssigned event reaches Identity), the class might not appear in the dropdown yet
   - Mitigation: Add a "Refresh classes" button next to the Class dropdown, or automatically retry fetching the class list if registration fails with 400 "Class not found"
   - Recommend the student retry the registration in a few seconds

9. Add tests covering:
   - Rendering the StudentCode field and Class dropdown in signup mode (not in login mode)
   - Loading classes from GET /catalog/classes
   - Submitting signup with StudentCode and ClassId both filled
   - Submitting signup with StudentCode filled but no ClassId
   - Submitting signup with no StudentCode but ClassId filled
   - Submitting signup with neither StudentCode nor ClassId (both optional)
   - Handling 400 error "Class not found" with clear error message
   - Success message shows the assigned class if one was selected

10. Manual test: in a browser, navigate to user-web login page, click "Create account", select a role (student), enter email/password/fullName, fill in MSSV, select a class from the dropdown, click "Create account", verify success message shows the assigned class, then log in and verify the student can see their MSSV/class in the dashboard (if there's such a view).

## Success Criteria

- `classService.ts` exists in user-web with getClasses() function
- `authService.signUpWithEmail()` accepts optional `studentCode` and `classId` parameters
- LoginPage signup form includes StudentCode (TextInput) and Class (SelectInput) fields
- Class dropdown loads classes from GET /catalog/classes (anonymous, no JWT)
- Class dropdown shows a "None / Skip" or empty option
- StudentCode field is optional, with client-side validation (non-empty if provided)
- On signup form submit, both fields are passed to the backend
- On successful registration, success message includes the assigned class (if provided)
- On error (400 "Class not found"), error message is displayed clearly
- Loading spinner shown while classes are fetching
- Retry button available if class loading fails
- Unit/component tests pass (render, load, submit, errors, success)
- user-web compiles and builds without errors
- Manual test: register with MSSV and class, verify success message, verify student is stored with those values in the admin roster

## Risks

- **Network Error on Class Fetch** — If the backend is unreachable when the signup form loads, the class dropdown fetch fails. *Mitigation:* Add a Retry button and allow the form to submit without selecting a class (both fields are optional). User can fill in class later via admin edit.
- **Stale Class List During Signup** — If a class is deleted between the time the class list is fetched and the form is submitted, the student might select a now-deleted class, resulting in a 400 error. *Mitigation:* Spec accepts this as a rare race. The error message guides the student to retry or contact admin. Add logging so admins can monitor for this scenario.
- **User Confusion on "Optional" Fields** — Students might not understand that MSSV and Class are optional, and might skip them incorrectly. *Mitigation:* Add clear labels: "Student ID (MSSV) - optional" and "Class - optional". Add a note: "You can fill these in later if you're not sure which class you belong to."
- **Accessibility: Class Dropdown Label** — Ensure the Class dropdown is properly labeled with `<label>` for screen readers. *Mitigation:* Use the Field component's label prop consistently (matching the existing pattern in LoginPage).

