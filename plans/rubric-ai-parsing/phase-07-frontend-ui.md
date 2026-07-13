# Phase 7: Frontend UI

## Requirements

Extend the admin-web rubric upload and management interface to show AI-extracted criteria, allow editing before confirmation, provide confirm and unlock buttons, display parsing status, and add a scope selector (with admin-only gating for SchoolWide rubrics). Follow existing patterns in `RubricUploadPage.tsx`, `useRubrics.ts`, and the shared UI component library.

## Steps

1. Update `fe/admin-web/src/services/rubricService.ts` to add three new API client methods: `confirmRubric(rubricId)` (POST to confirm endpoint), `unlockRubric(rubricId)` (POST to unlock endpoint), and `editRubricCriteria(rubricId, criteria)` (PATCH to criteria endpoint).
2. Update the `useRubrics` custom hook in `fe/admin-web/src/hooks/useRubrics.ts` to expose these new methods alongside the existing rubrics query, and add a mechanism to poll/refresh the rubric status (or subscribe to real-time updates if WebSocket is available).
3. On `RubricUploadPage.tsx`, after a successful upload, keep the page visible and add a section that shows the current rubric status: if `Parsing`, display a "Processing..." indicator with a refresh button; if `Draft`, display the criteria list with edit/delete UI and a "Confirm" button; if `Confirmed`, display the criteria as read-only and show an "Unlock" button.
4. Add a scope selector (radio buttons or dropdown) on the upload form with two options: `Lecturer` (default) and `SchoolWide` (disabled unless the current user has `admin` role).
5. Implement the criteria edit UI: allow inline editing of each criterion's name, description, and max score; add an "Add Criterion" row at the bottom; provide a "Delete" button on each row. Post changes to the `editRubricCriteria` endpoint after the user clicks "Save" or each change.
6. Implement the "Confirm" button: when clicked, call the `confirmRubric` endpoint, refresh the rubric status, and transition the UI to show read-only criteria and the "Unlock" button.
7. Implement the "Unlock" button: when clicked, call the `unlockRubric` endpoint, refresh the rubric status, and return to the edit UI.
8. Add a polling/refresh loop (e.g., poll every 2 seconds) to check if the `Parsing` status has transitioned to `Draft` after the AI extraction job completes.
9. Verify the end-to-end flow: upload a rubric, see "Processing...", wait for criteria to appear, edit one criterion, click "Confirm", verify the UI locks into read-only, click "Unlock", verify editing is re-enabled.

## Success Criteria

- `rubricService.ts` has `confirmRubric`, `unlockRubric`, and `editRubricCriteria` methods
- `RubricUploadPage` displays "Parsing..." while `Status = Parsing`
- `RubricUploadPage` displays editable criteria list when `Status = Draft`
- User can add/remove/edit individual criteria and click "Save" to persist
- User can click "Confirm" to transition to `Confirmed` status
- Once `Confirmed`, criteria are displayed as read-only
- "Unlock" button appears only when `Status = Confirmed`
- Clicking "Unlock" re-enables the edit UI
- Scope selector defaults to `Lecturer` and disables `SchoolWide` for non-admin users
- Upload form includes the scope selector before upload
- Rubric status refreshes automatically (via polling or real-time subscription)
- Criteria displayed in the UI match the AI-extracted fields (name, description, max score, order)

## Risks

- **UI/API contract mismatch** — If the backend criteria schema differs from the frontend's expectation, form binding or display might break. *Mitigation:* Use TypeScript interfaces/types that match the `RubricConfirmed` event's criteria structure; validate the API response matches the type before rendering.
- **Stale cached criteria** — If React Query caches the rubric and the status changes in the background, the UI might show outdated criteria. *Mitigation:* Configure the React Query cache to invalidate on confirm/unlock actions; consider real-time WebSocket updates if available.
- **Admin role check on SchoolWide selector** — If the role check is only client-side, a malicious user might send a SchoolWide rubric even if not admin. *Mitigation:* Rely on the backend authorization check (Phase 4) as the source of truth; the UI is just a convenience to prevent accidental clicks. The server will reject SchoolWide confirms from non-admin users.
- **Accessibility of edit UI** — The criteria edit table/form might be hard to navigate for keyboard-only users. *Mitigation:* Follow existing admin-web form patterns (e.g., use `Field`, `Button` components) and ensure tab order is logical; test with keyboard navigation.

---
