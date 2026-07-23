using AutoGrading.Identity.Api.Interfaces;

namespace AutoGrading.Identity.Api.Dto;

public sealed record RosterImportRowResult(int RowNumber, string Email, string Status, string? Reason)
{
    public static RosterImportRowResult FromData(RosterImportRowOutcome data) =>
        new(data.RowNumber, data.Email, data.Status, data.Reason);
}
