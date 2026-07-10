namespace AutoGrading.Grading.Api.Domain;

public class AiGradingRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public string Model { get; set; } = string.Empty;
    public AiGradingRunStatus Status { get; set; } = AiGradingRunStatus.Running;
    public string? RequestMetadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public List<AiCriterionScore> Scores { get; set; } = new();
}
