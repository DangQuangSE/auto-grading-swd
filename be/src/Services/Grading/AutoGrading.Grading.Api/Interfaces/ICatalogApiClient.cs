namespace AutoGrading.Grading.Api.Interfaces;

public sealed record RubricCriterionDto(Guid Id, string Code, string Name, string? Description, decimal MaxScore, int OrderIndex);

public sealed record RubricDto(Guid Id, Guid SubjectId, Guid? AssignmentId, string Name, List<RubricCriterionDto> Criteria);

public sealed record AssignmentDto(Guid Id, Guid SubjectId, string Title, string? Description);

public interface ICatalogApiClient
{
    Task<IReadOnlyList<RubricCriterionDto>> GetCriteriaForAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);
    Task<AssignmentDto?> GetAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);

    /// <summary>Student ids enrolled in any class the given lecturer teaches for the given subject
    /// (a lecturer may teach several classes of the same subject).</summary>
    Task<HashSet<Guid>> GetLecturerStudentIdsAsync(Guid lecturerId, Guid subjectId, CancellationToken cancellationToken);
}
