namespace AutoGrading.Catalog.Api.Domain;

public class Subject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public RegistrationStatus RegistrationStatus { get; set; } = RegistrationStatus.Closed;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Assignment> Assignments { get; set; } = new();
    public List<Class> Classes { get; set; } = new();
    public List<StudentEnrollment> Enrollments { get; set; } = new();
}
