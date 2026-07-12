# Phase 4: User Listing & Batch Endpoints with Authorization

## Requirements

Implement three new user-related endpoints in Identity: GET /users (list-all, lecturer/admin-gated), GET /users?ids=... (batch lookup with deduplication, lecturer/admin-gated, resolves ClassId→ClassName from cache), and PATCH /users/{userId} with real server-side authorization logic (admin always allowed; lecturer allowed only if they own the student's class OR have published a grade for that student). This is the core authorization logic that enables lecturers to edit only their own roster.

## Steps

1. Create a helper method (or service class) in Identity that checks authorization for a target user: given a caller's UserId, caller's role, and a target User (need their `ClassId`), determine access as: `admin` → always allowed. `lecturer` → allowed if `ClassLecturerCache[target.ClassId].LecturerId == callerId` (skip if `target.ClassId` is null or not found in the cache), **OR** if `callerId` appears in `SELECT DISTINCT sg.LecturerId FROM SubmissionGrader sg JOIN SubmissionStudent ss ON sg.SubmissionId = ss.SubmissionId WHERE ss.StudentId == target.Id AND sg.LecturerId == callerId` (the query-time join from Phase 2 — a single EF query, e.g. `db.SubmissionGraders.Join(db.SubmissionStudents, g => g.SubmissionId, s => s.SubmissionId, (g, s) => new { s.StudentId, g.LecturerId }).AnyAsync(x => x.StudentId == target.Id && x.LecturerId == callerId)`). Any other role → not allowed. Return an explicit `enum RosterAuthorizationResult { Admin, ClassLecturer, Grader, Denied }` (not a bare boolean) so Phase 5's bulk import can report a specific skip reason per row rather than a generic "not authorized" for every denial.

2. Create a new endpoint group `UsersEndpoints` (following the Catalog/Grading pattern) with three mapped routes:
   - GET /users (list-all, lecturer/admin-gated, returns all users with Id, Email, FullName, StudentCode, ClassId, resolved ClassName)
   - GET /users?ids=... (batch lookup, lecturer/admin-gated, query string: `ids=guid1,guid2,guid3` or `ids=guid1&ids=guid2&ids=guid3`, deduplicate, returns array of users matching the provided IDs)
   - PATCH /users/{userId} (update MSSV/Class, authorized per the logic in step 1, accepts StudentCode and/or ClassId in the request)

3. For GET /users: query `db.Users.AsNoTracking()` with includes for related data as needed. Return only to lecturer/admin roles. Apply the authorization policy `RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"))`.

4. For GET /users?ids=...: parse the query string to extract the list of IDs (handle both comma-separated and repeated query parameters). Deduplicate using `HashSet<Guid>` or LINQ `.Distinct()`. Query using `db.Users.Where(u => ids.Contains(u.Id))` in a single EF Core query (never loop). For each returned user, resolve their ClassId to a ClassName by querying ClassLecturerCache. Return the batch result. Lecturer/admin-gated.

5. For PATCH /users/{userId}: extract the caller's UserId from the JWT claims. Query the target User. Check authorization via the helper from step 1. If not authorized, return `Results.Forbid()` (403). If authorized, update the provided fields (StudentCode and/or ClassId). If ClassId is provided, validate it against ClassLecturerCache (return 400 if unknown, same as Phase 3). Save changes and return the updated User. Lecturer/admin-gated.

6. For PATCH /users/{userId}, ensure the transaction is atomic: if validation fails (unknown ClassId), the entire request fails with 400, no partial update. Use SaveChangesAsync to catch concurrency issues.

7. Register the endpoints in Identity's Program.cs (e.g., `app.MapUsersEndpoints();`).

8. Create xUnit tests covering:
   - GET /users returns all users (admin/lecturer, no filtering by authorization yet — this is a list-all endpoint)
   - GET /users?ids=... deduplicates IDs, resolves ClassIds to ClassNames, returns correct batch
   - PATCH /users/{userId} with admin caller: always succeeds (updates StudentCode and/or ClassId)
   - PATCH /users/{userId} with lecturer caller who owns the class: succeeds
   - PATCH /users/{userId} with lecturer caller who has graded the student: succeeds
   - PATCH /users/{userId} with lecturer caller with no relationship: returns 403
   - PATCH /users/{userId} with non-lecturer/non-admin role: returns 403 (or 401 if unauthenticated)
   - PATCH /users/{userId} with unknown ClassId: returns 400 "Class not found"
   - StudentCode is updated independently of ClassId (can update one without the other)

9. Manual test: register two students in different classes; create a lecturer; assign the lecturer to class 1; attempt PATCH /users for student 1 (should succeed); attempt PATCH /users for student 2 (should fail with 403).

10. Run the migration (none needed for this phase since fields were added in Phase 3) and verify no compilation errors.

## Success Criteria

- Authorization helper method/service exists and is used by PATCH /users/{userId}
- GET /users endpoint exists (lecturer/admin-gated) and returns all users with resolved class names
- GET /users?ids=... endpoint exists (lecturer/admin-gated), deduplicates IDs, queries in a single `WHERE id IN (...)`, resolves ClassIds to ClassNames
- PATCH /users/{userId} endpoint exists with real authorization (admin, class-lecturer, grader)
- PATCH /users/{userId} admin callers always succeed
- PATCH /users/{userId} lecturer callers succeed only if they own the class or have graded the student
- PATCH /users/{userId} lecturer callers with no relationship get 403
- PATCH /users/{userId} validates ClassId against cache, returns 400 if unknown
- All unit tests pass (authorization logic, batch deduplication, ClassId resolution)
- Identity service compiles and starts without errors
- Manual test: lecturer cannot edit unrelated student; admin can edit any student

## Risks

- **Authorization cache/join staleness** — If the `SubmissionStudent`/`SubmissionGrader` rows for a submission haven't both landed yet (event lag), a lecturer who genuinely graded a student might be denied access until the missing event arrives. Per Phase 2's design, this self-resolves once both events land — there's no data loss, unlike the original (rejected) eager-merge design. *Mitigation:* Spec accepts this as a rare, self-resolving race; surface a clear "access denied — try again shortly if you just graded this student" message in the FE error state (Phase 8) rather than a generic 403.
- **Batch Query Performance** — GET /users?ids=... with 1000+ IDs could create a very large SQL `IN (...)` clause. *Mitigation:* Spec assumes admin-scale requests (10–100 IDs at a time). Add a comment noting this; if real usage exceeds 500 IDs, consider pagination in a future release.
- **Concurrent ClassId Validation** — If a ClassLecturerCache row is deleted between PATCH validation and the User save, the ClassId becomes "orphaned" (points to a deleted class). *Mitigation:* This is acceptable per spec (no FK constraint). Caches are event-driven; if a class is truly deleted, a new ClassDeleeted event would need to be defined and handled. For now, treat orphaned ClassIds as a future concern.

