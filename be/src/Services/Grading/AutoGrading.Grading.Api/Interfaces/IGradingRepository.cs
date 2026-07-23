using AutoGrading.Grading.Api.Domain;

namespace AutoGrading.Grading.Api.Interfaces;

public interface IGradingRepository
{
    Task<IReadOnlyList<AiGradingRun>> GetRunsForSubmissionAsync(Guid submissionId, CancellationToken ct);

    Task<GradePublication?> GetLatestPublicationAsync(Guid submissionId, CancellationToken ct);

    Task<FinalGrade?> GetFinalGradeByIdAsync(Guid finalGradeId, CancellationToken ct);

    Task<AiGradingRun?> GetRunByIdAsync(Guid runId, Guid submissionId, CancellationToken ct);

    Task<FinalGrade?> GetLatestFinalGradeAsync(Guid submissionId, CancellationToken ct);

    Task<IReadOnlyList<FinalGrade>> GetLatestFinalGradesBatchAsync(IReadOnlyCollection<Guid> submissionIds, CancellationToken ct);

    Task<bool> IsRunCompletedAsync(Guid runId, Guid submissionId, CancellationToken ct);

    Task<bool> HasCompletedRunAsync(Guid submissionId, CancellationToken ct);

    /// <summary>Owns the atomic 3-table write internally: creates <see cref="FinalGrade"/>,
    /// <see cref="GradePublication"/>, and <see cref="GradePublishedOutbox"/> together, using the
    /// same isolation level as the current code (default <c>ReadCommitted</c> — no <c>Serializable</c>).
    /// On failure, clears the change tracker so a subsequent call on the same scoped DbContext
    /// (e.g. from a batch-publish loop) is not corrupted by the failed attempt's tracked entities.</summary>
    Task<FinalGrade> PublishAsync(Guid submissionId, Guid? runId, decimal score, string? notes, Guid userId, CancellationToken ct);

    Task<int> CountPublicationsAsync(CancellationToken ct);

    Task<IReadOnlyList<AiGradingRun>> GetUnpublishedCompletedRunsBatchAsync(int batchSize, CancellationToken ct);

    Task AddRunAsync(AiGradingRun run, CancellationToken ct);

    Task UpdateRunStatusAsync(Guid runId, AiGradingRunStatus status, DateTimeOffset completedAt, CancellationToken ct);

    Task AddCriterionScoresAsync(Guid runId, IReadOnlyList<AiCriterionScore> scores, CancellationToken ct);

    Task<IReadOnlyList<GradePublishedOutbox>> GetPendingOutboxMessagesAsync(int batchSize, CancellationToken ct);

    Task MarkOutboxDispatchedAsync(Guid outboxId, CancellationToken ct);
}
