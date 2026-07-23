using AutoGrading.Grading.Api.Interfaces;

namespace AutoGrading.Grading.Api.Dto;

/// <summary>Narrower batch-endpoint projection — a deliberate smaller shape already used by the
/// original <c>GetFinalGradesBatchAsync</c>, not a new omission (contrast with the full-entity
/// mirror <see cref="FinalGradeDetailResponse"/> used by the single-submission routes).</summary>
public sealed record FinalGradeResponse(Guid SubmissionId, Guid FinalGradeId, decimal FinalScore, DateTimeOffset CreatedAt)
{
    public static FinalGradeResponse FromData(FinalGradeData data) => new(data.SubmissionId, data.FinalGradeId, data.FinalScore, data.CreatedAt);
}
