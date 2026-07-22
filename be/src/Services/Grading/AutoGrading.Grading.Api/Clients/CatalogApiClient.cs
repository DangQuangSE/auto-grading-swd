using System.Net.Http.Json;
using System.Text.Json;

namespace AutoGrading.Grading.Api.Clients;

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

public sealed class CatalogApiClient(HttpClient httpClient) : ICatalogApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<RubricCriterionDto>> GetCriteriaForAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var rubrics = await httpClient.GetFromJsonAsync<List<RubricDto>>(
            $"/rubrics?assignmentId={assignmentId}", JsonOptions, cancellationToken);

        return rubrics?.FirstOrDefault()?.Criteria ?? [];
    }

    public Task<AssignmentDto?> GetAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken) =>
        httpClient.GetFromJsonAsync<AssignmentDto>($"/assignments/{assignmentId}", JsonOptions, cancellationToken);

    public async Task<HashSet<Guid>> GetLecturerStudentIdsAsync(Guid lecturerId, Guid subjectId, CancellationToken cancellationToken)
    {
        var ids = await httpClient.GetFromJsonAsync<List<Guid>>(
            $"/enrollments/lecturer-student-ids?subjectId={subjectId}&lecturerId={lecturerId}", JsonOptions, cancellationToken);
        return ids is null ? [] : [.. ids];
    }
}
