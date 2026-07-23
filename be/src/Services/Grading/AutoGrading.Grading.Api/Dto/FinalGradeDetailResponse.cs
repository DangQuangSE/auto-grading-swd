using AutoGrading.Grading.Api.Domain;

namespace AutoGrading.Grading.Api.Dto;

/// <summary>Mirrors <see cref="FinalGrade"/> 1:1 — used by <c>GetFinalGradeAsync</c>, <c>PublishGradeAsync</c>,
/// and <c>PublishedGradeResult</c>, which all return the full entity today (unlike the batch endpoint's
/// narrower <see cref="FinalGradeResponse"/> projection, which was already a deliberate smaller shape
/// in the original code, not a new omission).</summary>
public sealed record FinalGradeDetailResponse(
    Guid Id,
    Guid SubmissionId,
    Guid? GradingRunId,
    decimal FinalScore,
    string? Notes,
    DateTimeOffset CreatedAt,
    Guid CreatedByUserId)
{
    public static FinalGradeDetailResponse FromDomain(FinalGrade grade) => new(
        grade.Id,
        grade.SubmissionId,
        grade.GradingRunId,
        grade.FinalScore,
        grade.Notes,
        grade.CreatedAt,
        grade.CreatedByUserId);
}
