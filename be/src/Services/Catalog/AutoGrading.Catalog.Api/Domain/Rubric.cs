namespace AutoGrading.Catalog.Api.Domain;

public class Rubric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubjectId { get; set; }
    public Guid? AssignmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FileObjectKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Subject? Subject { get; set; }
    public Assignment? Assignment { get; set; }
    public List<RubricCriterion> Criteria { get; set; } = new();
}
