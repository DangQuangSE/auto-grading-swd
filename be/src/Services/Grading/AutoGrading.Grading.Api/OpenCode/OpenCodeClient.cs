using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoGrading.Common.OpenCode;
using Microsoft.Extensions.Options;

namespace AutoGrading.Grading.Api.OpenCode;

/// <summary>
/// Calls OpenCode for AI grading when an API key is configured; otherwise falls back to a
/// deterministic stub so the grading pipeline is exercisable without external credentials.
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
            logger.LogWarning("OpenCode API key is not configured; using stub grading.");
            return StubGrade(criteria, "Stub grading (no OpenCode API key configured).");
        }

        var prompt = BuildPrompt(reportContent, diagramContent, criteria, assignmentDescription);
        // Vision is opt-in because not every model behind OpenCode Zen is multimodal.
        var userContent = _options.EnableVision
            ? BuildUserContentWithImages(prompt, images)
            : prompt;

        var requestBody = new
        {
            model = _options.Model,
            max_tokens = _options.MaxCompletionTokens,
            messages = new object[]
            {
                new { role = "system", content = "You are an assistant that grades student submissions against rubric criteria and returns strict JSON." },
                new { role = "user", content = userContent },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("OpenCode request failed with {StatusCode}: {Body}", response.StatusCode, payload);
            return StubGrade(criteria, $"Stub grading (OpenCode request failed: {(int)response.StatusCode}).");
        }

        var parsed = TryParseResponse(payload, criteria, out var failureReason);
        if (parsed is not null)
        {
            return parsed;
        }

        logger.LogError("Failed to parse OpenCode response: {Reason}. Raw payload: {Payload}", failureReason, payload);
        return StubGrade(criteria, $"Stub grading (could not parse AI response: {failureReason}).");
    }

    public Task<IReadOnlyList<ExtractedRubricCriterion>> ParseRubricCriteriaAsync(
        string documentText,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ExtractedRubricCriterion>>(
            [new ExtractedRubricCriterion("Overall Quality", null, 10m, 0)]);

    private static object BuildUserContentWithImages(string prompt, IReadOnlyList<string>? images)
    {
        if (images is null or { Count: 0 })
        {
            return prompt;
        }

        var parts = new List<object> { new { type = "text", text = prompt } };
        parts.AddRange(images.Select(url => (object)new { type = "image_url", image_url = new { url } }));
        return parts;
    }

    private static string BuildPrompt(string reportContent, string diagramContent, IReadOnlyList<GradingCriterionInput> criteria, string? assignmentDescription)
    {
        var criteriaText = string.Join(
            "\n",
            criteria.Select(c => $"- {c.RubricCriterionId}: {c.Name} (max {c.MaxScore})"));

        var assignmentSection = string.IsNullOrWhiteSpace(assignmentDescription)
            ? string.Empty
            : $"\nAssignment description (mã đề):\n{assignmentDescription}\n";

        return $"""
                Grade the following submission against the rubric criteria below.
                Any images attached after this message are diagrams/screenshots embedded directly
                in the student's report document (e.g. UML class/sequence/activity diagrams) — inspect
                them visually and factor them into the score alongside the text content.
                Respond with a JSON array, one object per criterion, each with fields:
                rubricCriterionId, suggestedScore, deductions, evidence, comment, confidence (0-1).
                rubricCriterionId must be copied exactly as given below (a GUID string).
                {assignmentSection}
                Rubric criteria:
                {criteriaText}

                Report content:
                {reportContent}

                Diagram content:
                {(string.IsNullOrWhiteSpace(diagramContent) ? "(no diagram submitted)" : diagramContent)}
                """;
    }

    private static readonly Regex CodeFenceRegex = new(@"^```(?:json)?\s*|\s*```$", RegexOptions.Compiled | RegexOptions.Multiline);

    private static IReadOnlyList<GradingCriterionResult>? TryParseResponse(string payload, IReadOnlyList<GradingCriterionInput> criteria, out string failureReason)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var contentElement = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content");

            var content = contentElement.ValueKind == JsonValueKind.String
                ? contentElement.GetString()
                : contentElement.GetRawText();

            if (string.IsNullOrWhiteSpace(content))
            {
                failureReason = "empty message content";
                return null;
            }

            var jsonText = ExtractJsonArray(content);

            using var contentDoc = JsonDocument.Parse(jsonText);
            var results = new List<GradingCriterionResult>();
            var skipped = 0;

            foreach (var item in contentDoc.RootElement.EnumerateArray())
            {
                if (!TryResolveCriterion(item, criteria, out var criterionId, out var criterion))
                {
                    skipped++;
                    continue;
                }

                results.Add(new GradingCriterionResult(
                    criterionId,
                    criterion.MaxScore,
                    GetFlexibleDecimal(item, "suggestedScore") ?? 0m,
                    GetFlexibleString(item, "deductions"),
                    GetFlexibleString(item, "evidence"),
                    GetFlexibleString(item, "comment"),
                    GetFlexibleDecimal(item, "confidence")));
            }

            if (results.Count == 0)
            {
                failureReason = $"no matching rubric criteria in AI response (skipped {skipped} item(s))";
                return null;
            }

            failureReason = string.Empty;
            return results;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            failureReason = ex.Message;
            return null;
        }
    }

    private static bool TryResolveCriterion(
        JsonElement item,
        IReadOnlyList<GradingCriterionInput> criteria,
        out Guid criterionId,
        out GradingCriterionInput criterion)
    {
        criterionId = Guid.Empty;
        criterion = null!;

        if (!item.TryGetProperty("rubricCriterionId", out var idElement))
        {
            return false;
        }

        if (idElement.ValueKind == JsonValueKind.String && Guid.TryParse(idElement.GetString(), out var parsedGuid))
        {
            var match = criteria.FirstOrDefault(c => c.RubricCriterionId == parsedGuid);
            if (match is null)
            {
                return false;
            }

            criterionId = parsedGuid;
            criterion = match;
            return true;
        }

        // Fallback: some models substitute a 1-based ordinal instead of the GUID.
        if (idElement.ValueKind == JsonValueKind.Number && idElement.TryGetInt32(out var index) && index >= 1 && index <= criteria.Count)
        {
            criterion = criteria[index - 1];
            criterionId = criterion.RubricCriterionId;
            return true;
        }

        return false;
    }

    private static decimal? GetFlexibleDecimal(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(element.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static string? GetFlexibleString(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
    }

    private static string ExtractJsonArray(string content)
    {
        var trimmed = CodeFenceRegex.Replace(content, string.Empty).Trim();

        var start = trimmed.IndexOf('[');
        var end = trimmed.LastIndexOf(']');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : trimmed;
    }

    private static IReadOnlyList<GradingCriterionResult> StubGrade(IReadOnlyList<GradingCriterionInput> criteria, string reason) =>
        criteria
            .Select(c => new GradingCriterionResult(
                c.RubricCriterionId,
                c.MaxScore,
                Math.Round(c.MaxScore * 0.8m, 2),
                Deductions: null,
                Evidence: reason,
                Comment: "Automatically generated stub score.",
                Confidence: 0.5m))
            .ToList();
}
