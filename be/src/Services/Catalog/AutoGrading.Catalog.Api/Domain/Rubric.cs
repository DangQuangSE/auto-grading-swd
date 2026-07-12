namespace AutoGrading.Catalog.Api.Domain;

public class Rubric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubjectId { get; set; }
    public Guid? AssignmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FileObjectKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public RubricStatus Status { get; set; } = RubricStatus.Parsing;
    public RubricScope Scope { get; set; } = RubricScope.Lecturer;
    public Guid? LecturerId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Subject? Subject { get; set; }
    public Assignment? Assignment { get; set; }
    public List<RubricCriterion> Criteria { get; set; } = new();

    /// <summary>Transitions a locked rubric back to <see cref="RubricStatus.Draft"/> so its criteria can be edited again.</summary>
    public void Unlock()
    {
        if (Status != RubricStatus.Confirmed)
        {
            throw new InvalidOperationException($"Cannot unlock a rubric with status '{Status}'; only '{RubricStatus.Confirmed}' rubrics can be unlocked.");
        }

        Status = RubricStatus.Draft;
    }

    /// <summary>Locks a reviewed draft so its criteria become the version used for grading.</summary>
    public void Confirm()
    {
        if (Status != RubricStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot confirm a rubric with status '{Status}'; only '{RubricStatus.Draft}' rubrics can be confirmed.");
        }

        Status = RubricStatus.Confirmed;
    }
}
