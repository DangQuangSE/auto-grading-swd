using System.Text.Json.Serialization;

namespace AutoGrading.SubmissionSvc.Api.Domain;

public class ExtractedArtifact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public ArtifactKind Kind { get; set; }
    public string? Content { get; set; }
    public string? Warnings { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public Submission? Submission { get; set; }
}
