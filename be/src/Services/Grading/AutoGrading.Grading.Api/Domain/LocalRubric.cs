namespace AutoGrading.Grading.Api.Domain;

/// <summary>
/// Local read-only copy of a Catalog rubric's confirmed criteria, populated by consuming the
/// RubricConfirmed event. AiGradingJob reads from here instead of calling Catalog at grading
/// time, so grading has no runtime dependency on Catalog.
/// </summary>
public class LocalRubric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RubricId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? AssignmentId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public DateTimeOffset ConfirmedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<LocalRubricCriterion> Criteria { get; set; } = new();
}
