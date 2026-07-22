# Phase 6: Student Profile Enrollment UI

## Stories

- **P1:** student chooses one valid class for each subject currently open for registration.

## Goal

Add an authenticated Profile page that shows account identity and manages subject-specific class selections through API-backed dropdowns.

## Steps

1. Add `/profile` to student routes and navigation with a suitable profile icon and active state.
2. Create typed services/hooks for open subjects, subject-filtered classes, paginated self enrollments, and self upsert; follow pages of at most 100 until pagination metadata reports completion.
3. Display the authenticated email/identity already present in the session plus a `Môn học và lớp` section.
4. Render current enrollments, including closed subjects as read-only rows. Offer editing only while their subjects are open.
5. Populate Subject and Class dropdowns exclusively from API results. On Subject change, immediately clear the selected Class and fetch only that Subject's classes.
6. Prevent save until selections are valid; submit current base64 row version when editing (null only for a new subject), disable duplicate submissions, and show loading/validation/conflict states.
7. After save, invalidate/refetch enrollments and preserve other subject rows.
8. On `409`, refetch the enrollment/version and require explicit confirmation before another save.
9. Add UI tests for two enrollments, paginated reads, open-only choices, selection reset, closed read-only state, version conflict refresh, empty lists, and navigation accessibility.

## Success Criteria

- Profile navigation is available after login.
- Student can save distinct classes for at least two subjects.
- Subject and Class codes cannot be typed freely.
- Changing Subject clears an incompatible Class.
- Closed enrollment remains visible but cannot be changed.

## Design Constraints

Preflight: Reuse user-web session, React Query, route/nav, form, panel, and message conventions. Profile uses only authenticated open-subject, subject-class, and self-enrollment APIs; no StudentId or anonymous Class lookup is used. Each API page remains capped at 100 and dependent Class state is cleared on Subject changes. Applicable rules: FE_STATE_OWNERSHIP_CLEAR, FE_ACCESSIBLE_INTERACTIVE_ELEMENT, TS_PROMISE_HANDLED, DOM_BACKEND_AUTHORITY, API_PAGINATION_BOUNDED, CORR_DEFINED_BRANCH_BEHAVIOR. Tests declined by user; quality gate required.

- Profile uses authenticated APIs; it must not depend on the anonymous legacy `/catalog/classes` response.
- The client never sends a Student ID.
- Preserve existing Submit and Result routes/layout behavior.
- Avoid presenting Identity `User.ClassId` as the student's subject enrollment.

## Quality and Testing State

- quality: approved (`quality/phase-06-student-profile-ui-receipt.json`)
- testing: not started (skipped by user for cook; user will test later)
