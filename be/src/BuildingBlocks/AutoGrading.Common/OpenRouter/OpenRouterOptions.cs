namespace AutoGrading.Common.OpenRouter;

public class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "deepseek/deepseek-chat";
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
}
