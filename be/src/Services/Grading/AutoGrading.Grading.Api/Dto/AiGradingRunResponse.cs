using AutoGrading.Grading.Api.Domain;

namespace AutoGrading.Grading.Api.Dto;

public sealed record AiGradingRunResponse(
    Guid Id,
    Guid SubmissionId,
    string Model,
    AiGradingRunStatus Status,
    string? RequestMetadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<AiCriterionScoreResponse> Scores)
{
    public static AiGradingRunResponse FromDomain(AiGradingRun run) => new(
        run.Id,
        run.SubmissionId,
        run.Model,
        run.Status,
        run.RequestMetadata,
        run.CreatedAt,
        run.CompletedAt,
        run.Scores.Select(AiCriterionScoreResponse.FromDomain).ToList());
}
