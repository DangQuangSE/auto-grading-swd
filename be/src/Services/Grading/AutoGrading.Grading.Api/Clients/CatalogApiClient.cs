using System.Net.Http.Json;
using System.Text.Json;
using AutoGrading.Grading.Api.Interfaces;

namespace AutoGrading.Grading.Api.Clients;

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
