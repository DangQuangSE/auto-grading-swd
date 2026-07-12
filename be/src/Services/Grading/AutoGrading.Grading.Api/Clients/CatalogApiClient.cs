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
}
