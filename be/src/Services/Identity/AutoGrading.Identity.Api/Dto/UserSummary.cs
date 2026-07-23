using AutoGrading.Identity.Api.Interfaces;

namespace AutoGrading.Identity.Api.Dto;

public sealed record UserSummary(Guid Id, string Email, string FullName, string Role, string? StudentCode, Guid? ClassId, string? ClassName)
{
    public static UserSummary FromData(UserSummaryData data) =>
        new(data.Id, data.Email, data.FullName, data.Role, data.StudentCode, data.ClassId, data.ClassName);
}
