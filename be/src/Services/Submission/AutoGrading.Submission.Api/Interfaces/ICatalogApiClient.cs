namespace AutoGrading.SubmissionSvc.Api.Interfaces;

public sealed record AssignmentDto(Guid Id, Guid SubjectId, int MaxAttempts);

public interface ICatalogApiClient
{
    Task<AssignmentDto?> GetAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);

    /// <summary>Student ids enrolled in any class the given lecturer teaches for the given subject
    /// (a lecturer may teach several classes of the same subject).</summary>
    Task<HashSet<Guid>> GetLecturerStudentIdsAsync(Guid lecturerId, Guid subjectId, CancellationToken cancellationToken);
}
