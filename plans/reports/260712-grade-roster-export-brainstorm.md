# Brainstorm: Student grade roster & Excel export

**Date:** 2026-07-12

## Ideas Explored

- **CSV export vs real .xlsx** — CSV needs no new dependency and opens fine in Excel, but has no formatting. User explicitly wants a real `.xlsx` file, so a client-side Excel-writing library (e.g. `exceljs`) is needed in admin-web.
- **Grade scope: per-assignment vs per-subject aggregate vs both** — per-subject aggregation would require defining how multiple assignment scores roll up into one grade, which isn't defined anywhere in the system today. Chose per-assignment only (matches the existing single-assignment `FinalGrade`/`GradePublication` model) to avoid inventing an undefined aggregation rule.
- **Score shown: AI-suggested vs published FinalGrade vs both** — showing AI-suggested scores risks lecturers/admins treating an unreviewed score as authoritative. Chose FinalGrade (published) only — consistent with `GradePublication` being the existing "this is official" gate in Grading service.
- **Data joining across 3 services (Submission → Grading → Identity)** — considered (a) FE stitches together existing per-resource endpoints, vs (b) a new backend aggregate endpoint that calls across services directly, vs (c) event-driven denormalization (mirroring the `RubricConfirmed → LocalRubric` pattern from the rubric-ai-parsing feature). Rejected (b) as it breaks the project's "no direct HTTP between services" convention; rejected (c) as disproportionate infrastructure for a read-only report. Chose (a), refined with two new **batch** endpoints (`GET /grades/final?submissionIds=...`, `GET /identity/users?ids=...`) so request count stays constant (~3 calls) regardless of class size, avoiding an N+1 pattern.
- **MSSV/Class as new fields — where do they live** — `User` (Identity service) only has Email/FullName/Role today; no student-ID or class concept exists anywhere in the domain. Chose to add `StudentCode` (MSSV) and `ClassName` to `User` directly, since Identity already models `Role = Student`.
- **How MSSV/Class gets into the system** — considered (a) student self-registration only, (b) admin/lecturer manual CRUD only, (c) bulk import via Excel/CSV roster upload, or some combination. User wants all three: self-entry at registration, individual edit by admin/lecturer, and bulk roster import — reflecting that schools always distribute a class list.
- **Bulk import semantics for students who haven't registered yet** — considered pre-provisioning passwordless accounts from the roster file (import creates the account; first login/registration by matching email claims it) vs. update-only import (roster rows only apply to already-registered users, matched by email; unmatched rows are skipped/reported). User chose to keep it simple: students self-register first, import/CRUD only manages already-registered students' MSSV/Class.

## User's Direction

Two-part feature, in order of dependency:
1. **Roster management** — `StudentCode` (MSSV) + `ClassName` fields on `User`; settable at self-registration, editable individually by admin/lecturer, and settable in bulk via an Excel/CSV roster upload that matches existing users by email (unmatched rows reported, not auto-created).
2. **Grade viewing/export** — for a given assignment, a table of submissions joined with published `FinalGrade` and each student's MSSV/Class, filterable by MSSV, Class, or both (combinable), exportable to a real `.xlsx` file. Data joining happens client-side in admin-web against two new batch endpoints (Grading, Identity) plus the existing `GET /submissions?assignmentId=`.

## Open Questions

- Exact admin-web page layout: new top-level nav item vs. extending `SubmissionReviewPage` — left to `/ck:plan`.
- Bulk import file column mapping/validation rules (required columns, duplicate-email handling within one file, max row count) — left to `/ck:plan`.
- Whether `ClassName` is free text or a constrained/enumerable value — user described it as "trường luôn có danh sách lớp" (school always has a class list) but didn't specify a `Class` entity; treating as free text for now, flagged as an assumption below.
- Where the "student self-registers with MSSV/Class" fields live in `fe/user-web`'s existing register flow, and whether they're required or optional at registration — left to `/ck:plan`.

## Risks

- **No existing xlsx-writing dependency in the repo** — first use of a library like `exceljs`; bundle-size and browser-compatibility need a quick check during planning.
- **Import matching by email is brittle** — typos or case differences between the roster file and a student's registered email will silently skip that row; needs clear per-row error reporting back to the importer (which row/email failed) so lecturers can fix and re-upload.
- **`StudentCode`/`ClassName` uniqueness/format not yet defined** — e.g. can two students share an MSSV, is MSSV validated against any format — needs a decision in planning since it affects the User migration's constraints.

## Scope Expansion (during /ck:plan)

During planning validation, the user pushed the scope significantly further than the original brainstorm:

- **PATCH /users/{userId} authorization**: originally any lecturer+admin; user insisted only admin, or a lecturer with an actual relationship to the target student ("giám khảo hoặc lecturer của class đó" — the grader, or that class's lecturer), not any lecturer arbitrarily.
- **Class becomes a real entity, not free text**: `ClassName` as a plain string (the original spec's assumption) can't support "lecturer của class đó" since nothing links a free-text class name to a lecturer. User confirmed: add a real `Class` entity (Catalog service) with `LecturerId`, and `User.ClassName` becomes `User.ClassId` referencing it.
- **Cross-service authorization requires event-driven denormalization**: Identity (where the PATCH endpoint lives) has no direct knowledge of Catalog's `Class.LecturerId` or Grading's grade-publication history. Resolved by reusing two *already-existing* events (`SubmissionUploaded`, `GradePublished`) plus one *new* event (`ClassLecturerAssigned`) to build two local read-only caches in Identity — no new direct inter-service HTTP calls, consistent with the codebase's established event-bus-only convention.
- **Net effect**: plan grew from ~7 phases (Identity fields + 2 batch endpoints + 2 FE pages) to ~10 phases (adds a full `Class` CRUD subsystem in Catalog, 2 new local caches + 2 new event subscriptions in Identity, and an admin-web class-management page). User explicitly confirmed doing all of this in one plan rather than splitting into a foundation-plan + roster-plan sequence.
