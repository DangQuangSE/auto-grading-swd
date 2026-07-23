namespace AutoGrading.Grading.Api.Dto;

public sealed record PublishedGradeResult(FinalGradeDetailResponse FinalGrade, DateTimeOffset PublishedAt, AiGradingRunResponse? GradingRun);
