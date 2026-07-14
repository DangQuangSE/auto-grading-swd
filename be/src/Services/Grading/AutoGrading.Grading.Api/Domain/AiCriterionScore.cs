using System.Text.Json.Serialization;

namespace AutoGrading.Grading.Api.Domain;

public class AiCriterionScore
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GradingRunId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid RubricCriterionId { get; set; }
    public decimal MaxScore { get; set; }
    public decimal SuggestedScore { get; set; }
    public string? Deductions { get; set; }
    public string? Evidence { get; set; }
    public string? Comment { get; set; }
    public decimal? Confidence { get; set; }

    [JsonIgnore]
    public AiGradingRun? GradingRun { get; set; }
}
