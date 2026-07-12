namespace AutoGrading.Grading.Api.Domain;

public class LocalRubricCriterion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LocalRubricId { get; set; }
    public Guid RubricCriterionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MaxScore { get; set; }
    public int OrderIndex { get; set; }

    public LocalRubric? LocalRubric { get; set; }
}
