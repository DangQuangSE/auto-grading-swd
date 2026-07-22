namespace AutoGrading.Catalog.Api.Domain;

public class Class
{
    public Class()
    {
        Id = Guid.NewGuid();
        EnrollmentSubjectId = Id;
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public Guid LecturerId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid EnrollmentSubjectId { get; set; }
    public Subject? Subject { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<StudentEnrollment> Enrollments { get; set; } = new();
}
