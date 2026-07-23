using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoGrading.Grading.Api.Interfaces;

namespace AutoGrading.Grading.Api.Clients;

/// <summary>Fetches a submission and its extracted report/diagram content from the Submission service.</summary>
public sealed class SubmissionApiClient(HttpClient httpClient) : ISubmissionApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<SubmissionDto?> GetSubmissionAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"/submissions/{submissionId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions, cancellationToken);
    }
}
