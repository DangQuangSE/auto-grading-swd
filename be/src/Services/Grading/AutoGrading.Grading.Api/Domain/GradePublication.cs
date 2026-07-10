namespace AutoGrading.Grading.Api.Domain;

public class GradePublication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FinalGradeId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid PublishedByUserId { get; set; }
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
}
