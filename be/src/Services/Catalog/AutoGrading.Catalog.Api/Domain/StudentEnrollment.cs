namespace AutoGrading.Catalog.Api.Domain;

public class StudentEnrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
