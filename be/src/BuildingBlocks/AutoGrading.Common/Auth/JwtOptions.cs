namespace AutoGrading.Common.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AutoGrading.Identity";
    public string Audience { get; set; } = "AutoGrading";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}
