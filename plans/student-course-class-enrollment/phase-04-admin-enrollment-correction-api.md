# Phase 4: Admin Enrollment Correction APIs

## Stories

- **P2:** admin views and corrects one student enrollment without changing other subjects.

## Goal

Give administrators a bounded correction workflow independent of student registration status.

## Steps

1. Add admin-only paginated enrollment queries with filters for student, subject, and class; cap page size at 100.
2. Add an admin correction command targeting an explicit enrollment/student-subject pair with replacement `ClassId` and required current base64 `rowVersion` for updates.
3. Validate that Subject and Class exist and match. Admin correction may operate while registration is closed, but must not bypass relational integrity.
4. Ensure correction updates only the requested subject enrollment and cannot mutate other rows for the student.
5. Apply the same row-version update predicate and current-representation `409` behavior used by self-service; include `rowVersion` in every query/command response.
6. Define not-found and conflict responses clearly enough for admin-web to prompt refresh.
7. Add tests for admin success, lecturer/student denial, correction while closed, mismatched class, pagination/filtering, concurrency, and isolation across subjects.

## Success Criteria

- Admin can view and change a student's selected class for one subject.
- Student and lecturer callers receive 403.
- Changing SWD does not change SWR.
- Admin correction works for a closed Subject without reopening student registration.

## Design Constraints

Preflight: Extend the existing Catalog enrollment endpoint group; use admin-only server authorization, bounded DTO projection, one-row correction, row-version conflict semantics, and the existing composite Class/Subject invariant. Do not write Identity or expose unrelated profile data. Applicable rules: SEC_SERVER_SIDE_AUTHZ, CORR_BOUNDARY_VALIDATION, API_PAGINATION_BOUNDED, TXN_RACE_HANDLED_AT_DATA_LAYER, API_NO_DB_SCHEMA_AS_CONTRACT, DOTNET_CANCELLATION_TOKEN_PROPAGATED. Tests declined by user; quality gate required.

- This is an administrative correction, not impersonated self-service.
- Do not write Identity `User.ClassId` as a side effect.
- Responses must not be unbounded and should avoid returning unrelated user profile data owned by Identity.

## Quality and Testing State

- quality: approved (`quality/phase-04-admin-enrollment-correction-api-receipt.json`)
- testing: not started (skipped by user for cook; user will test later)
