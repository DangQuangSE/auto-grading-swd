namespace AutoGrading.Contracts.Enums;

public enum AppRole
{
    Student,
    Lecturer,
    Admin,

    /// <summary>Used for internal service-to-service JWTs (e.g. Grading calling Catalog/Submission); never issued to end users.</summary>
    Service,
}
