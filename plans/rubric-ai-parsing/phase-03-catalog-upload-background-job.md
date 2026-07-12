# Phase 3: Catalog Upload & Background Job

## Requirements

Update the rubric upload endpoint to enqueue an async background job for AI extraction instead of creating a hardcoded placeholder; implement the background job following the existing `AiGradingJob` pattern, handling both new uploads and re-uploads (which should replace the existing rubric in place).

## Steps

1. Create a new `RubricParsingJob.cs` in the Catalog service Jobs folder, following the structure of the existing `AiGradingJob` in the Grading service: accept the rubric ID as a constructor/Enqueue parameter.
2. In the job's Execute method, retrieve the rubric by ID from the database, fetch the uploaded file from S3 using the stored file object key, pass the document text to `OpenRouterClient.ParseRubricCriteriaAsync()`, persist the returned criteria as child records/collections attached to the rubric, and set the rubric's `Status` to `Draft`.
3. Handle job failure gracefully: if the AI extraction or file retrieval fails, log the error and let Hangfire retry; the rubric will remain in `Parsing` state.
4. Update the `UploadRubricAsync` endpoint to:
   - Accept a `Scope` parameter in the upload request (default: `Lecturer`). If `Scope = SchoolWide`, verify the current user has the `admin` role; reject with 403 otherwise. Populate `Rubric.Scope` from this value.
   - Authorize re-upload: if the assignment already has a rubric, verify the current user is either that rubric's `LecturerId` or has the `admin` role; reject with 403 if not.
   - On authorized re-upload: delete the old file from MinIO via the existing `FileObjectKey` (using `IObjectStorage`) before storing the new upload, to avoid orphaning storage; clear existing criteria; set `Status = Parsing`; update the row in place. Preserve the rubric's existing `LecturerId` — re-upload does not change ownership, only file/criteria/Status are reset.
   - Otherwise (no existing rubric for the assignment), create a new rubric with `LecturerId` set to the current user's id (or null if `Scope = SchoolWide`).
5. In the upload endpoint, after storing the file to S3, enqueue the `RubricParsingJob` with the rubric ID.
6. Register `RubricParsingJob` in `Program.cs` with `AddScoped<RubricParsingJob>()` and configure it with Hangfire (both the job class registration and a call to add Hangfire processing).
7. Add a `POST /rubrics/{id}/retry-parsing` endpoint: verifies the rubric's `Status` is `Parsing` (i.e. a previous attempt is stuck — reject with 409 if `Draft` or `Confirmed`, since re-parsing a reviewed rubric should go through re-upload instead), checks authorization (owner or admin), and re-enqueues `RubricParsingJob` for the existing file without requiring a new upload.
8. Verify the upload endpoint responds immediately (before the job completes) with the rubric ID and current status.
9. Verify re-uploading a rubric file for the same assignment updates the existing rubric row and re-enqueues the job.
10. Verify `retry-parsing` re-enqueues the job for a rubric stuck in `Parsing` and is rejected for a `Draft`/`Confirmed` rubric.

## Success Criteria

- `RubricParsingJob` class exists at `be/src/Services/Catalog/AutoGrading.Catalog.Api/Jobs/RubricParsingJob.cs`
- Upload endpoint enqueues the job and returns immediately with rubric ID and `Status = Parsing`
- Background job fetches the file, calls `ParseRubricCriteriaAsync`, and persists criteria to the rubric
- Upon job completion, the rubric's `Status` transitions to `Draft`
- Re-uploading a file for an existing assignment results in exactly one `Rubric` row for that assignment
- The endpoint does not block the user's HTTP response; upload completes in the same order of magnitude as before the change
- Re-upload is rejected with 403 for a user who is neither the rubric's owning lecturer nor an admin
- Uploading with `Scope = SchoolWide` is rejected with 403 for a non-admin caller
- The old file's `FileObjectKey` no longer exists in MinIO after a re-upload replaces it
- `POST /rubrics/{id}/retry-parsing` exists, re-enqueues the job for a `Parsing` rubric, and returns 409 for `Draft`/`Confirmed` rubrics

## Risks

- **Concurrent uploads of the same assignment** — If two uploads happen simultaneously for the same assignment, both jobs may run and attempt to update the same rubric row. *Mitigation:* In the background job's Execute method, verify that the rubric's `Status` is still `Parsing` before processing; if it has moved to `Draft` or `Confirmed`, exit early (superseded by another upload).
- **AI extraction timeout** — If the OpenRouter API is slow or the rubric is large, the job may exceed the Hangfire job timeout. *Mitigation:* Configure Hangfire with a reasonable timeout (e.g., 60 seconds) and rely on Hangfire's built-in retry mechanism; if timeouts persist, this is a future tuning point.
- **File not found after upload** — The job might run before the file is fully persisted to S3, or the file key might be incorrect. *Mitigation:* Add a check in the job to verify the file exists in S3 before attempting to read it; if not, fail with a clear error to retry.

---
