using AutoGrading.Catalog.Api.Domain;

namespace AutoGrading.Catalog.Api.Interfaces;

/// <summary>Defined here (not reused from <c>Endpoints/EnrollmentContracts.cs</c>) so <see cref="IEnrollmentRepository"/>/
/// <see cref="IEnrollmentService"/> never have to depend on the Endpoints namespace. The Endpoints-namespace copies stay in
/// place, unused by these interfaces, until Phase 4 deletes them and repoints <c>EnrollmentHttpResults</c> at these types.</summary>
public enum EnrollmentCommandStatus
{
    Success,
    Invalid,
    NotFound,
    Conflict
}

public sealed record EnrollmentCommandResult<T>(
    EnrollmentCommandStatus Status,
    string? Code,
    string? Message,
    T? Value,
    T? Current)
{
    public static EnrollmentCommandResult<T> Success(T? value) =>
        new(EnrollmentCommandStatus.Success, null, null, value, default);

    public static EnrollmentCommandResult<T> Invalid(string code, string message) =>
        new(EnrollmentCommandStatus.Invalid, code, message, default, default);

    public static EnrollmentCommandResult<T> NotFound(string code, string message) =>
        new(EnrollmentCommandStatus.NotFound, code, message, default, default);

    public static EnrollmentCommandResult<T> Conflict(string code, string message, T? current = default) =>
        new(EnrollmentCommandStatus.Conflict, code, message, default, current);
}

public sealed record EnrollmentSummary(
    Guid Id,
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    RegistrationStatus RegistrationStatus,
    Guid ClassId,
    string ClassName,
    string RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminEnrollmentSummary(
    Guid Id,
    Guid StudentId,
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    RegistrationStatus RegistrationStatus,
    Guid ClassId,
    string ClassName,
    string RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
