using AutoGrading.Grading.Api.Domain;

namespace AutoGrading.Grading.Api.Interfaces;

/// <summary>Auth-framework-free view of the caller, built by the endpoint from <c>ClaimsPrincipal</c>
/// before calling into <c>Service/</c> — keeps the service free of ASP.NET Core auth types.</summary>
public sealed record RequesterContext(Guid? UserId, bool IsStudent, bool IsLecturer, bool IsAdmin);

public sealed record PublishAllResult(int Published, int Skipped, int Failed);

public sealed record FinalGradeData(Guid SubmissionId, Guid FinalGradeId, decimal FinalScore, DateTimeOffset CreatedAt);

/// <summary>
/// <c>GradingDone</c> is <see langword="null"/> when a publication row exists but its <see cref="FinalGrade"/>
/// is missing (a defensive, should-never-happen data-integrity edge case) — the endpoint maps that to a bare
/// 404 with no body, matching current behavior exactly. When there is no publication at all, <c>GradingDone</c>
/// carries a real <see langword="true"/>/<see langword="false"/> so the endpoint can include it in the 404 body.
/// </summary>
public sealed record PublishedResultData(FinalGrade? Grade, DateTimeOffset? PublishedAt, AiGradingRun? Run, bool? GradingDone);

public interface IGradingService
{
    Task<IReadOnlyList<AiGradingRun>> GetRunsForRequesterAsync(Guid submissionId, RequesterContext requester, CancellationToken ct);

    Task<PublishedResultData> GetPublishedResultForRequesterAsync(Guid submissionId, RequesterContext requester, CancellationToken ct);

    Task<FinalGrade?> GetFinalGradeForRequesterAsync(Guid submissionId, RequesterContext requester, CancellationToken ct);

    Task<IReadOnlyList<FinalGradeData>> GetFinalGradesBatchForRequesterAsync(IReadOnlyCollection<Guid> submissionIds, RequesterContext requester, CancellationToken ct);

    Task RegradeAsync(Guid submissionId, string? assignmentDescriptionOverride, RequesterContext requester, CancellationToken ct);

    Task<FinalGrade> PublishGradeAsync(Guid submissionId, Guid? gradingRunId, decimal finalScore, string? notes, RequesterContext requester, CancellationToken ct);

    Task<PublishAllResult> PublishAllAsync(RequesterContext requester, CancellationToken ct);
}

public sealed class GradingForbiddenException() : Exception("Requester is not authorized to access this submission.");

public sealed class InvalidGradingRunException(string message) : Exception(message);
