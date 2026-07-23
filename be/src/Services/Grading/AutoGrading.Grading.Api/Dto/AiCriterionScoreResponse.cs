using AutoGrading.Grading.Api.Domain;

namespace AutoGrading.Grading.Api.Dto;

public sealed record AiCriterionScoreResponse(
    Guid Id,
    Guid GradingRunId,
    Guid SubmissionId,
    Guid RubricCriterionId,
    decimal MaxScore,
    decimal SuggestedScore,
    string? Deductions,
    string? Evidence,
    string? Comment,
    decimal? Confidence)
{
    public static AiCriterionScoreResponse FromDomain(AiCriterionScore score) => new(
        score.Id,
        score.GradingRunId,
        score.SubmissionId,
        score.RubricCriterionId,
        score.MaxScore,
        score.SuggestedScore,
        score.Deductions,
        score.Evidence,
        score.Comment,
        score.Confidence);
}
