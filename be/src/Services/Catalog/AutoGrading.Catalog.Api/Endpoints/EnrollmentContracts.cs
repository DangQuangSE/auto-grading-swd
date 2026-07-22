using AutoGrading.Catalog.Api.Domain;

namespace AutoGrading.Catalog.Api.Endpoints;

public sealed record UpsertEnrollmentRequest(Guid ClassId, string? RowVersion);

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
    DateTimeOffset UpdatedAt)
{
    internal static EnrollmentSummary From(EnrollmentProjection item) => new(
        item.Id,
        item.SubjectId,
        item.SubjectCode,
        item.SubjectName,
        item.RegistrationStatus,
        item.ClassId,
        item.ClassName,
        Convert.ToBase64String(item.RowVersion),
        item.CreatedAt,
        item.UpdatedAt);
}

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
    DateTimeOffset UpdatedAt)
{
    internal static AdminEnrollmentSummary From(AdminEnrollmentProjection item) => new(
        item.Id,
        item.StudentId,
        item.SubjectId,
        item.SubjectCode,
        item.SubjectName,
        item.RegistrationStatus,
        item.ClassId,
        item.ClassName,
        Convert.ToBase64String(item.RowVersion),
        item.CreatedAt,
        item.UpdatedAt);
}

internal sealed record EnrollmentProjection(
    Guid Id,
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    RegistrationStatus RegistrationStatus,
    Guid ClassId,
    string ClassName,
    byte[] RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record AdminEnrollmentProjection(
    Guid Id,
    Guid StudentId,
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    RegistrationStatus RegistrationStatus,
    Guid ClassId,
    string ClassName,
    byte[] RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal enum EnrollmentCommandStatus
{
    Success,
    Invalid,
    NotFound,
    Conflict
}

internal sealed record EnrollmentCommandResult<T>(
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
