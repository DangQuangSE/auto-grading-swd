# Phase 8: Legacy Roster Migration and User.ClassId Retirement

## Stories

- **P1:** subject-scoped enrollment becomes the only source of truth for course classes.

## Goal

Cut roster/import over from Identity `User.ClassId` to Catalog `StudentEnrollment` without invisible writes, distributed partial-loss, inferred Subjects, or permanent dual sources of truth.

## Steps

1. Inventory every `User.ClassId` reader/writer and capture baseline telemetry for old request fields, DB writes, reconciliation mismatches, and import failures.
2. Add one concrete Catalog command contract: an internal/admin batch enrollment upsert of at most 100 rows. Each row carries stable `OperationId`, `StudentId`, `SubjectId`, `ClassId`, and nullable `RowVersion`; null is create-only and conflicts if the student/subject already exists, a real change requires the current version, and an unchanged Class is replay-safe. Admin/import may write closed Subjects; invariants still apply and outcomes are returned per row.
3. Make Identity the durable roster/import workflow owner. In the same Identity transaction that creates/updates a user, write an outbox message keyed by `OperationId` (file checksum + row identity/version). Catalog persists a canonical payload hash beside each processed outcome: same key/hash replays it, while same key with a different Student/Subject/Class/version hash returns deterministic conflict and raises an alert. Result events let Identity record per-row `pending/succeeded/failed`.
4. Define partial-failure behavior: Identity user creation may succeed while enrollment remains `pending/failed`; API/UI returns per-row workflow status, retries only failed/pending operations, no compensation deletes a valid user, and reconciliation alerts on expired pending/dead-letter rows.
5. Choose one roster read architecture: admin-web pages Catalog enrollments first (maximum 100), then performs exactly one admin-only Identity batch profile lookup for those Student IDs (maximum 100). Join key is immutable user Guid. If Identity is unavailable, show Catalog rows with Student ID and an explicit degraded profile state; never issue N+1 calls or return unbounded pages.
6. Deploy compatibility readers before cutover: roster merges legacy assignments and Catalog enrollments, preferring Catalog for an equivalent Subject/Class mapping and labelling unresolved legacy rows. Deploy the outbox workflow dark and verify authorization/idempotency without routing production writers to it.
7. Perform a fenced writer cutover using a centrally stored, generation-versioned feature flag checked by every Identity writer. Deploy both modes, advance the shared generation to `enrollment-only`, require every running instance to acknowledge it, and block snapshot start until acknowledgements match the active instance set. Legacy APIs then reject `ClassId`; a DB trigger/permission guard prevents late changes. New-user registration no longer accepts a global Class.
8. After writers are fenced, take a repeatable-read legacy snapshot with timestamp/high-water mark, row count, ordered content hash, and immutable artifact identifier. Dry-run reconciliation consumes that exact artifact and classifies rows as convertible, already equivalent, ambiguous, unmapped, conflicting, or missing user.
9. Require admin resolution to zero ambiguous/unmapped/conflicting/missing rows. Immediately before apply, repeat the source query and require the same row count/hash/high-water mark; any delta invalidates approval and requires a new snapshot/dry-run.
10. Run an idempotent one-way backfill from the approved immutable artifact. Use Catalog invariants, stable operation IDs, and report created/skipped/conflict totals. Never overwrite a different existing enrollment automatically.
11. Verify artifact totals equal the sum of terminal outcomes, no pending/dead-letter event remains, and compatibility reads show no unresolved legacy-only row. Then switch roster reads to Catalog paging plus the single Identity batch-profile lookup; stop reading `User.ClassId`.
12. Observe for at least 7 consecutive days and at least 1,000 enrollment read/write operations. Removal gate requires: 0 legacy API field uses, 0 guarded DB writes, 0 reconciliation mismatches, 0 expired pending/dead-letter operations, and less than 0.1% Catalog workflow failures after retry. Any threshold breach aborts removal and keeps the column read-only.
13. In a separately deployed final Identity migration, remove `User.ClassId`, its FK/index/navigation, obsolete request fields, and cache/event handling used only by the global link. Require verified backup and forward-fix instructions. Tests cover idempotency-key payload drift, create-again conflict, stale update, unchanged replay, and multi-instance fence acknowledgement.

## Success Criteria

- Roster/import require Subject and Class and write enrollment through the durable outbox workflow.
- Retry after timeout cannot create duplicate users/enrollments or ambiguously overwrite an enrollment.
- Compatibility reads expose both pre-cutover legacy and post-cutover Catalog data without N+1 service calls.
- Approved snapshot hash remains unchanged through apply and all rows have terminal outcomes.
- Telemetry removal gates pass for 7 days and 1,000 operations before column removal.
- No production code, API DTO, frontend type, or test reads/writes `User.ClassId` after final migration.

## Design Constraints

- Catalog remains sole owner of enrollment; Identity owns durable workflow/outbox and user profile only.
- Migration is one-way and explicit; never infer Subject from class name, student, assignment, or submission.
- Do not use a distributed transaction or permanent dual writes. Outbox, stable operation IDs, idempotent consumer, result events, and reconciliation handle partial failure.
- Internal service authorization and admin authorization are separate policies and audit identities.
- A future administrative cohort must be modeled separately from course enrollment.

## Quality and Testing State

- quality: not evaluated
- testing: not started
