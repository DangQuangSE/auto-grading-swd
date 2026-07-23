namespace AutoGrading.Grading.Api.Dto;

public sealed record PublishGradeRequest(Guid? GradingRunId, decimal FinalScore, string? Notes);
