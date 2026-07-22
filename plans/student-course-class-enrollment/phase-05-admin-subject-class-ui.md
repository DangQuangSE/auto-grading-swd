# Phase 5: Admin Subject and Class UI

## Stories

- **P1:** admin controls registration state and subject ownership of classes.
- **P2:** admin views and corrects a student's enrollment.

## Goal

Extend existing admin screens so all inputs come from Catalog and legacy rows can be repaired explicitly.

## Steps

1. Update subject types/services/hooks for registration status while preserving Subject creation for lecturer/admin; expose the status mutation only to admin UI/roles.
2. Add an Open/Closed control and clear pending/error/success states to `SubjectsPage`; invalidate/refetch subject queries after mutation.
3. Update class types/services/hooks so create requires a Subject dropdown populated from the admin subject API.
4. Display Subject on `ClassManagementPage` and provide an explicit edit path to assign it while preserving lecturer reassignment. Disable reassignment after enrollment and surface backend `409` on stale state.
5. Mark null-subject legacy classes as `Unassigned (legacy)` and require an explicit admin choice rather than guessing from class name.
6. Reset Class selection if a changed Subject makes the current value invalid; use dropdowns only, never free-text Subject IDs.
7. Add a bounded enrollment correction view (standalone or integrated into Roster): filter by student, display subject/class, choose a replacement from that subject, and submit the API row version.
8. On enrollment `409`, retain the choice, refetch current row/version, and require an explicit retry; never silently overwrite.
9. Add tests for open/close, required subject, immutable ownership, legacy assignment, enrollment correction while closed, isolation across subjects, and conflict refresh.

## Success Criteria

- Admin can open/close registration from the subject screen.
- Admin cannot submit a new class without selecting a subject.
- Existing null-subject classes are visible and repairable.
- UI data refreshes after successful mutations and communicates conflicts/errors.
- Admin can find and correct one student's enrollment without changing another subject row.

## Design Constraints

Preflight: Reuse admin-web React Query, service, form, message, table, and session-role conventions. Backend remains authoritative; UI hides admin-only controls for convenience, uses bounded API pages, and never accepts free-text Subject/Class IDs except the explicit student Guid filter for admin lookup. Applicable rules: FE_STATE_OWNERSHIP_CLEAR, FE_ACCESSIBLE_INTERACTIVE_ELEMENT, TS_PROMISE_HANDLED, DOM_BACKEND_AUTHORITY, API_PAGINATION_BOUNDED, CORR_DEFINED_BRANCH_BEHAVIOR. Tests declined by user; quality gate required.

- Reuse existing admin API client, React Query, field, message, and button patterns.
- UI authorization is convenience only; backend role checks remain authoritative.
- Do not remove or repurpose lecturer assignment or roster links.

## Quality and Testing State

- quality: approved (`quality/phase-05-admin-subject-class-ui-receipt.json`)
- testing: not started (skipped by user for cook; user will test later)
