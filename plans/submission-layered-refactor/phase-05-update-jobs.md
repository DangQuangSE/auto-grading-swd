# Phase 5: Update Jobs

**Maps to:** P1 story 2 (data access đứng sau interface) — extends the boundary to the background job.

## Goal

`Jobs/ExtractionJob.cs` stops depending on `SubmissionDbContext` directly and depends on `ISubmissionRepository` instead, so EF Core is fully contained inside `Repository/`. `Jobs/SubmissionUploadedHandler.cs` needs no change (already verified thin — only enqueues the Hangfire job, no DB access).

## Steps

1. Add to `ISubmissionRepository` (Phase 1/2 file) whatever narrow methods `ExtractionJob` needs that aren't already covered:
   ```
   Task<Submission?> GetForExtractionAsync(Guid submissionId, CancellationToken ct); // Include(Artifacts)
   Task UpdateStateAsync(Guid submissionId, SubmissionState state, CancellationToken ct);
   Task AddExtractedArtifactAsync(Guid submissionId, ExtractedArtifact artifact, CancellationToken ct);
   ```
   (Reuse `GetByIdAsync(id, includeArtifacts: true, ct)` from Phase 1 instead of a new method if it fits — don't add a duplicate.)

2. Update `Jobs/ExtractionJob.cs` constructor to inject `ISubmissionRepository` instead of `SubmissionDbContext`; replace the direct `db.Submissions...`/`db.ExtractedArtifacts.Add`/`db.SaveChangesAsync` calls (lines 24-35, 60-69, 79-81 of the current file) with the repository methods above. `IObjectStorage`, `IArtifactParser`, `IEventBus` stay exactly as-is (already interfaces, no change).

3. No change to `Jobs/SubmissionUploadedHandler.cs` — confirmed in the brainstorm/research pass that it only calls `IBackgroundJobClient.Enqueue<ExtractionJob>(...)`, no DB access.

## Design Constraints

- Do not introduce a separate `Service/ExtractionOrchestrator.cs` (see plan.md's "Deliberate deviation" note) — `ExtractionJob` remains the orchestrator, just decoupled from `SubmissionDbContext`.
- Preserve the exact state machine transitions (`Extracting` → `Extracted`/`Failed`) and the exact event publishing sequence (`SubmissionStatusChanged("Extracting")`, then `ArtifactsExtracted`, then conditionally `SubmissionStatusChanged("ExtractionFailed", ...)`).

## Quality and Testing State

- Quality: not evaluated
- Testing: not started

## Manual Verification

1. `dotnet build` — zero errors.
2. Upload a submission end-to-end, watch the Hangfire dashboard for `ExtractionJob` execution, confirm it transitions `Uploaded → Extracting → Extracted` (or `Failed` for a deliberately malformed docx) with the same artifact rows and warnings as before the change.
3. Trigger via the RabbitMQ path too (publish a `SubmissionUploaded` event directly, or use the real upload flow which publishes it) — confirm `SubmissionUploadedHandler` still enqueues correctly and the job still runs.
