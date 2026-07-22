# Phase 1: Catalog Enrollment Schema and Safe Migration

## Stories

- **P1:** subject registration state, subject-scoped classes, and one class per student per subject.

## Goal

Establish Catalog-owned persistence while making the database upgrade safe for existing Class and Identity roster data.

## Steps

1. Add a registration status to `Subject` using an enum/string representation (`open`, `closed`) and default every newly created subject to `closed`.
2. Add nullable `SubjectId` and a Subject navigation to Catalog `Class`. Null represents only a legacy row created before this feature.
3. Add `StudentEnrollment` with `Id`, `StudentId`, `SubjectId`, `ClassId`, `CreatedAt`, `UpdatedAt`, and a SQL row-version/concurrency token.
4. Configure a restrictive Subject FK plus composite FK `(ClassId, SubjectId) -> Class(Id, SubjectId)`. Add the required unique alternate key on Class `(Id, SubjectId)` so the database guarantees class/subject consistency.
5. Add a unique index on `(StudentId, SubjectId)` and lookup indexes on `StudentId`, `SubjectId`, and `ClassId`. Treat existing `Class.Name` as the display code, store its trimmed uppercase `NormalizedName`, and enforce unique `(SubjectId, NormalizedName)` for non-null Subject rows.
6. Create an EF migration that adds the Subject status, nullable Class `SubjectId`, and enrollment table without rewriting existing rows.
7. Leave `Identity.User.ClassId` untouched in this additive migration. Record that it is transitional and cannot be converted until admins explicitly map legacy Classes to Subjects; retirement occurs only in Phase 8.
8. Verify migration up/down behavior against a database containing legacy classes and users, including classes with duplicate names that remain legal while `SubjectId` is null. Down migration is supported/tested only before activation; after enrollment writes, recovery requires backup/export and a forward-fix.

## Success Criteria

- New subjects default to `closed`.
- Existing Class rows survive migration with null `SubjectId`.
- Database rejects a second enrollment for the same student/subject.
- Database rejects an enrollment whose Class and Subject do not match, even if application validation is bypassed.
- Database permits different subject enrollments for the same student.
- No migration reads or modifies Identity `User.ClassId`.

## Design Constraints

Preflight: Follow Catalog's entity-plus-`OnModelCreating` convention; keep Catalog as the sole enrollment owner; use restrictive FKs, database uniqueness/composite integrity, SQL row-version concurrency, additive nullable rollout fields, and no inferred Identity backfill. Applicable rules: CORR_BOUNDARY_VALIDATION, DOM_SINGLE_SOURCE, TXN_RACE_HANDLED_AT_DATA_LAYER, TXN_CONSTRAINT_NOT_REPLACED_BY_APP_CHECK, CHG_MIGRATION_DEPLOY_ORDER_SAFE, DB_CONSTRAINT_ENFORCES_INVARIANT, DB_MIGRATION_HAS_ROLLBACK_PATH. Tests declined by user; quality gate required.

- Catalog is the sole owner of `StudentEnrollment`.
- `Class.SubjectId` is nullable only for legacy rollout; application commands for create/update must not create new null values.
- Do not infer a Subject from class name, student submission, assignment, or Identity `ClassId`.
- Delete behavior must preserve enrollment integrity; destructive cascade across Subject/Class enrollment history is prohibited.
- Class `SubjectId` cannot change after any enrollment references the Class; null-to-value legacy repair is allowed only before enrollment.

## Quality and Testing State

- quality: approved (`quality/phase-01-catalog-enrollment-schema-receipt.json`)
- testing: not started (skipped by user for cook; user will test later)
