using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoGrading.Common.OpenCode;

public record GradingCriterionInput(Guid RubricCriterionId, string Name, decimal MaxScore);

public record GradingCriterionResult(
    Guid RubricCriterionId,
    decimal MaxScore,
    decimal SuggestedScore,
    string? Deductions,
    string? Evidence,
    string? Comment,
    decimal? Confidence);

public record ExtractedRubricCriterion(string Name, string? Description, decimal MaxScore, int Order);

public interface IOpenCodeClient
{
    Task<IReadOnlyList<GradingCriterionResult>> GradeAsync(
        string reportContent,
        string diagramContent,
        IReadOnlyList<GradingCriterionInput> criteria,
        string? assignmentDescription,
        IReadOnlyList<string>? images,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExtractedRubricCriterion>> ParseRubricCriteriaAsync(
        string documentText,
        CancellationToken cancellationToken);
}

/// <summary>
/// Calls OpenCode for AI grading and rubric-criteria extraction when an API key is configured;
/// otherwise falls back to a deterministic stub so callers are exercisable without external credentials.
/// </summary>
public class OpenCodeClient(HttpClient httpClient, IOptions<OpenCodeOptions> options, ILogger<OpenCodeClient> logger) : IOpenCodeClient
{
    private readonly OpenCodeOptions _options = options.Value;

    public async Task<IReadOnlyList<GradingCriterionResult>> GradeAsync(
        string reportContent,
        string diagramContent,
        IReadOnlyList<GradingCriterionInput> criteria,
        string? assignmentDescription,
        IReadOnlyList<string>? images,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return StubGrade(criteria);
        }

        var prompt = BuildGradingPrompt(reportContent, diagramContent, criteria, assignmentDescription);
        var payload = await SendChatCompletionAsync(prompt, cancellationToken);
        var parsed = TryParseGradingResponse(payload, criteria);

        return parsed ?? StubGrade(criteria);
    }

    public async Task<IReadOnlyList<ExtractedRubricCriterion>> ParseRubricCriteriaAsync(
        string documentText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return StubRubricCriteria("Stub criterion (no OpenCode API key configured).");
        }

        var prompt = BuildRubricExtractionPrompt(documentText);
        var payload = await SendChatCompletionAsync(prompt, cancellationToken);
        var parsed = TryParseRubricCriteriaResponse(payload);

        if (parsed is not null)
        {
            return parsed;
        }

        logger.LogWarning(
            "OpenCodeClient: rubric-criteria extraction response could not be parsed into valid criteria; falling back to stub. Response length: {PayloadLength}",
            payload.Length);

        return StubRubricCriteria("AI extraction could not parse a valid response for this document; add criteria manually.");
    }

    private async Task<string> SendChatCompletionAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = "You are an assistant that returns strict JSON and nothing else." },
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

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string BuildGradingPrompt(string reportContent, string diagramContent, IReadOnlyList<GradingCriterionInput> criteria, string? assignmentDescription)
    {
        var criteriaText = string.Join(
            "\n",
            criteria.Select(c => $"- {c.RubricCriterionId}: {c.Name} (max {c.MaxScore})"));

        var assignmentSection = string.IsNullOrWhiteSpace(assignmentDescription)
            ? string.Empty
            : $"\nAssignment description:\n{assignmentDescription}\n";

        return $"""
                Grade the following submission against the rubric criteria below.
                Respond with a JSON array, one object per criterion, each with fields:
                rubricCriterionId, suggestedScore, deductions, evidence, comment, confidence (0-1).
                {assignmentSection}
                Rubric criteria:
                {criteriaText}

                Report content:
                {reportContent}

                Diagram content:
                {diagramContent}
                """;
    }

    private static string BuildRubricExtractionPrompt(string documentText)
    {
        return $"""
                Extract the grading criteria from the rubric document below.
                Respond with a JSON array, one object per criterion, each with fields:
                name (string), description (string, may be empty), maxScore (number), order (integer, 0-based).

                Rubric document:
                {documentText}
                """;
    }

    private static IReadOnlyList<GradingCriterionResult>? TryParseGradingResponse(string payload, IReadOnlyList<GradingCriterionInput> criteria)
    {
        var criteriaById = criteria.ToDictionary(c => c.RubricCriterionId);

        return TryParseJsonArray<GradingCriterionResult>(payload, item =>
        {
            var criterionId = Guid.Parse(item.GetProperty("rubricCriterionId").GetString()!);
            if (!criteriaById.TryGetValue(criterionId, out var criterion))
            {
                return null;
            }

            return new GradingCriterionResult(
                criterionId,
                criterion.MaxScore,
                item.GetProperty("suggestedScore").GetDecimal(),
                item.TryGetProperty("deductions", out var d) ? d.GetString() : null,
                item.TryGetProperty("evidence", out var e) ? e.GetString() : null,
                item.TryGetProperty("comment", out var c) ? c.GetString() : null,
                item.TryGetProperty("confidence", out var conf) ? conf.GetDecimal() : null);
        });
    }

    internal static IReadOnlyList<ExtractedRubricCriterion>? TryParseRubricCriteriaResponse(string payload)
    {
        var order = 0;

        return TryParseJsonArray<ExtractedRubricCriterion>(payload, item =>
        {
            if (!item.TryGetProperty("name", out var nameProp) || nameProp.GetString() is not { Length: > 0 } name)
            {
                return null;
            }

            if (!item.TryGetProperty("maxScore", out var maxScoreProp) || !maxScoreProp.TryGetDecimal(out var maxScore))
            {
                return null;
            }

            var description = item.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
            var itemOrder = item.TryGetProperty("order", out var orderProp) && orderProp.TryGetInt32(out var parsedOrder)
                ? parsedOrder
                : order;

            order++;
            return new ExtractedRubricCriterion(name, description, maxScore, itemOrder);
        });
    }

    /// <summary>Shared defensive-parse skeleton: extracts the AI message content, parses it as a JSON array,
    /// and runs <paramref name="itemParser"/> per element, skipping items it rejects (returns null for).</summary>
    private static IReadOnlyList<T>? TryParseJsonArray<T>(string payload, Func<JsonElement, T?> itemParser)
        where T : class
    {
        try
        {
            var content = ExtractMessageContent(payload);
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            using var contentDoc = JsonDocument.Parse(StripCodeFence(content));
            var results = new List<T>();

            foreach (var item in contentDoc.RootElement.EnumerateArray())
            {
                var parsed = itemParser(item);
                if (parsed is not null)
                {
                    results.Add(parsed);
                }
            }

            return results.Count > 0 ? results : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractMessageContent(string payload)
    {
        using var doc = JsonDocument.Parse(payload);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    /// <summary>Some models wrap JSON responses in a markdown code fence (```json ... ```) despite being
    /// asked for strict JSON; strip that fence so <see cref="JsonDocument.Parse(string)"/> can still succeed.</summary>
    private static string StripCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
        {
            return trimmed;
        }

        var withoutOpeningFence = trimmed[(firstNewline + 1)..];
        var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        var end = closingFenceIndex >= 0 ? closingFenceIndex : withoutOpeningFence.Length;

        return withoutOpeningFence[..end].Trim();
    }

    private static IReadOnlyList<GradingCriterionResult> StubGrade(IReadOnlyList<GradingCriterionInput> criteria) =>
        criteria
            .Select(c => new GradingCriterionResult(
                c.RubricCriterionId,
                c.MaxScore,
                Math.Round(c.MaxScore * 0.8m, 2),
                Deductions: null,
                Evidence: "Stub grading (no OpenCode API key configured).",
                Comment: "Automatically generated stub score.",
                Confidence: 0.5m))
            .ToList();

    private static IReadOnlyList<ExtractedRubricCriterion> StubRubricCriteria(string reason) =>
        [new ExtractedRubricCriterion("Overall Quality", reason, 10m, 0)];
}
