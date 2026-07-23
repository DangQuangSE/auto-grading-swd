using AutoGrading.SubmissionSvc.Api.Constant;
using AutoGrading.SubmissionSvc.Api.Domain;

namespace AutoGrading.SubmissionSvc.Api.Interfaces;

public interface ISubmissionRepository
{
    Task<IReadOnlyList<Submission>> ListAsync(Guid? assignmentId, IReadOnlyCollection<Guid>? restrictToStudentIds, Guid? studentId, CancellationToken ct);

    Task<Submission?> GetByIdAsync(Guid id, bool includeArtifacts, CancellationToken ct);

    /// <summary>Owns the Serializable transaction internally: attempt-limit check and insert happen atomically.</summary>
    Task<Submission> CreateWithAttemptCheckAsync(Guid assignmentId, Guid studentId, int maxAttempts, CancellationToken ct);

    Task SaveUploadResultAsync(Submission submission, string reportObjectKey, string? diagramObjectKey, CancellationToken ct);

    /// <summary>Rollback path when object storage upload fails after the submission row was created.</summary>
    Task DeleteAsync(Submission submission, CancellationToken ct);

    /// <summary>Clears old artifacts for the submission and resets its state to Uploaded.</summary>
    Task ResetForRetryAsync(Guid submissionId, CancellationToken ct);

    /// <summary>Used by <c>Jobs/ExtractionJob.cs</c> to move the submission through the
    /// Uploaded -> Extracting -> Extracted/Failed state machine.</summary>
    Task UpdateStateAsync(Guid submissionId, SubmissionState state, CancellationToken ct);

    Task AddExtractedArtifactAsync(Guid submissionId, ExtractedArtifact artifact, CancellationToken ct);
}

public sealed class SubmissionAttemptLimitReachedException(int used, int max) : Exception(SubmissionConstants.AttemptLimitReached)
{
    public int Used { get; } = used;
    public int Max { get; } = max;
}

public sealed class SubmissionAttemptConflictException(int used, int max) : Exception(SubmissionConstants.AttemptConflict)
{
    public int Used { get; } = used;
    public int Max { get; } = max;
}
