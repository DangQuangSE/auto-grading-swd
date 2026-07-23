namespace AutoGrading.Grading.Api.Dto;

public sealed record PublishAllResponse(int Published, int Skipped, int Failed);
