namespace AutoGrading.Identity.Api.Domain;

/// <summary>Local read-only copy of Catalog's Class-to-lecturer assignment, kept in sync via the ClassLecturerAssigned event.</summary>
public class ClassLecturerCache
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public Guid LecturerId { get; set; }
}
