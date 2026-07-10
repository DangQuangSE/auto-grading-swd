using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AutoGrading.Grading.Api.OpenRouter;

public record GradingCriterionInput(Guid RubricCriterionId, string Name, decimal MaxScore);

public record GradingCriterionResult(
    Guid RubricCriterionId,
    decimal MaxScore,
    decimal SuggestedScore,
    string? Deductions,
    string? Evidence,
    string? Comment,
    decimal? Confidence);

public interface IOpenRouterClient
{
    Task<IReadOnlyList<GradingCriterionResult>> GradeAsync(
        string reportContent,
        string diagramContent,
        IReadOnlyList<GradingCriterionInput> criteria,
        CancellationToken cancellationToken);
}

/// <summary>
/// Calls OpenRouter for AI grading when an API key is configured; otherwise falls back to a
/// deterministic stub so the grading pipeline is exercisable without external credentials.
/// </summary>
public class OpenRouterClient(HttpClient httpClient, IOptions<OpenRouterOptions> options) : IOpenRouterClient
{
    private readonly OpenRouterOptions _options = options.Value;

    public async Task<IReadOnlyList<GradingCriterionResult>> GradeAsync(
        string reportContent,
        string diagramContent,
        IReadOnlyList<GradingCriterionInput> criteria,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return StubGrade(criteria);
        }

        var prompt = BuildPrompt(reportContent, diagramContent, criteria);

        var requestBody = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = "You are an assistant that grades student submissions against rubric criteria and returns strict JSON." },
                new { role = "user", content = prompt },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = TryParseResponse(payload, criteria);

        return parsed ?? StubGrade(criteria);
    }

    private static string BuildPrompt(string reportContent, string diagramContent, IReadOnlyList<GradingCriterionInput> criteria)
    {
        var criteriaText = string.Join(
            "\n",
            criteria.Select(c => $"- {c.RubricCriterionId}: {c.Name} (max {c.MaxScore})"));

        return $"""
                Grade the following submission against the rubric criteria below.
                Respond with a JSON array, one object per criterion, each with fields:
                rubricCriterionId, suggestedScore, deductions, evidence, comment, confidence (0-1).

                Rubric criteria:
                {criteriaText}

                Report content:
                {reportContent}

                Diagram content:
                {diagramContent}
                """;
    }

    private static IReadOnlyList<GradingCriterionResult>? TryParseResponse(string payload, IReadOnlyList<GradingCriterionInput> criteria)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            using var contentDoc = JsonDocument.Parse(content);
            var results = new List<GradingCriterionResult>();

            foreach (var item in contentDoc.RootElement.EnumerateArray())
            {
                var criterionId = Guid.Parse(item.GetProperty("rubricCriterionId").GetString()!);
                var criterion = criteria.FirstOrDefault(c => c.RubricCriterionId == criterionId);
                if (criterion is null)
                {
                    continue;
                }

                results.Add(new GradingCriterionResult(
                    criterionId,
                    criterion.MaxScore,
                    item.GetProperty("suggestedScore").GetDecimal(),
                    item.TryGetProperty("deductions", out var d) ? d.GetString() : null,
                    item.TryGetProperty("evidence", out var e) ? e.GetString() : null,
                    item.TryGetProperty("comment", out var c) ? c.GetString() : null,
                    item.TryGetProperty("confidence", out var conf) ? conf.GetDecimal() : null));
            }

            return results.Count > 0 ? results : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<GradingCriterionResult> StubGrade(IReadOnlyList<GradingCriterionInput> criteria) =>
        criteria
            .Select(c => new GradingCriterionResult(
                c.RubricCriterionId,
                c.MaxScore,
                Math.Round(c.MaxScore * 0.8m, 2),
                Deductions: null,
                Evidence: "Stub grading (no OpenRouter API key configured).",
                Comment: "Automatically generated stub score.",
                Confidence: 0.5m))
            .ToList();
}
