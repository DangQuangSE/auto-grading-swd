# Phase 3: Student Self-Service Enrollment APIs

## Stories

- **P1:** student views and changes one class for each open subject while retaining closed-subject history.

## Goal

Provide authenticated self-service reads and atomic create/change behavior using the caller identity from JWT.

## Steps

1. Add a student enrollment endpoint group under Catalog with role-based authorization for `student`.
2. Implement paginated `GET /enrollments/me?page&pageSize` (maximum 100) using `ClaimsPrincipal.GetUserId()`/JWT `NameIdentifier`; return display data, registration status, pagination metadata, and base64 `rowVersion`.
3. Implement a self upsert command whose request contains `SubjectId`, `ClassId`, and nullable `rowVersion` only. Never accept `StudentId`. Null version means create; if a row exists it returns `409`. Updates require the current version.
4. In a serializable transaction using the shared Subject-row order, validate Subject exists/open and Class matches Subject, then write. The composite FK remains the database backstop.
5. Enforce the unique index and row-version predicate. Translate parallel create and stale/missing update versions into deterministic `409` responses containing the current representation/version where safe.
6. Apply idempotent equality before version conflict handling: if the stored Subject/Class already equals the requested Subject/Class, return the current representation without mutation even when `rowVersion` is null or stale. Otherwise enforce create/update version rules and return `409` for missing/stale versions.
7. Return 400/404/409/403 consistently for malformed identifiers, missing records, closed registration/concurrency, and wrong roles. Avoid revealing other students' enrollment data.
8. Add API tests for two subjects/two classes, closed subjects, mismatch including direct DB constraint coverage, forged identities, pagination limits, duplicate/concurrent submissions, stale versions, exact replay of both null-version create and stale-version update, close-vs-enroll races, and closed-subject visibility.

## Success Criteria

- One account stores `SWD -> SE1830` and `SWR -> SE1829` simultaneously.
- A student cannot select a class from another subject or write while registration is closed.
- A student cannot name or modify another student.
- Concurrent requests cannot violate `(StudentId, SubjectId)` uniqueness.
- Existing enrollment remains visible after its Subject closes.

## Design Constraints

Preflight: Use Catalog minimal endpoint groups, `PagedResult`, defensive JWT parsing, DTO projections, SQL row-version, and serializable transactions matching the Subject status command. Catalog remains the sole owner and no Identity call is introduced. Applicable rules: SEC_SERVER_SIDE_AUTHZ, CORR_BOUNDARY_VALIDATION, API_PAGINATION_BOUNDED, TXN_RACE_HANDLED_AT_DATA_LAYER, TXN_IDEMPOTENT_ON_REPLAY, CONC_CANCELLATION_AND_TIMEOUT_PROPAGATED, DOTNET_CANCELLATION_TOKEN_PROPAGATED. Tests declined by user; quality gate required.

- Student ID comes only from JWT `NameIdentifier` and is parsed defensively.
- Validation and write are atomic in Catalog.
- Closing a Subject does not delete or mutate enrollment rows.
- Avoid direct calls to Identity; Catalog stores the opaque authenticated student Guid.
- Every list response is bounded to 100; clients follow pagination metadata.

## Quality and Testing State

- quality: approved (`quality/phase-03-student-self-enrollment-api-receipt.json`)
- testing: not started (skipped by user for cook; user will test later)
