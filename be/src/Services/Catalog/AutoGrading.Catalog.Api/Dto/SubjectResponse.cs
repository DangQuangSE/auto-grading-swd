using AutoGrading.Catalog.Api.Domain;

namespace AutoGrading.Catalog.Api.Dto;

public sealed record SubjectResponse(
    Guid Id,
    string Code,
    string Name,
    RegistrationStatus RegistrationStatus,
    DateTimeOffset CreatedAt)
{
    public static SubjectResponse FromDomain(Subject subject) => new(
        subject.Id,
        subject.Code,
        subject.Name,
        subject.RegistrationStatus,
        subject.CreatedAt);
}

public sealed record CreateSubjectRequest(string? Code, string? Name);

public sealed record UpdateSubjectRegistrationRequest(RegistrationStatus Status);
