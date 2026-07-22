namespace AutoGrading.SubmissionSvc.Api.Domain;

public class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public int AttemptNumber { get; set; }
    public string ReportObjectKey { get; set; } = string.Empty;
    public string? DiagramObjectKey { get; set; }
    public SubmissionState State { get; set; } = SubmissionState.Uploaded;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ExtractedArtifact> Artifacts { get; set; } = new();
}
