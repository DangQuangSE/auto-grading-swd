using System.Net.Http.Json;

namespace AutoGrading.SubmissionSvc.Api.Clients;

public sealed record AssignmentDto(Guid Id, int MaxAttempts);

public interface ICatalogApiClient
{
    Task<AssignmentDto?> GetAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);
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
}
