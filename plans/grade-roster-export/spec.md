# Spec: Student grade roster & Excel export

**Date:** 2026-07-13
**Status:** Draft

---

## Problem Statement

Lecturers/admins currently have no way to see or export a class's grades in one place, and the system has no concept of a student ID (MSSV), a class, or which lecturer teaches which class — `User` only has email/full name/role, and `Subject`/`Assignment` have no owning lecturer. Without this, grades can't be filtered/reported the way schools require (by student ID, by class, or both), there's no `.xlsx` gradesheet export, and there's no way to restrict who can edit a student's roster data to the people who actually have a relationship to that student (their class's lecturer, or a lecturer who graded them) versus any lecturer in the system.

---

## User Stories

<!-- P1 = MVP (must ship), P2 = nice-to-have, P3 = future/out-of-scope -->

- **[P1]** As an admin, I want to create classes and assign a lecturer to each one, so the system knows who's responsible for which group of students.
  Accepted when: an admin-web page lets an admin create a `Class` (name) and assign/reassign its `LecturerId`; only admin can do this.

- **[P1]** As a student, I want to pick my class from a list and enter my MSSV when I register, so my submissions and grades are correctly attributable to me and my class's lecturer.
  Accepted when: the registration form (`fe/user-web`) has an MSSV text field and a Class dropdown populated from existing classes (fetched anonymously, since registration happens before login); a successful registration persists `StudentCode` and `ClassId` on the created `User`.

- **[P1]** As a lecturer/admin, I want to view and edit an individual student's MSSV/Class, so I can correct mistakes without asking the student to re-register — but only for students I actually have a relationship with (my class's students, or students I've graded), unless I'm an admin.
  Accepted when: an admin-web page lists students with their MSSV/Class; editing a student succeeds for admin always, or for a lecturer only when that lecturer is the `LecturerId` of the student's `Class` OR has published a grade for at least one of that student's submissions; otherwise the edit is rejected with 403.

- **[P1]** As a lecturer/admin, I want to bulk-import a class roster (Excel/CSV) to set MSSV/Class for many students at once, so I don't have to enter each one manually.
  Accepted when: uploading a file with columns for email/MSSV/Class updates `StudentCode`/`ClassId` on each `User` whose email matches a row AND whose class name in the file matches a known `Class` name; rows with no matching registered user, or no matching class name, are skipped and reported back to the uploader (not auto-created).

- **[P1]** As a lecturer/admin, I want to view a grade table for a specific assignment, filterable by MSSV, Class, or both, so I can find specific students' results.
  Accepted when: given an assignment, the table shows one row per submission with student name, MSSV, Class name, and published final score; entering an MSSV filter, a Class filter, or both narrows the rows shown (AND when both are set).

- **[P1]** As a lecturer/admin, I want to export the currently-filtered grade table to a real `.xlsx` file, so I can archive or submit it per school requirements.
  Accepted when: clicking Export downloads a `.xlsx` file (not CSV) containing exactly the rows currently visible in the filtered table, with a header row.

- **[P3]** _(out of scope — noted for future)_ Pre-provisioning unregistered students via bulk import (creating passwordless accounts ahead of self-registration).

- **[P3]** _(out of scope — noted for future)_ Grade view/export aggregated across multiple assignments or an entire subject.

- **[P3]** _(out of scope — noted for future)_ A lecturer managing/reassigning their own class's `LecturerId` (only admin can (re)assign class ownership in this iteration).

---

## Functional Requirements

<!-- Number each. Be specific. -->

1. FR-01: New `Class` entity in Catalog service: `Id`, `Name`, `LecturerId` (Guid, required), `CreatedAt`.
2. FR-02: Catalog gets `POST /classes` (create, admin-only), `PATCH /classes/{id}` (reassign `LecturerId`, admin-only), `GET /classes` (list — **anonymous**, no auth required, since `fe/user-web`'s pre-login registration form needs it; returns only `Id`+`Name`, no `LecturerId`, to avoid leaking staffing info to anonymous callers).
3. FR-03: Catalog publishes a new `ClassLecturerAssigned(ClassId, ClassName, LecturerId)` event whenever a `Class` is created or its `LecturerId` changes.
4. FR-04: `User` (Identity service) gains `StudentCode` (string, nullable) and `ClassId` (Guid, nullable — references a Catalog `Class` by id, no cross-database FK, validity checked at write time against Identity's local class cache from FR-05) fields, replacing the earlier free-text `ClassName` idea.
5. FR-05: Identity subscribes to `ClassLecturerAssigned` and maintains a local read-only cache table (`ClassId` → `ClassName`, `LecturerId`) — used to resolve a `ClassId` to a display name and to look up the owning lecturer for authorization (FR-09).
6. FR-06: Identity subscribes to the existing `SubmissionUploaded` (`SubmissionId`, `AssignmentId`, `StudentId`) and `GradePublished` (`SubmissionId`, `PublishedByUserId`) events, storing each in its own table keyed by `SubmissionId` (not eagerly merged, since the two events have no guaranteed arrival order). The "grader" authorization condition (FR-09) is computed by joining the two tables on `SubmissionId` at authorization-check time, per target student. Grading authority is cumulative and permanent: if a submission is later re-graded by a different lecturer, both lecturers retain edit authority over that student — every lecturer who has ever published a grade for any of a student's submissions keeps roster-edit access to that student (explicit product decision; not automatically revoked by a re-grade or by time).
7. FR-07: `POST /auth/register` accepts optional `studentCode` (string) and `classId` (Guid, must match a known class in Identity's local class cache or the registration is rejected with 400) fields and persists them on the created user.
8. FR-08: A new admin-web page lists registered students (email, full name, MSSV, resolved class name) and supports editing one student's MSSV/Class at a time via `PATCH /users/{userId}`.
9. FR-09: `PATCH /users/{userId}` is authorized when: the caller is `admin`, OR the caller is `lecturer` AND (`LecturerId` of the target student's `Class` (via FR-05 cache) equals the caller's user id, OR the caller's user id is in the target student's grader set (via FR-06 cache)). All other lecturer callers get 403.
10. FR-10: A new endpoint accepts a bulk roster file (Excel/CSV) with header-name-mapped (not positional) Email/StudentCode/ClassName columns; for each row: resolve `ClassName` to a `ClassId` via Identity's local class-name cache (case-insensitive; unresolvable class name → row skipped, reason "unknown class"), then resolve `Email` to an existing `User` (unresolvable → row skipped, reason "email not registered"); on double match, update `StudentCode`/`ClassId`. Returns a per-row result report (row number, email, reason skipped) to the caller. Same authorization rule as FR-09 applies per-row (a lecturer can only bulk-update rows for students they have a relationship with; rows outside their authority are skipped with reason "not authorized for this student").
11. FR-11: A new batch endpoint on Grading, `GET /grades/final?submissionIds=...`, lecturer/admin-gated (stricter than the existing student-accessible single-submission endpoint), returns the latest published `FinalGrade` for each requested `SubmissionId` (omitting submissions with no published grade), deduplicating the requested ID list.
12. FR-12: A new batch endpoint on Identity, `GET /users?ids=...`, lecturer/admin-gated, returns MSSV/resolved-class-name/name for each requested user id, deduplicating the requested ID list. A companion `GET /users` (list-all, lecturer/admin-gated) supports the roster page's initial load.
13. FR-13: A new admin-web page: given a selected assignment, calls `GET /submissions?assignmentId=`, then the two batch endpoints (FR-11, FR-12), and joins the results client-side into one table (student name, MSSV, class name, published final score; blank score for submissions with no published grade yet).
14. FR-14: The grade table supports filtering by MSSV (exact or partial match), Class name (exact or partial match), or both combined (AND).
15. FR-15: An Export button generates a real `.xlsx` file (via a client-side Excel-writing library) containing the currently-filtered rows, with a header row, and triggers a browser download.

---

## Non-Functional Requirements

<!-- Use numbers, not adjectives. -->

- Performance: loading the grade table for one assignment issues at most 3 network requests regardless of class size (1 submissions call + 1 batch grades call + 1 batch users call) — no per-student N+1 calls.
- Security: class management (FR-02 create/reassign) is admin-only; roster edit/bulk-import (FR-08, FR-09, FR-10) enforce the lecturer-relationship check in FR-09, not just role membership; batch grade/user lookups (FR-11, FR-12) require lecturer or admin role, matching the existing role-policy convention on Catalog/Grading endpoints; `GET /classes` (FR-02) is the one deliberately anonymous exception, and returns no `LecturerId` to anonymous callers.
- Availability: bulk import is synchronous (not a background job) since roster files are small (class-sized, not bulk-institution-sized). Cross-service authorization data (FR-05, FR-06 caches) is kept eventually-consistent via the existing event bus, not direct HTTP — Identity has no hard runtime dependency on Catalog/Submission/Grading being reachable to process a `PATCH /users/{userId}` request; it reads its own local cache.

---

## Success Criteria

- [ ] An admin can create a `Class`, assign a lecturer, and that lecturer can immediately (after event propagation) edit MSSV/Class for a student in that class, while a different lecturer with no relationship to that student gets 403 on the same edit.
- [ ] A student who fills in MSSV + picks a class at registration has both values visible immediately in the admin roster list (as MSSV + resolved class name).
- [ ] Uploading a roster file with 30 rows where 25 match existing registered emails AND known class names updates exactly 25 users; the other 5 are reported as skipped with a specific reason (email not registered / unknown class / not authorized).
- [ ] The grade table for an assignment with N submissions loads with exactly 3 network requests, independent of N.
- [ ] Filtering by MSSV alone, Class alone, and both together each produce the correct narrowed row set on a table with mixed MSSV/Class values.
- [ ] The exported `.xlsx` file opens correctly in Excel/LibreOffice and contains exactly the filtered rows shown on screen at export time, not the full unfiltered set.
- [ ] A lecturer who has published at least one grade for a student, but isn't that student's class lecturer, can still edit that student's MSSV/Class (the "grader" condition works independently of the "class lecturer" condition).

---

## Out of Scope

- Pre-provisioning unregistered students via bulk import (P3 above).
- Multi-assignment/subject grade aggregation (P3 above).
- Lecturer self-service class/lecturer reassignment (P3 above) — admin-only in this iteration.
- CSV export (xlsx only, per user's explicit choice).
- Showing AI-suggested (unpublished) scores in this view — published `FinalGrade` only.
- Real-time cache invalidation UX (e.g. a "your access is updating, try again shortly" message) if a lecturer tries to edit a student immediately after being assigned to their class, before the `ClassLecturerAssigned` event has propagated to Identity — treated as a rare, self-resolving race (retry succeeds once the event lands), not a blocking concern for this iteration.

---

## Assumptions

- `Class` is scoped simply — no relationship to `Subject`/`Assignment` is added; a `Class` exists purely to group students under a lecturer for authorization/reporting purposes, independent of which subjects that class's students happen to submit work for.
- MSSV (`StudentCode`) has no enforced uniqueness or format validation in this iteration — two users could share a value; add a constraint later if the school's real MSSV format is known.
- Bulk import matches rows to existing users by email, case-insensitively, mirroring how `POST /auth/register` already normalizes email (`Trim().ToLowerInvariant()`); class names are matched case-insensitively against Identity's local class-name cache.
- The two new batch endpoints (FR-11, FR-12) and the class list endpoint (FR-02) are called directly by the FE (already authenticated via the gateway's existing JWT flow where role-gated), not by another backend service — consistent with the "no direct HTTP between services" convention; the FR-05/FR-06 local caches are how Identity gets cross-service data it needs for authorization, via events, not HTTP.
- Both `SubmissionUploaded` and `GradePublished` events already exist in the codebase with the exact shape FR-06 depends on (`SubmissionUploaded(SubmissionId, AssignmentId, StudentId, ReportObjectKey, DiagramObjectKey)`, `GradePublished(SubmissionId, FinalGradeId, FinalScore, PublishedByUserId)`) — verified during brainstorming, no changes needed to either.

---
