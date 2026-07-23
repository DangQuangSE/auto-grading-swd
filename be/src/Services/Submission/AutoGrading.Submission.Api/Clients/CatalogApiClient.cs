using System.Net.Http.Json;
using AutoGrading.SubmissionSvc.Api.Interfaces;

namespace AutoGrading.SubmissionSvc.Api.Clients;

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
