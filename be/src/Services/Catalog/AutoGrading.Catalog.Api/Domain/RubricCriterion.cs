using System.Text.Json.Serialization;

namespace AutoGrading.Catalog.Api.Domain;

public class RubricCriterion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RubricId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MaxScore { get; set; }
    public int OrderIndex { get; set; }

    [JsonIgnore]
    public Rubric? Rubric { get; set; }
}
