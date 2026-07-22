# Plan: Student Course-Class Enrollment

Status: Ready
Date: 2026-07-22
Mode: Hard
Test: default (non-TDD)

## Overview

Catalog becomes the source of truth for subject registration, subject-scoped classes, and `StudentEnrollment`. The authenticated student identity is always read from the JWT `NameIdentifier`; student-facing write contracts never accept `StudentId`. The rollout temporarily preserves Identity `User.ClassId`, then migrates roster/import writers to subject-scoped enrollment and removes the legacy field only after explicit mapping and reconciliation pass.

## Scope Challenge

- **Exists?** Subject and Class catalogs, admin subject/class screens, JWT authentication, Identity roster, and bulk import already exist. Subject registration state, Class-to-Subject ownership, multi-subject enrollment, self-service enrollment APIs, and a student Profile screen do not.
- **Minimum?** Add registration state to Subject, nullable `Class.SubjectId` for safe rollout, Catalog-owned enrollment rows, admin controls, and an authenticated Profile workflow. Preserve rather than rewrite the legacy roster model.
- **Complexity?** Hard: schema migration, authorization boundaries, concurrent upserts, two frontends, gateway routing, and legacy coexistence span more than three phases.

## Spec Quality Check

- Remaining `[NEEDS CLARIFICATION]`: none.
- Success criteria measurable: yes.
- P1/P2/P3 stories: present; P3 is explicitly deferred.
- Acceptance criteria testable: yes.
- **Verdict: PASS.**

## Architecture Decisions

- Catalog owns `StudentEnrollment` because it already owns both Subject and Class and can enforce class/subject consistency in one transaction without cross-service calls.
- `StudentEnrollment` stores `StudentId`, `SubjectId`, `ClassId`, timestamps, and a row-version token. A unique database constraint on `(StudentId, SubjectId)` prevents duplicates, while a composite FK `(ClassId, SubjectId) -> Class(Id, SubjectId)` guarantees that the selected Class belongs to the recorded Subject.
- Student self-service endpoints derive `StudentId` exclusively from the JWT `NameIdentifier`. No self-service route parameter or request body may select another student.
- Subject owns a registration status (`open`/`closed`), defaulting to `closed`. Closing a subject prevents student writes but does not hide/delete the student's existing enrollment.
- Admin and lecturer may create Subjects, preserving current behavior; only admin may open/close registration or manage Classes.
- Class gains nullable `SubjectId` during rollout. New and edited classes require a valid Subject; null is retained only for legacy rows until admins explicitly classify them.
- Once a Class has an enrollment, its Subject ownership is immutable and reassignment returns `409 Conflict`; lecturer reassignment remains independent.
- Enrollment writes and Subject open/close commands use serializable transactions with consistent Subject-row access ordering. Either enrollment commits first, or close wins and enrollment is rejected.
- Enrollment DTOs return a base64 row version. Update requests carry that version; an existing row with a missing/stale version returns `409`, while creates use a null version and rely on the unique index for races.
- Identity `User.ClassId` is transitional only and is never synchronized bidirectionally. After admins map legacy Classes to Subjects, a controlled one-way migration creates unambiguous enrollments, roster/import writers switch to Catalog, reconciliation must report zero unresolved rows, and a final migration removes `User.ClassId`.
- Any future administrative cohort/class is modeled separately (for example `AdministrativeClassId`); it must not reuse course Class or the legacy field.
- The existing anonymous `GET /catalog/classes` gateway exception remains only for legacy pre-login registration compatibility and returns a restricted safe DTO. New open-subject, subject-class, and enrollment routes are authenticated and must fall through the protected catalog gateway route.
- P3 capacity, waitlist, approval, and scheduling concerns remain outside this plan.

## Phases

- [x] Phase 1: Catalog enrollment schema and safe migration — P1 [quality: approved; testing: skipped_by_user]
- [x] Phase 2: Admin catalog APIs and gateway boundary — P1 [quality: approved; testing: skipped_by_user]
- [x] Phase 3: Student self-service enrollment APIs — P1 [quality: approved; testing: skipped_by_user]
- [x] Phase 4: Admin enrollment correction APIs — P2 [quality: approved; testing: skipped_by_user]
- [x] Phase 5: Admin subject and class UI — P1, P2 [quality: approved; testing: skipped_by_user]
- [x] Phase 6: Student Profile enrollment UI — P1 [quality: approved; testing: skipped_by_user]
- [ ] Phase 7: Integration, regression, performance, and rollout verification — P1, P2, P3 boundary
- [ ] Phase 8: Legacy roster migration and `User.ClassId` retirement — P1
- [ ] Phase 9: Post-cutover acceptance and completion gate — P1, P2, P3 boundary

## Dependencies

- Existing Catalog EF Core/SQL Server infrastructure and JWT middleware.
- Existing admin-web and user-web API clients, React Query hooks, routing, and shared form components.
- Existing Identity roster and bulk-import behavior remains available only during the compatibility window, then moves to Catalog enrollment contracts in Phase 8.
- Identity durably orchestrates roster/import through an outbox; Catalog consumes idempotently and remains the sole enrollment owner. Roster reads page Catalog then use one bounded Identity batch profile lookup.

## Risks

- **HIGH — legacy ambiguity:** `Identity.User.ClassId` has no reliable Subject mapping. Mitigation: never auto-migrate it; expose null-subject legacy classes to admins for explicit correction.
- **HIGH — duplicate concurrent writes:** two simultaneous student requests could create two rows. Mitigation: transaction plus unique `(StudentId, SubjectId)` constraint; translate the losing insert into a deterministic conflict/retry outcome.
- **HIGH — authorization regression:** accepting `StudentId` from a self request enables horizontal privilege escalation. Mitigation: derive it from JWT and test forged body/query/route identifiers.
- **MEDIUM — anonymous class enumeration:** the legacy gateway route exposes class identifiers/names. Mitigation: keep the DTO minimal, ensure it cannot expose enrollment or subject registration state, and document removal once pre-login class selection is retired.
- **MEDIUM — partial rollout:** new classes require subjects while old rows remain null. Mitigation: filtered constraints, explicit UI state, migration notes, and no destructive cleanup.
- **MEDIUM — legacy/new semantic drift:** roster and bulk import change `User.ClassId` but not enrollments. Mitigation: label both models distinctly and add regression tests proving coexistence.

## Red-Team Adjudication

- **ACCEPTED:** database composite Class/Subject relationship and deterministic serializable close-vs-enroll semantics.
- **ACCEPTED:** Class Subject ownership becomes immutable after the first enrollment.
- **ACCEPTED:** row-version is explicit in response and update request contracts and propagated by both frontends.
- **ACCEPTED:** P2 includes an admin correction UI, not API only.
- **ACCEPTED:** self-enrollment reads are paginated and migration rollback is restricted after activation.
- **ACCEPTED:** Class display-code uniqueness uses a normalized canonical value, not raw display text.
- **ACCEPTED:** exact replay checks equality before row-version validation; unchanged requests return the current representation, while real changes still require the current version.
- **ACCEPTED:** user validation changed FR-01 so admin and lecturer retain Subject creation permission; registration status remains admin-only.
- **ACCEPTED:** one-way retirement of `User.ClassId`; no permanent dual source of truth or bidirectional synchronization.
- **ACCEPTED:** cutover uses compatibility reads, an atomic legacy-writer fence, immutable hashed snapshot, idempotent outbox workflow, measurable telemetry gate, and post-cutover full regression.
- **ACCEPTED:** idempotency keys bind to canonical payload hashes, batch row-version semantics prohibit blind overwrite, and a shared generation fence requires acknowledgement from every writer instance before snapshot.

## Deferred P3

Capacity limits, deadlines beyond open/closed, approval, waitlists, timetables, and external synchronization are not implemented. Schema and APIs should not invent placeholders that imply support for them.

## Handoff

Execute phases in order. Before each phase, `ck:cook` must confirm whether to create/run unit tests and whether to run `ck:quality`. This plan uses the default testing workflow, not test-first TDD.
