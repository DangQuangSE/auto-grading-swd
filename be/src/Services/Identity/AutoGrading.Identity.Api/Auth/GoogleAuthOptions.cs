namespace AutoGrading.Identity.Api.Auth;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    public string ClientId { get; set; } = string.Empty;
}

public static class EducationEmailValidator
{
    /// <summary>
    /// Accepts academic domains like "school.edu" as well as country-specific
    /// variants like "fpt.edu.vn" or "unimelb.edu.au".
    /// </summary>
    public static bool IsEducationEmail(string email)
    {
        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1)
        {
            return false;
        }

        var domain = email[(atIndex + 1)..];
        return domain.EndsWith(".edu", StringComparison.OrdinalIgnoreCase)
            || domain.Contains(".edu.", StringComparison.OrdinalIgnoreCase);
    }
}
