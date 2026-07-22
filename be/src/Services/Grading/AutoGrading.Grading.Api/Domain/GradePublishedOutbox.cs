namespace AutoGrading.Grading.Api.Domain;

public sealed class GradePublishedOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid FinalGradeId { get; set; }
    public decimal FinalScore { get; set; }
    public Guid PublishedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DispatchedAt { get; set; }
}
