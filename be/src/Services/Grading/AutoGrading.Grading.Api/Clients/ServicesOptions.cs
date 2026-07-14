namespace AutoGrading.Grading.Api.Clients;

public sealed class ServicesOptions
{
    public const string SectionName = "Services";

    public string CatalogApiBaseUrl { get; set; } = string.Empty;
    public string SubmissionApiBaseUrl { get; set; } = string.Empty;
}
