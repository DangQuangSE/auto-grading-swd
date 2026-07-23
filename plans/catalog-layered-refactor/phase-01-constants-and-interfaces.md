# Phase 1: Constants + Interfaces (additive only)

## Requirements

Create the new folders and contracts with zero behavior change. Nothing that currently works stops working — this phase only adds files and moves two interface declarations.

## Design Constraints

- Zero behavior change — this phase must compile and the app must run identically to before, since nothing yet calls the new repository/service interfaces.
- Repository/service method names are derived from what the caller needs (e.g. `ListAsync`, `GetByIdAsync`, `CreateAsync`), not from raw CRUD — concurrency-critical operations like enrollment upserts own their entire transaction boundary inside a single method, never split across a "check" + "insert" pair.
- Do not touch any `Endpoints/`, `Jobs/`, or `Program.cs` in this phase — this is groundwork only.
- Do NOT delete `Parsing/DocxRubricParser.cs` and `Parsing/IRubricParser.cs` yet — that happens in Phase 5 after the last code that might theoretically reference them has been re-verified as genuinely dead. Phase 1 is additive-only.

## Steps

1. Create `Constant/CatalogConstants.cs` with error message strings currently inlined across the five endpoint files:
   - Subject-related: "Subject code already exists", "Subject does not exist", "invalid_registration_status", "Status must be open or closed"
   - Assignment-related: "MaxAttempts must be at least 1", "Assignment does not exist"
   - Class-related: "invalid_class_name", "Name is required", "Name must be at most 256 characters", "invalid_lecturer", "LecturerId is required", "invalid_subject", "Subject does not exist", "class_subject_locked", "A class with enrollments cannot be moved to another subject", "class_conflict", "Class data conflicts with an existing class"
   - Enrollment-related: "invalid_enrollment", "SubjectId and ClassId are required", "invalid_row_version", "RowVersion must be an 8-byte base64 value", "subject_not_found", "Subject does not exist", "registration_closed", "Subject registration is closed", "class_subject_mismatch", "Class does not belong to the subject", "enrollment_missing", "Enrollment no longer exists. Refresh and retry", "row_version_required", "Refresh the enrollment before changing it", "enrollment_conflict", "Enrollment could not be saved because the data changed", "stale_enrollment", "Enrollment changed. Refresh and retry", "enrollment_not_found", "Enrollment does not exist"
   - Rubric-related: "invalid_rubric", "Rubric does not exist", "Rubric {id} is '{status}', not '{expected}' — {action} instead", "Rubric was modified concurrently; reload and try again"

2. Create five repository interfaces in `Interfaces/`:
   - `ISubjectRepository.cs` — methods: `ListAsync(search?, page?, pageSize?, ct)`, `ListOpenAsync(page?, pageSize?, ct)`, `GetByIdAsync(id, ct)`, `CreateAsync(subject, ct)`, `UpdateRegistrationAsync(id, status, ct)`
   - `IAssignmentRepository.cs` — methods: `ListAsync(subjectId?, page?, pageSize?, ct)`, `GetByIdAsync(id, ct)`, `CreateAsync(assignment, ct)`, `UpdateAsync(id, assignment, ct)`
   - `IClassRepository.cs` — methods: `ListAsync(page?, pageSize?, ct)`, `ListAdminAsync(subjectId?, page?, pageSize?, ct)`, `ListForSubjectAsync(subjectId, page?, pageSize?, ct)`, `GetByIdAsync(id, ct)`, `CreateAsync(class, ct)`, `UpdateAsync(id, class, ct)`, `AnyAsync(id, ct)`, `AnyWithEnrollmentsAsync(id, ct)`
   - `IEnrollmentRepository.cs` — methods: `ListStudentAsync(studentId, page?, pageSize?, ct)`, `ListAdminAsync(studentId?, subjectId?, classId?, page?, pageSize?, ct)`, `ListStudentIdsForLecturerAsync(lecturerId, subjectId, ct)`, `UpsertStudentAsync(studentId, subjectId, classId, rowVersion?, ct)` (returning `EnrollmentCommandResult<EnrollmentSummary>`), `CorrectAdminAsync(studentId, subjectId, classId, rowVersion?, ct)` (returning `EnrollmentCommandResult<AdminEnrollmentSummary>`)
   - `IRubricRepository.cs` — methods: `ListAsync(subjectId?, assignmentId?, userId?, isAdmin, ct)`, `GetByIdAsync(id, includeCriteria, ct)`, `GetByAssignmentIdAsync(assignmentId, ct)`, `CreateAsync(rubric, ct)`, `UpdateAsync(rubric, ct)`, `UpdateCriteriaAsync(rubric, criteria, ct)`, `ConfirmAsync(rubric, ct)`, `UnlockAsync(rubric, ct)`, `DownloadFileAsync(id, ct)`

3. Create five service interfaces in `Interfaces/`:
   - `ISubjectService.cs` — methods: `ListAsync(search?, page?, pageSize?, ct)`, `ListOpenAsync(page?, pageSize?, ct)`, `GetByIdAsync(id, ct)`, `CreateAsync(request, ct)`, `UpdateRegistrationAsync(id, status, ct)`
   - `IAssignmentService.cs` — methods: `ListAsync(subjectId?, page?, pageSize?, ct)`, `GetByIdAsync(id, ct)`, `CreateAsync(request, ct)`, `UpdateAsync(id, request, ct)`
   - `IClassService.cs` — methods: `ListLegacyAsync(ct)`, `ListAdminAsync(subjectId?, page?, pageSize?, ct)`, `ListForSubjectAsync(subjectId, page?, pageSize?, isStudent, ct)`, `GetByIdAsync(id, ct)`, `CreateLegacyAsync(request, ct)`, `CreateSubjectScopedAsync(request, ct)`, `UpdateAsync(id, request, ct)`
   - `IEnrollmentService.cs` — methods: `ListStudentAsync(studentId, page?, pageSize?, ct)`, `ListAdminAsync(studentId?, subjectId?, classId?, page?, pageSize?, ct)`, `ListStudentIdsForLecturerAsync(lecturerId, subjectId, ct)`, `UpsertStudentAsync(studentId, subjectId, classId, rowVersion?, ct)` (returning `EnrollmentCommandResult<EnrollmentSummary>`), `CorrectAdminAsync(studentId, subjectId, classId, rowVersion?, ct)` (returning `EnrollmentCommandResult<AdminEnrollmentSummary>`)
   - `IRubricService.cs` — methods: `ListAsync(subjectId?, assignmentId?, userId?, isAdmin, ct)`, `GetByIdAsync(id, includeCriteria, ct)`, `UploadAsync(form, userId, isAdmin, ct)`, `RetryParsingAsync(id, userId, isAdmin, ct)`, `UpdateCriteriaAsync(id, criteria, userId, isAdmin, ct)`, `ConfirmAsync(id, userId, isAdmin, ct)`, `UnlockAsync(id, userId, isAdmin, ct)`, `DownloadFileAsync(id, userId, isAdmin, ct)`

4. Create a new `Dto/` folder (currently only `EnrollmentContracts.cs` in `Endpoints/`). Do NOT move `EnrollmentContracts.cs` yet — Phase 1 is additive only. Just create the folder; it will be populated in Phase 4.

5. Create domain exception types (or place in `Interfaces/`):
   - These are not strictly needed in Phase 1 (endpoints don't throw them yet), but defining them now ensures they're available for Phase 3's Service layer. If Submission/Grading/Identity precedent applies: keep them lightweight, sealed, with simple `message` and optional `data` fields for carrying validation error details (e.g., `EnrollmentConflictException(string message, EnrollmentSummary? current)`).

## Success Criteria

- `dotnet build` on `AutoGrading.Catalog.Api` compiles with zero errors/warnings.
- New folders exist: `Constant/`, `Dto/`, `Interfaces/`.
- Five repository interfaces and five service interfaces are defined and compile.
- No change to any existing endpoint behavior — all 25 routes still work exactly as before.

## Quality and Testing State

- Quality gate: not evaluated (Cook runs `/ck:quality --gate` after implementing this phase)
- Testing: not started

## Manual Verification

1. `dotnet build` on `AutoGrading.Catalog.Api` — must compile with zero errors/warnings introduced.
2. Verify new files exist: `Constant/CatalogConstants.cs`, `Interfaces/ISubjectRepository.cs`, `Interfaces/IAssignmentRepository.cs`, `Interfaces/IClassRepository.cs`, `Interfaces/IEnrollmentRepository.cs`, `Interfaces/IRubricRepository.cs`, `Interfaces/ISubjectService.cs`, `Interfaces/IAssignmentService.cs`, `Interfaces/IClassService.cs`, `Interfaces/IEnrollmentService.cs`, `Interfaces/IRubricService.cs`.
3. Run the service locally (`dotnet run` or via docker-compose), confirm all 25 routes (4 Subject + 4 Assignment + 6 Class + 7 Rubric + 2 Student Enrollment + 2 Admin Enrollment + 1 Lecturer Enrollment) behave exactly as before — smoke check that no reference was broken by interface creation.
