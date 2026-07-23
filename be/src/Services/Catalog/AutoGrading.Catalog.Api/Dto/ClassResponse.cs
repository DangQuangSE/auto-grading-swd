using AutoGrading.Catalog.Api.Domain;

namespace AutoGrading.Catalog.Api.Dto;

public sealed record LegacyClassSummary(Guid Id, string Name, Guid? LecturerId);

public sealed record ClassSummary(Guid Id, string Name, Guid LecturerId, Guid? SubjectId, string? SubjectCode)
{
    public static ClassSummary FromDomain(Class item) =>
        new(item.Id, item.Name, item.LecturerId, item.SubjectId, item.Subject?.Code);
}

public sealed record RegistrationClassOption(Guid Id, string Name, Guid SubjectId);

public sealed record CreateLegacyClassRequest(string? Name, Guid LecturerId);

public sealed record CreateSubjectScopedClassRequest(string? Name, Guid LecturerId, Guid SubjectId);

public sealed record UpdateClassRequest(Guid? LecturerId, Guid? SubjectId);
