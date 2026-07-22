# Phase 9: Post-Cutover Acceptance and Completion Gate

## Stories

- **P1:** the enrollment-only system is verified end to end after legacy removal.
- **P2:** admin correction remains functional after roster cutover.
- **P3 boundary:** no cohort, capacity, waitlist, or approval semantics were introduced accidentally.

## Goal

Run the final full-system regression and operational smoke gates after Phase 8, before the feature is declared complete.

## Steps

1. Run the full backend solution tests/build plus Catalog and Identity migration tests from both pre-cutover and post-cutover fixtures.
2. Run admin-web and user-web tests/builds and verify no compiled or runtime contract references `User.ClassId`.
3. Exercise Gateway/service authorization, admin/lecturer Subject creation rules, admin-only status/Class management, student self-enrollment, admin correction, roster/import outbox processing, and batch profile composition.
4. Run browser smoke tests for admin Subjects, Classes, roster/import status and student Profile at desktop and responsive widths.
5. Verify two-Subject behavior end to end: student self-selects one, roster/import writes another, admin corrects one, and no action changes the other.
6. Verify operational dashboards/alerts for outbox age, dead letters, idempotency conflicts, legacy field use, reconciliation mismatch, and Catalog workflow failures.
7. Capture command output, migration artifact hashes, telemetry gate evidence, and release rollback/forward-fix instructions in the completion receipt.

## Success Criteria

- All backend/frontend suites and builds pass after legacy removal.
- End-to-end and browser smoke scenarios pass through Gateway.
- No `User.ClassId` reference remains outside historical migrations/release artifacts.
- Operational alerts and dashboards are active and removal-gate evidence is recorded.

## Design Constraints

- Phase 9 cannot start until Phase 8 telemetry and removal gates pass.
- Completion claims require fresh verification output, not results captured before cutover.
- Historical migration files may retain the old column name; production code may not.

## Quality and Testing State

- quality: not evaluated
- testing: not started
