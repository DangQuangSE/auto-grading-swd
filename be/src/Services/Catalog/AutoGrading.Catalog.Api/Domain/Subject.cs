namespace AutoGrading.Catalog.Api.Domain;

public class Subject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Assignment> Assignments { get; set; } = new();
}
