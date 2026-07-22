# Phase 7: Integration, Regression, Performance, and Rollout Verification

## Stories

- **P1:** end-to-end admin-open/student-enroll workflow.
- **P2:** admin correction workflow.
- **P3 boundary:** prove deferred capacity/approval behavior was not accidentally introduced.

## Goal

Verify cross-layer behavior, legacy coexistence, security, concurrency, and operational rollout before release.

## Steps

1. Run Catalog API tests, full backend solution tests/build, admin-web tests/build, and user-web tests/build.
2. Add an integration scenario: admin creates a closed Subject, creates subject-linked Classes, opens it, student enrolls, admin closes it, student can read but cannot change, admin can correct it.
3. Add a two-subject scenario proving the same Student ID can hold distinct classes and changing one leaves the other untouched.
4. Exercise authorization directly against Catalog and through Gateway: anonymous callers reach only exact legacy `GET /catalog/classes`; student cannot call admin routes; forged Student IDs have no effect.
5. Exercise concurrent create/update, close-vs-enroll, and class-reassign-vs-enroll requests; confirm one row per student/subject and the documented winner/status.
6. During the compatibility deployment, regression-test Identity registration, roster editing, and bulk roster import. Confirm they remain stable before controlled cutover in Phase 8 and never dual-write silently.
7. Test migration on a legacy fixture and document remediation. Test down only before activation; after writes, prohibit destructive down migration and use backup/export plus forward-fix recovery.
8. Measure representative list/upsert requests with 10,000 enrollment rows; confirm p95 under 500 ms and pagination capped at 100. Capture query plans/index adjustments if the target is missed.
9. Perform an authenticated browser walkthrough of admin Subjects/Classes and student Profile at desktop and sidebar-responsive widths.
10. Document ordering: deploy additive schema/API/UI, classify legacy classes, enable Profile, migrate roster/import in Phase 8, reconcile, then retire `User.ClassId`. Never infer a Subject during backfill.

## Success Criteria

- All existing and new automated suites pass and both frontends build.
- Gateway and service-level authorization behave consistently.
- Concurrent writes never create duplicate student/subject rows.
- Legacy roster/bulk import remains stable during the compatibility window without silent dual writes.
- Performance target is verified with 10,000 rows and bounded pages.
- Rollout documentation explicitly states that legacy class assignment requires admin review.

## Design Constraints

Preflight: This cook run performs compile/type/build verification and cross-layer quality review only. The user explicitly declined automated, integration, browser, concurrency, migration-data, and performance tests and will run them later; none may be claimed as passing. Applicable rules: CHG_ROLLOUT_BACKWARD_COMPATIBLE, CHG_MIGRATION_DEPLOY_ORDER_SAFE, SEC_SERVER_SIDE_AUTHZ, API_STABLE_VERSIONED_CONTRACT, DOC_ARCHITECTURE_DECISION_RECORDED. Quality gate required.

- Verification must use fresh command output before completion is claimed.
- No destructive production data cleanup or inferred backfill is part of rollout.
- Do not broaden the anonymous gateway route to a wildcard.
- P3 capacity, waitlist, approval, or scheduling behavior must remain absent.

## Quality and Testing State

- quality: not evaluated
- testing: not started (all planned behavioral/performance/browser tests skipped by user for cook; user will test later)
