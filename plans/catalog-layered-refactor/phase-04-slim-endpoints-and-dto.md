# Phase 4: Slim Endpoints + DTOs

## Requirements

Endpoints become pure HTTP adapters: bind request → call service → map response to `IResult`. Introduce `Dto/` response shapes to replace any direct-entity serialization. Keep endpoints thin (role checks, input validation that's HTTP-specific like `[FromForm]` binding) and push everything else into Service.

## Design Constraints

- Endpoints must not reference repositories directly — only services.
- Endpoints may check roles/claims (e.g., `user.IsInRole("admin")`) inline; this stays in Endpoints for now (no separate `IAuthorizationService`), mirroring Submission/Grading/Identity precedent.
- Request/response records move to `Dto/` folder from their various scattered locations (`Endpoints/*`).
- Entity serialization: if an endpoint currently returns a raw domain entity directly (`Results.Ok(assignment)` serializes the `Assignment` domain class), introduce a DTO and use `Dto/AssignmentResponse.FromDomain(assignment)` instead. **Byte-for-byte compatibility:** the DTO must produce identical JSON to the old entity serialization (same property names, same types), confirmed via before/after Swagger comparison.
- `EnrollmentHttpResults.From()` mapper stays in `Endpoints/` (it's already exactly what Phase 4 wants — result-to-`IResult` mapping at the boundary), but adapt it to handle whatever exception-based or result-type-based signaling `EnrollmentService` uses.
- `IFormFile` binding and orchestration (file upload to MinIO, Hangfire job enqueue) stays in `RubricsEndpoints.UploadAsync` and `RetryParsingAsync` — Service does not touch these concerns.

## Steps

1. Create `Dto/` response record types for each concern (move existing records from endpoint files + create new ones as needed):
   - **Subjects:** Move `SubjectSummary` from `SubjectsEndpoints.cs` to `Dto/SubjectResponse.cs` (add `FromDomain` static mapper).
   - **Assignments:** Create `Dto/AssignmentResponse.cs` mirroring current `Assignment` entity serialization (the endpoint currently returns raw domain entity, line 31/38/55/69).
   - **Classes:** Move `LegacyClassSummary`, `ClassSummary`, `RegistrationClassOption` from `ClassesEndpoints.cs` to `Dto/ClassResponse.cs` (add `FromDomain` mappers for each).
   - **Enrollments:** Move `UpsertEnrollmentRequest`, `EnrollmentSummary`, `AdminEnrollmentSummary` from `EnrollmentContracts.cs` to `Dto/EnrollmentResponse.cs`. Keep internal projection records (`EnrollmentProjection`, `AdminEnrollmentProjection`) in their original location or move to Repository if needed.
   - **Rubrics:** Create `Dto/RubricResponse.cs` mirroring current `Rubric` entity serialization (the endpoint currently returns raw domain entity, line 40/146).
   - Request records (`CreateSubjectRequest`, `UpdateSubjectRegistrationRequest`, etc.) either stay in Endpoints (if only used by one endpoint) or move to `Dto/` (if shared across endpoints or for consistency).

2. Refactor all five endpoint files to inject services + call them instead of repositories:
   - **`SubjectsEndpoints.cs`:**
     - Inject `ISubjectService service` instead of `ISubjectRepository`.
     - `ListSubjectsAsync` → call `service.ListAsync(search, page, pageSize, ct)`, map response via `SubjectResponse.FromDomain()`, return `Results.Ok(mapped)`.
     - `ListOpenSubjectsAsync` → call `service.ListOpenAsync(page, pageSize, ct)`, map, return.
     - `CreateSubjectAsync` → validate input (via existing inline checks or move to Service), call `service.CreateAsync(request, ct)`, catch exceptions, map success response, return.
     - `UpdateRegistrationAsync` → validate enum, call `service.UpdateRegistrationAsync(id, status, ct)`, map, return.
   - **`AssignmentsEndpoints.cs`:** Similar refactor, calling `IAssignmentService` methods and mapping responses.
   - **`ClassesEndpoints.cs`:** Similar refactor, calling `IClassService` methods. Keep role checks inline (lines 37, 92). Keep `NormalizeName`, `ValidateClassInput` either inline or move to Service (deferred to Phase 3 if needed). Keep `SaveAndPublishAsync` logic in Service or Repository per Phase 3 decisions.
   - **`RubricsEndpoints.cs`:**
     - `UploadRubricAsync` → validate scope/role inline, **upload to MinIO** (stays in Endpoints, lines 108–112), call `service.UploadAsync(form minus the file stream, userId, isAdmin, ct)` to save metadata, **enqueue Hangfire** (stays in Endpoints, line 144). **Order is mandatory and must match this exact sequence — do not reorder:**
       ```
       1. objectKey = await objectStorage.UploadAsync(file, ct);   // MinIO first, or throw before any DB write
       2. rubric = await service.UploadAsync(formWithoutStream, objectKey, userId, isAdmin, ct); // persists metadata row
       3. backgroundJobs.Enqueue<RubricParsingJob>(job => job.ExecuteAsync(rubric.Id, CancellationToken.None)); // enqueue last, matches original method name exactly
       ```
       **Do NOT** move the Hangfire enqueue into `RubricService` (Service must stay Hangfire-agnostic per this phase's Design Constraints). **Do NOT** defer the MinIO upload until after `service.UploadAsync` returns — the original code uploads the file before persisting the metadata row, so a failed upload never creates an orphaned DB row; reversing this would leave a "Parsing" rubric with no backing file in MinIO if the upload step failed after the DB write. Verify this exact ordering against the original `RubricsEndpoints.cs` (lines 87–150) before implementing, don't assume.
     - `RetryParsingAsync` → call `service.RetryParsingAsync(id, userId, isAdmin, ct)` to validate, **enqueue Hangfire** (stays in Endpoints, line 167).
     - `UpdateCriteriaAsync` → call `service.UpdateCriteriaAsync(...)`.
     - `ConfirmAsync` → call `service.ConfirmAsync(...)`, handle response/exceptions, publish event (stays in Endpoints or moves to Service per Phase 3 decision).
     - `UnlockAsync` → call `service.UnlockAsync(...)`.
     - `LoadAuthorizedRubricAsync`, `IsAuthorized`, `CanView` → move to Service as private helpers, or keep in Endpoints if they're only used by endpoint inline checks.
   - **`StudentEnrollmentEndpoints.cs` / `AdminEnrollmentEndpoints.cs` / `LecturerEnrollmentEndpoints.cs`:**
     - Inject `IEnrollmentService service`.
     - `StudentEnrollmentEndpoints.ListMineAsync` → call `service.ListStudentAsync(studentId, page, pageSize, ct)`, return.
     - `StudentEnrollmentEndpoints.UpsertMineAsync` → call `service.UpsertStudentAsync(studentId, subjectId, classId, rowVersion, ct)`, receive `EnrollmentCommandResult<EnrollmentSummary>`, call `EnrollmentHttpResults.From(result)` to map to `IResult`.
     - `AdminEnrollmentEndpoints.ListAsync` → call `service.ListAdminAsync(...)`.
     - `AdminEnrollmentEndpoints.CorrectAsync` → call `service.CorrectAdminAsync(...)`, map via `EnrollmentHttpResults.From()`.
     - `LecturerEnrollmentEndpoints.ListForLecturerAsync` → extract `effectiveLecturerId` logic, call `service.ListStudentIdsForLecturerAsync(effectiveLecturerId, subjectId, ct)`.

3. Delete or rename `Endpoints/EnrollmentQueries.cs` and `Endpoints/EnrollmentCommands.cs` (they've been merged into `Repository/EnrollmentRepository.cs` + `Service/EnrollmentService.cs`). Keep them for now if they aid readability (mark as deprecated/internal), or delete immediately if they're no longer referenced.

4. Move all remaining endpoint request/response records to `Dto/`:
   - `CreateSubjectRequest`, `UpdateSubjectRegistrationRequest` → `Dto/SubjectRequest.cs`.
   - `CreateAssignmentRequest`, `UpdateAssignmentRequest` → `Dto/AssignmentRequest.cs`.
   - `CreateLegacyClassRequest`, `CreateSubjectScopedClassRequest`, `UpdateClassRequest` → `Dto/ClassRequest.cs`.
   - `UploadRubricForm` → stays in `Endpoints/` (it's an `[FromForm]` binding type, HTTP-specific).
   - `UpdateCriterionRequest` → `Dto/RubricRequest.cs`.

5. Update `Program.cs`: no new DI needed (Phase 3 already added service registrations). Just verify service registrations are in place.

## Success Criteria

- `dotnet build` on `AutoGrading.Catalog.Api` compiles with zero errors.
- `Dto/` folder exists with response types for all five concerns.
- No repository injections in `Endpoints/`, only services.
- All 25 routes continue to work identically (manual pass, byte-for-byte JSON response comparison for entities that were previously serialized directly).

## Quality and Testing State

- Quality gate: not evaluated (Cook runs `/ck:quality --gate` after implementing this phase)
- Testing: not started

## Manual Verification

1. `dotnet build` — zero errors.
2. `grep -rn "ISubjectRepository\|IAssignmentRepository\|IClassRepository\|IEnrollmentRepository\|IRubricRepository" Endpoints/` returns nothing (only service injections).
3. Byte-for-byte JSON comparison for entity responses:
   - `GET /assignments/{id}` — compare response body before/after, confirm same properties/types (if it changed, a DTO was introduced that differs from the entity).
   - `GET /rubrics/` — same check.
   - `POST /subjects/` response body — same check.
4. Manually exercise all 25 routes end-to-end (via Swagger/Postman against a local run):
   - **All 25 routes pass** as before.
   - **Rubric upload orchestration verification:** Upload a rubric (file → MinIO → metadata in Service → Hangfire enqueue in Endpoints) — confirm status transitions to `Parsing` and Hangfire job is queued (check Hangfire dashboard).
   - **Concurrent enrollment conflict test:** Simulate concurrent updates to the same enrollment, confirm `409 Conflict` response with expected `current` field populated.
