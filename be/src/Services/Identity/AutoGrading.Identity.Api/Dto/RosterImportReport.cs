using AutoGrading.Identity.Api.Interfaces;

namespace AutoGrading.Identity.Api.Dto;

public sealed record RosterImportReport(int TotalRows, int UpdatedCount, int SkippedCount, IReadOnlyList<RosterImportRowResult> Details)
{
    public static RosterImportReport FromData(RosterImportResult data) =>
        new(data.TotalRows, data.UpdatedCount, data.SkippedCount, data.Details.Select(RosterImportRowResult.FromData).ToList());
}
