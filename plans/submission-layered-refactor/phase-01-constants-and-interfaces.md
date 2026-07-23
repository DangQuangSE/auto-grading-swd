# Phase 1: Constants + Interfaces (additive only)

**Maps to:** P1 story 2 ("data access đứng sau interface"), P1 story 3 (cấu trúc thư mục thống nhất) — groundwork only, no code path switches yet.

## Goal

Create the new folders and contracts with zero behavior change. Nothing that currently works stops working — this phase only adds files and moves two interface *declarations* (not their implementations).

## Steps

1. Create `Constant/SubmissionConstants.cs` with the error message strings currently inlined in `SubmissionsEndpoints.cs`:
   - `AssignmentNotFound = "Assignment not found."`
   - `StudentIdRequiredForLecturerUpload = "StudentId is required for lecturer/admin uploads."`
   - `AttemptLimitReached = "Submission attempt limit reached."`
   - `AttemptConflict = "Submission attempt conflict. Please refresh and try again."`
   - `AssignmentIdRequiredForLecturerListing = "assignmentId is required for lecturer submission listing."`
   (Do not use these yet — endpoint still has its own inline strings until Phase 4.)

2. Create `Interfaces/ISubmissionRepository.cs` — new contract, methods derived from what the endpoint currently does inline against `SubmissionDbContext`:
   ```
   Task<IReadOnlyList<Submission>> ListAsync(Guid? assignmentId, IReadOnlyCollection<Guid>? restrictToStudentIds, Guid? studentId, CancellationToken ct);
   Task<Submission?> GetByIdAsync(Guid id, bool includeArtifacts, CancellationToken ct);
   Task<Submission> CreateWithAttemptCheckAsync(Guid assignmentId, Guid studentId, int maxAttempts, CancellationToken ct); // throws AttemptLimitReachedException or AttemptConflictException — owns the Serializable transaction internally
   Task SaveUploadResultAsync(Submission submission, string reportObjectKey, string? diagramObjectKey, CancellationToken ct);
   Task DeleteAsync(Submission submission, CancellationToken ct); // rollback path when object storage upload fails
   Task ResetForRetryAsync(Guid submissionId, CancellationToken ct); // clears old artifacts, sets State = Uploaded
   ```
   Define two small exception types in the same `Interfaces/` namespace (or `Domain/Exceptions/`): `SubmissionAttemptLimitReachedException(int used, int max)` and `SubmissionAttemptConflictException(int used, int max)` — these carry the data the endpoint currently returns in `Results.Conflict(new { ... })`, so `Service`/`Endpoints` can map them without the repository leaking HTTP concerns.

3. Move `IArtifactParser` interface declaration from `Parsing/IArtifactParser.cs` to `Interfaces/IArtifactParser.cs`. Update the `namespace` and the `using` in `Parsing/ArtifactParser.cs` (and any other implementer) accordingly. No method signature changes.

4. Move `ICatalogApiClient` interface declaration out of `Clients/CatalogApiClient.cs` into `Interfaces/ICatalogApiClient.cs`. `Clients/CatalogApiClient.cs` keeps the concrete class, now `using` the interface from its new location. No method signature changes.

## Design Constraints

- Zero behavior change — this phase must compile and the app must run identically to before, since nothing yet calls the new repository interface.
- `ISubmissionRepository` methods are named after what the caller needs (`CreateWithAttemptCheckAsync`), not after raw CRUD — the Serializable transaction semantics from `SubmissionsEndpoints.cs:141-151` must be fully owned by whichever method implements attempt-checked creation, never split across a `Check()` + `Insert()` pair.
- Do not touch `SubmissionsEndpoints.cs`, `Jobs/*`, or `Program.cs` in this phase — this is groundwork only.

## Quality and Testing State

- Quality: not evaluated
- Testing: not started

## Manual Verification

1. `dotnet build` on `AutoGrading.Submission.Api` — must compile with zero errors/warnings introduced.
2. Run the service locally (`dotnet run` or via docker-compose), confirm `GET /submissions`, `GET /submissions/{id}`, `POST /submissions/upload`, `POST /submissions/{id}/retry` all behave exactly as before (nothing in this phase should have changed runtime behavior — this is a smoke check that the move didn't break a `using`/namespace reference).
