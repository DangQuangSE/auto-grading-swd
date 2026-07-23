namespace AutoGrading.Identity.Api.Dto;

public sealed class BulkImportForm
{
    public IFormFile File { get; set; } = null!;
}
