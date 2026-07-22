using System.Net.Http.Json;

namespace AutoGrading.SubmissionSvc.Api.Clients;

public sealed record AssignmentDto(Guid Id, Guid SubjectId, int MaxAttempts);

public interface ICatalogApiClient
{
    Task<AssignmentDto?> GetAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);

    /// <summary>Student ids enrolled in any class the given lecturer teaches for the given subject
    /// (a lecturer may teach several classes of the same subject).</summary>
    Task<HashSet<Guid>> GetLecturerStudentIdsAsync(Guid lecturerId, Guid subjectId, CancellationToken cancellationToken);
}

public sealed class CatalogApiClient(HttpClient httpClient) : ICatalogApiClient
{
    public async Task<AssignmentDto?> GetAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"/assignments/{assignmentId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AssignmentDto>(cancellationToken: cancellationToken);
    }

    public async Task<HashSet<Guid>> GetLecturerStudentIdsAsync(Guid lecturerId, Guid subjectId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            $"/enrollments/lecturer-student-ids?subjectId={subjectId}&lecturerId={lecturerId}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var ids = await response.Content.ReadFromJsonAsync<List<Guid>>(cancellationToken: cancellationToken);
        return ids is null ? [] : [.. ids];
    }
}
