# Phase 6: DI Wiring + Full Regression

## Requirements

Verify all dependency injection wiring is in place. Run a comprehensive manual regression pass covering all 25 routes across all 5 concerns, with special attention to high-risk areas (enrollment concurrency, rubric workflow, class event publishing).

## Design Constraints

- All five repositories and five services must be registered in `Program.cs` (done in Phases 2/3, but verify here).
- `RubricParsingJob` must be registered and have `IRubricRepository` available at runtime.
- No breaking changes to the HTTP API contract — responses must be byte-for-byte identical to before the refactor (excluding JSON property ordering, which is ignored).
- Manual verification is mandatory and comprehensive — this phase's verification section lists every route explicitly, not "all routes."

## Steps

1. Verify `Program.cs` DI registration (should be done by earlier phases, but double-check):
   - `AddDbContext<CatalogDbContext>` (line 15–16).
   - Five repositories: `AddScoped<ISubjectRepository, SubjectRepository>()`, `AddScoped<IAssignmentRepository, AssignmentRepository>()`, `AddScoped<IClassRepository, ClassRepository>()`, `AddScoped<IEnrollmentRepository, EnrollmentRepository>()`, `AddScoped<IRubricRepository, RubricRepository>()`.
   - Five services: `AddScoped<ISubjectService, SubjectService>()`, `AddScoped<IAssignmentService, AssignmentService>()`, `AddScoped<IClassService, ClassService>()`, `AddScoped<IEnrollmentService, EnrollmentService>()`, `AddScoped<IRubricService, RubricService>()`.
   - `AddScoped<RubricParsingJob>()` (already there from Phase 2).
   - All other existing registrations (`AddHttpClient`, event bus, storage, OpenCode client, Hangfire, health checks) remain unchanged.

2. Full-solution build:
   - `dotnet build` on the entire solution (all services, shared libraries) — must compile with zero errors/warnings.
   - Spot-check: `dotnet build AutoGrading.Catalog.Api.csproj` specifically — zero errors.

3. Run database migrations if needed:
   - Catalog service may have pending migrations (none expected in this refactor, but verify).
   - Run migrations in dev environment (docker-compose or local SQL Server).

4. Start the service locally and verify HTTP layer:
   - `dotnet run` or `docker-compose up catalog` (depending on local dev environment).
   - Swagger should be accessible at `/swagger/` or `/swagger/index.html`.
   - Health check `/health` should return 200 OK.

## Success Criteria

- `dotnet build` on entire solution compiles with zero errors.
- Service starts without DI errors (`no ISubjectRepository registered` etc.).
- All 25 routes respond (no 404 routing errors).
- All 25 routes produce correct responses with correct shapes (manual regression pass below).

## Quality and Testing State

- Quality gate: not evaluated (Cook runs `/ck:quality --gate` after implementing this phase)
- Testing: not started (manual regression is the full verification step)

## Manual Verification

**Setup:** Open Swagger at `http://localhost:{port}/swagger/` or use Postman/curl. For each route, verify request succeeds and response matches expected shape.

### Subjects (4 routes)

1. **GET /subjects/** (page=1, pageSize=10, no search) — 200 OK, returns paginated list of `SubjectSummary` objects with `{ id, code, name, registrationStatus, createdAt }`.
2. **GET /subjects/open-for-registration** (page=1, pageSize=10) — 200 OK, returns only subjects with `registrationStatus: "open"`.
3. **POST /subjects/** with `{ "code": "CS101", "name": "Computer Science 101" }` — 201 Created, returns `SubjectSummary` for the newly created subject.
4. **PATCH /subjects/{id}/registration** with `{ "status": "open" }` — 200 OK, toggles registration status, returns updated `SubjectSummary`. Try toggling back to `"closed"` to confirm round-trip.

### Assignments (4 routes)

5. **GET /assignments/** (subjectId=null, page=1, pageSize=10) — 200 OK, returns paginated list of `AssignmentResponse` objects with `{ id, subjectId, title, description, dueDate, maxAttempts, createdAt }`.
6. **GET /assignments/{id}** (use an assignment from step 5) — 200 OK, returns single `AssignmentResponse`. Try with invalid ID, confirm 404 NotFound.
7. **POST /assignments/** with `{ "subjectId": "{from step 3}", "title": "HW1", "maxAttempts": 2 }` — 201 Created, returns the new assignment. Try with `maxAttempts: 0`, confirm 400 BadRequest.
8. **PUT /assignments/{id}** with updated title/maxAttempts — 200 OK, returns updated assignment.

### Classes (6 routes)

9. **GET /classes/** (no filters) — 200 OK, returns list of `LegacyClassSummary` objects (no pagination on legacy endpoint).
10. **GET /classes/admin** (page=1, pageSize=10, subjectId=null) — 200 OK, returns paginated list of `ClassSummary` objects with `{ id, name, lecturerId, subjectId, subjectCode }`.
11. **GET /classes/by-subject/{subjectId}** (use subject from step 3, page=1, pageSize=10) — 200 OK if subject registration is open, returns `RegistrationClassOption` objects. Try with a closed-registration subject as student role, confirm 404 NotFound.
12. **POST /classes/** with `{ "name": "Class A", "lecturerId": "{some-uuid}" }` — 201 Created, returns `ClassSummary`. Event `ClassLecturerAssigned` should be published (verify via event logs or Rabbit MQ dashboard if accessible).
13. **POST /classes/subject-scoped** with `{ "name": "Class B", "lecturerId": "{some-uuid}", "subjectId": "{from step 3}" }` — 201 Created. Try with invalid subjectId, confirm 400 BadRequest.
14. **PATCH /classes/{id}** (use a class from steps 12–13) with `{ "lecturerId": "{new-lecturer-id}" }` — 200 OK, returns updated class. Try changing subjectId while class has enrollments (created later), confirm 409 Conflict.

### Rubrics (7 routes)

15. **GET /rubrics/** (subjectId=null, assignmentId=null) as lecturer — 200 OK, returns list of rubrics (only Confirmed ones + own Draft/Parsing ones). As admin, should see more.
16. **GET /rubrics/{id}/file** (use a rubric with an uploaded file) — 200 OK with `application/vnd.openxmlformats-officedocument.wordprocessingml.document` content-type. Try with invalid ID, confirm 404 NotFound.
17. **POST /rubrics/upload** with multipart form (file + subjectId + name + scope) — 201 Created, rubric status = Parsing, Hangfire job enqueued (check Hangfire dashboard). Response includes the created rubric.
18. **POST /rubrics/{id}/retry-parsing** (use the rubric from step 17, still in Parsing status) — 202 Accepted, Hangfire job re-enqueued. Try on a non-Parsing rubric, confirm 409 Conflict.
19. **PATCH /rubrics/{id}/criteria** with array of `{ name, description, maxScore, orderIndex }` objects (rubric must be in Draft status for this to work — may need to wait for step 17's job to complete) — 200 OK, criteria updated, returns array of updated criteria.
20. **POST /rubrics/{id}/confirm** (rubric in Draft status with criteria) — 200 OK, status transitions to Confirmed, `RubricConfirmed` event published. Try on an already-Confirmed rubric, confirm 409 Conflict.
21. **POST /rubrics/{id}/unlock** (rubric in Confirmed status) — 200 OK, status transitions back to Draft. Try on a Parsing rubric, confirm 409 Conflict.

### Enrollments (5 routes) — CRITICAL

22. **GET /enrollments/me** (as student role) with page=1, pageSize=10 — 200 OK, returns paginated list of `EnrollmentSummary` objects with `{ id, subjectId, subjectCode, subjectName, registrationStatus, classId, className, rowVersion, createdAt, updatedAt }`. RowVersion should be a base64-encoded string.
23. **PUT /enrollments/me/{subjectId}** (as student, subjectId from step 3 with open registration, classId from a class in that subject) with `{ "classId": "{id}", "rowVersion": null }` (null for first enroll) — 200 OK, creates enrollment, returns `EnrollmentSummary` with a rowVersion. Re-run with same classId and the returned rowVersion, confirm **idempotent** (200 OK, no duplicate).
24. **Concurrent enrollment conflict test (high-risk):** From step 23, extract the rowVersion. In parallel (two separate HTTP clients or rapid Postman runs):
    - Client A: PUT with rowVersion from step 23, new classId.
    - Client B: PUT with same rowVersion, different classId.
    - Expected: One succeeds (200), one gets 409 Conflict with `current` field populated (showing the now-current enrollment). Both should NOT succeed.
25. **GET /enrollments/admin** (as admin) with page=1, pageSize=10, filters for the studentId and subjectId from earlier — 200 OK, returns paginated list of `AdminEnrollmentSummary` objects (same shape as student summary but includes `studentId` field).
26. **PUT /enrollments/admin/{studentId}/{subjectId}** (as admin, using IDs from enrollment created in step 23) with `{ "classId": "{new-class-id}", "rowVersion": null }` (or extract actual rowVersion from step 25) — 200 OK, updates enrollment. Try with a rowVersion from step 25, then a stale rowVersion, confirm 409 Conflict on stale.
27. **GET /enrollments/lecturer-student-ids** (as lecturer, subjectId from step 3) — 200 OK, returns list of Guid student IDs (distinct set of all students enrolled in any class that lecturer teaches for that subject).

### Cross-concern Integration

28. **Class subject change with enrollments:** Create a subject (step 3), class in it (step 13), enroll a student (step 23). Try to change the class's subject to a different subject (step 14, PATCH with new subjectId) — confirm 409 Conflict "class_subject_locked" since it now has enrollments.

29. **Rubric visibility by role:** Upload and confirm a rubric (steps 17–20). As student role, try `GET /rubrics/` — should see only Confirmed rubrics, not Draft/Parsing ones. As lecturer owner (if different from current user), same visibility rule applies.

30. **Event publishing verification:** Throughout the tests above, verify these events are published (check RabbitMQ logs, event bus traces, or event store if available):
    - `ClassLecturerAssigned` on class creation/update (step 12–14).
    - `RubricParsed` when parsing job completes (watch Hangfire dashboard in step 17–18, then check for event after job finishes).
    - `RubricConfirmed` on rubric confirmation (step 20).

### Final Checks

31. `dotnet build` one more time — zero errors, zero warnings.
32. All 12 unique endpoint route groups are responsive and return correct shapes:
    - GET /subjects/
    - GET /subjects/open-for-registration
    - POST /subjects/
    - PATCH /subjects/{id}/registration
    - GET /assignments/
    - GET /assignments/{id}
    - POST /assignments/
    - PUT /assignments/{id}
    - GET /classes/
    - GET /classes/admin
    - GET /classes/by-subject/{subjectId}
    - POST /classes/
    - POST /classes/subject-scoped
    - PATCH /classes/{id}
    - GET /rubrics/
    - GET /rubrics/{id}/file
    - POST /rubrics/upload
    - POST /rubrics/{id}/retry-parsing
    - PATCH /rubrics/{id}/criteria
    - POST /rubrics/{id}/confirm
    - POST /rubrics/{id}/unlock
    - GET /enrollments/me
    - PUT /enrollments/me/{subjectId}
    - GET /enrollments/admin
    - PUT /enrollments/admin/{studentId}/{subjectId}
    - GET /enrollments/lecturer-student-ids

**Total: 25 route operations across 5 concerns, all verified manually.**
