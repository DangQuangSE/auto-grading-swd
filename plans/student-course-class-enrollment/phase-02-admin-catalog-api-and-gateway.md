# Phase 2: Admin Catalog APIs and Gateway Boundary

## Stories

- **P1:** admin opens/closes subjects and assigns every new/edited class to one valid subject.

## Goal

Expose explicit admin catalog commands and safe student lookup routes without broadening the existing anonymous gateway exception.

## Steps

1. Extend Subject create/read DTOs with registration status while keeping create default `closed`; add an admin-only status update endpoint and run open/close in the same serializable Subject-row protocol used by enrollment writes.
2. Preserve subject creation for both `admin` and `lecturer`; keep every new Subject `closed`. Restrict registration status changes to `admin` only.
3. Extend Class admin DTOs and create/update handlers with `SubjectId`. Validate non-empty IDs and reject missing subjects before saving.
4. Support explicit assignment of legacy null-subject classes through the admin update path. Reject Subject reassignment with `409` once the Class has any enrollment; preserve lecturer reassignment and `ClassLecturerAssigned` event behavior.
5. Add authenticated read endpoints for admin class details and for student registration options: open subjects only, and classes filtered by the selected Subject.
6. Ensure the subject-class endpoint excludes legacy classes with null `SubjectId` and classes belonging to other subjects.
7. Audit Gateway configuration. Keep the exact anonymous `GET /catalog/classes` route solely for the legacy registration form, return only its current safe fields, and ensure all new routes match the authenticated catch-all route. Mirror routing changes in every environment-specific gateway configuration.
8. Add endpoint tests proving admin/lecturer may create a default-closed Subject, lecturer/student cannot open or close it, plus filtering, invalid Subject IDs, cross-subject filtering, null legacy rows, immutable ownership after enrollment, normalized duplicate class codes, and anonymous route response shape.

## API Contract Direction

- `PATCH /subjects/{subjectId}/registration` — admin-only `{ status }`.
- `GET /subjects/open-for-registration` — authenticated student-safe options, paginated at at most 100.
- `GET /subjects/{subjectId}/classes` — authenticated, subject-filtered options, paginated at at most 100.
- Existing `/classes` admin commands gain `SubjectId`; exact final paths should follow current endpoint conventions and avoid ambiguous overloads.

## Success Criteria

- Admins and lecturers can create default-closed Subjects; only admins can create/manage Classes or change registration state/subject ownership.
- Invalid or missing Subject IDs are rejected server-side.
- Student option APIs expose only open subjects and matching classes.
- Anonymous access does not reach any new registration/enrollment endpoint.
- Legacy pre-login class lookup continues to work with a minimal DTO.

## Design Constraints

Preflight: Extend existing minimal endpoint groups and `PagedResult` conventions; DTOs, not EF entities, cross the API boundary. Preserve lecturer/admin Subject creation, admin-only status/Class commands, the exact anonymous legacy Class route, and `ClassLecturerAssigned` transaction behavior. Applicable rules: SEC_SERVER_SIDE_AUTHZ, CORR_BOUNDARY_VALIDATION, API_PAGINATION_BOUNDED, API_NO_DB_SCHEMA_AS_CONTRACT, TXN_BOUNDARY_MATCHES_OPERATION, CHG_ROLLOUT_BACKWARD_COMPATIBLE, DOTNET_CANCELLATION_TOKEN_PROPAGATED. Tests declined by user; quality gate required.

- Authorization must be enforced in Catalog endpoints in addition to Gateway policy.
- Never trust frontend filtering for registration status or class ownership.
- Preserve existing class lecturer event publication and failure semantics.
- New list APIs must use bounded pagination; no unbounded enrollment/catalog response.
- Subject status and enrollment commands must use serializable isolation and consistent Subject-row access ordering so close-vs-enroll has one deterministic winner.

## Quality and Testing State

- quality: approved (`quality/phase-02-admin-catalog-api-and-gateway-receipt.json`)
- testing: not started (skipped by user for cook; user will test later)
