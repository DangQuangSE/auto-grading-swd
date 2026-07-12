# Plan: Student Grade Roster & Excel Export

Status: 🟡 In Progress
Date: 2026-07-13
Mode: Hard

## Overview

This plan implements a complete student roster and grade export system that enables lecturers and admins to manage student class assignments, view assignments' grade tables with filtering, and export results to `.xlsx`. It introduces a `Class` entity linking students to lecturers, event-driven authorization caches in Identity (tracking class membership and grader relationships), bulk roster import with skipped-row reporting, and a client-side-joined grade table with real-time filtering and Excel export via SheetJS.

## Phases

- [ ] Phase 1: Catalog `Class` Entity & Events — New `Class` domain entity (Id, Name, LecturerId, CreatedAt); CRUD endpoints (POST/PATCH admin-only, GET anonymous); ClassLecturerAssigned event publishing.
- [ ] Phase 2: Identity Event Consumers & Local Caches — Cache tables ClassLecturerCache, SubmissionStudent, SubmissionGrader (join deferred to query time, not eagerly merged — see Plan Review); event handlers for ClassLecturerAssigned, SubmissionUploaded, GradePublished; single-row-upsert idempotency (no navigation-collection pattern needed).
- [ ] Phase 3: User Registration with MSSV & ClassId — User.StudentCode and User.ClassId fields (nullable); POST /auth/register accepts optional studentCode and classId; validates classId against cache (400 if unknown).
- [ ] Phase 4: User Listing & Batch Endpoints — GET /users (list-all, lecturer/admin-gated); GET /users?ids=... (batch, deduplicated, resolves ClassId→ClassName); PATCH /users/{userId} with real authorization (admin OR class-lecturer OR grader).
- [ ] Phase 5: Bulk Roster Import Endpoint — Excel/CSV upload with header-mapped Email/StudentCode/ClassName columns; row-by-row authorization check; atomic per-row updates; detailed skip-reason report (email not registered / unknown class / not authorized).
- [ ] Phase 6: Grading Batch Grades Endpoint — GET /grades/final?submissionIds=... (lecturer/admin-gated); batch endpoint returning latest published FinalGrade per SubmissionId (omit unpublished); deduplicates requested IDs.
- [ ] Phase 7: Admin Class Management UI — admin-web page listing Classes; create Class (form: name, lecturer picker); assign/reassign lecturer (PATCH endpoint); link to roster page.
- [ ] Phase 8: Admin Student Roster UI — admin-web page listing students (email, full name, MSSV, resolved class name, filters); edit student MSSV/Class (modal or inline, PATCH /users/{userId}); link to bulk import page.
- [ ] Phase 9: Admin Bulk Roster Import UI — admin-web file upload (Excel/CSV); column mapping UI (if needed); displays detailed skip-reason report per row; row count success/skipped summary.
- [ ] Phase 10: Admin Grade Table & Export UI — admin-web assignment picker; fetches submissions + batch grades + batch users; client-side join into table (student name, MSSV, class name, published final score); filters by MSSV (partial/exact) and Class (partial/exact, AND logic); Export button generates `.xlsx` via SheetJS with filtered rows + header.
- [ ] Phase 11: User Registration Form — user-web registration adds MSSV text field; Class dropdown (fetches anonymously from GET /catalog/classes); stores both on registration; displayed in admin roster immediately after registration.

## Research Summary

**Event Architecture Decision:**
- Identity maintains 3 local cache tables via event handlers:
  1. **ClassLecturerCache** (ClassId → ClassName, LecturerId): consumed from ClassLecturerAssigned event published by Catalog on Class create/lecturer-reassign. Enables fast class name resolution and authorization checks without cross-service HTTP.
  2. **SubmissionStudent** (SubmissionId → StudentId): consumed from the existing SubmissionUploaded event.
  3. **SubmissionGrader** (SubmissionId → LecturerId): consumed from the existing GradePublished event.
  - "Which lecturers graded this student" is computed by **joining SubmissionGrader and SubmissionStudent on SubmissionId at query time** (Phase 4), not eagerly merged into a StudentId-keyed set at event-handling time. The original eager-merge design was rejected during plan review: SubmissionUploaded and GradePublished are independent events from two different services with no guaranteed arrival order, and an eager merge has no clean way to handle GradePublished landing before SubmissionUploaded for the same submission without either losing the fact or reaching for a forbidden HTTP fallback. Storing each event's fact in its own PK-keyed table and joining at read time sidesteps the ordering problem entirely — see Phase 2 for the full rationale.

**Authorization Strategy:**
- `PATCH /users/{userId}` is authorized when: caller is admin, OR caller is lecturer AND (their id == target student's class lecturer from ClassLecturerCache) OR (their id appears in the SubmissionGrader⋈SubmissionStudent join for that student). Enforced server-side per-request via local cache queries, not just role membership.

**Batch Endpoint Design:**
- Both `GET /users?ids=...` and `GET /grades/final?submissionIds=...` deduplicate requested IDs and query via `WHERE id IN (...)` in a single query, never looping. Performance guaranteed at 3 total requests for any size grade table (1 submissions + 1 grades batch + 1 users batch).

**Anonymity Exception:**
- `GET /catalog/classes` is the only deliberately anonymous endpoint in this feature set (returns Id+Name only, no LecturerId), since user-web's pre-login registration form needs it to populate the class picker before JWT is available.

**File Upload Pattern:**
- Bulk roster import is synchronous (not a background job) because class-sized rosters (typically 20–50 rows) can be processed in milliseconds. Per-row authorization is checked server-side; rows outside the uploader's scope are skipped with a reason string reported back.

**Frontend Architecture:**
- admin-web grade table joins 3 backend results client-side (submissions, batch grades, batch users) to avoid N+1 and unnecessary batch endpoints. Filtering (MSSV partial/exact, Class exact/partial, both with AND logic) is applied in-browser. Excel export via SheetJS (`xlsx` package) runs in-browser from the filtered DOM state, not a backend call.

## Plan Review

`plan-reviewer` returned **BLOCK** on the first pass of this 11-phase version, with 1 CRITICAL and 4 HIGH findings, all in Phase 2 — resolved before cook starts:

- **CRITICAL — event ordering race**: the original Phase 2 design eagerly merged `SubmissionUploaded`+`GradePublished` into a single `StudentGraderCache` (StudentId → LecturerId set) at event-handling time. Since those two events come from different services with no ordering guarantee, `GradePublished` arriving first would have nowhere to attach the `LecturerId` (no `StudentId` known yet), and the plan's stated mitigation (an HTTP fallback to Submission service) contradicted this codebase's no-direct-inter-service-HTTP convention. **Fixed** by storing each event's fact in its own PK-keyed table (`SubmissionStudent`, `SubmissionGrader`, both keyed by `SubmissionId`) and deferring the join to authorization-check time in Phase 4 — order of arrival no longer matters, no data is ever lost, no HTTP fallback needed.
- **HIGH — cache structure/idempotency underspecified** (3 related findings): resolved as a consequence of the above — both new tables are simple single-row upserts keyed by their PK, not a collection-replace operation, so there's no ambiguity about "one-to-many vs composite key" or how idempotent redelivery works.
- Phase 2 now includes an explicit test requirement: deliver `GradePublished` before `SubmissionUploaded` for the same `SubmissionId` and assert the join still resolves correctly once both arrive — this is the exact scenario that was underspecified before.

2 NOTED findings (event-bus-downtime monitoring, anonymous `GET /classes` enumeration risk) are accepted as documented, non-blocking risks — already reflected in the Risks section below.

**Re-review pass** (targeted, verifying the CRITICAL fix): confirmed the query-time-join design genuinely eliminates the event-ordering race in both orderings, no HTTP fallback needed. Surfaced 2 more small gaps, both fixed: (1) the 3 event handlers' `SaveChangesAsync()` calls needed explicit `DbUpdateException` handling for the concurrent-redelivery PK-race case described in Phase 2's Risks (was documented as a risk but missing from the Steps — now in Phase 2 Step 7a, with a matching test); (2) the authorization helper's return type was underspecified — now an explicit `RosterAuthorizationResult` enum (Admin/ClassLecturer/Grader/Denied) so Phase 5's bulk import can report which reason a row was skipped for. Also updated spec.md FR-06 to state explicitly that "grader" means the *current* (most recent) publisher of a submission, not a historical record — this was an implicit consequence of the fixed design that the spec's original wording didn't make clear.

## Dependencies

- **Event bus availability:** Identity must receive ClassLecturerAssigned, SubmissionUploaded, GradePublished events. If the bus is down during publish, cache updates are delayed (race condition noted in spec as acceptable, self-resolving on retry).
- **No new cross-service HTTP:** All cross-service data flows through events. Identity reads no HTTP from Catalog/Submission/Grading; it only subscribes to their events.
- **Existing events (SubmissionUploaded, GradePublished):** Already published by Submission and Grading services with the required shape; no changes needed.

## Risks

- **HIGH: Event Ordering Race** — If ClassLecturerAssigned is delayed or reordered relative to a registration that specifies a ClassId, the user might fail validation (classId unknown in cache yet). *Mitigation:* Add clear error message to registration ("Class not found; try again in a moment or contact admin"); implement idempotent registration retry logic in FE.
- **HIGH: Authorization Cache Staleness** — If a lecturer is assigned to a class and immediately tries to edit a student in that class before ClassLecturerAssigned propagates to Identity's cache, the edit fails with 403 (race condition). *Mitigation:* Spec explicitly accepts this as a rare, self-resolving race; add client-side retry and clear message to UX.
- **MEDIUM: Bulk Import Performance** — Processing 1000+ rows synchronously could block the request thread. *Mitigation:* Codebase assumption is class-sized rosters (20–50 rows); add comment in code documenting this. If real data exceeds this, refactor to a background job in a future iteration.
- **MEDIUM: Cross-Service Authorization Correctness** — If the two cache tables get out of sync with reality (e.g., a SubmissionUploaded event is lost), a lecturer might not be able to edit a student they should have access to. *Mitigation:* Add logging/monitoring for event consumption errors; consider a future admin endpoint to manually rebuild caches from events.
- **LOW: Excel Export Encoding** — Very old Excel versions may not handle UTF-8 BOM correctly for non-ASCII characters. *Mitigation:* SheetJS defaults to proper XLSX encoding; this is a known-good library.
- **LOW: Filtering UI Confusion** — Users might not understand that both filters use AND logic. *Mitigation:* Add a clear label in the UI ("Showing rows where MSSV contains X AND Class contains Y").

## Risks to Monitor (Not Blocking)

- If Identity's local cache grows very large (10000+ classes or 100000+ students with gradings), query performance may degrade. Current plan assumes school-scale enrollments; optimize with DB indices if needed in a future release.
