namespace AutoGrading.Common.OpenCode;

public class OpenCodeOptions
{
    public const string SectionName = "OpenCode";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "mimo-v2.5-free";
    public string BaseUrl { get; set; } = "https://opencode.ai/zen/v1";

    /// <summary>Upper bound on completion tokens. Reasoning-capable models (e.g. MiMo-V2.5) emit
    /// reasoning tokens plus visible content; set generously — a 5-criterion grader uses ~2k tokens,
    /// a 15-criterion long report can exceed 8k.</summary>
    public int MaxCompletionTokens { get; set; } = 16000;

    /// <summary>If true, embedded report/diagram images are sent as image_url content parts so the
    /// model can see them. Disable when the chosen model isn't vision-capable.</summary>
    public bool EnableVision { get; set; } = true;
}
