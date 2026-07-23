namespace AutoGrading.Catalog.Api.Interfaces;

/// <summary>Thrown for a caller-input validation failure (maps to HTTP 400 with a <c>{ code, message }</c> body).</summary>
public sealed class CatalogValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Thrown when a referenced resource does not exist (maps to HTTP 404, with or without a body depending on the original endpoint's behavior).</summary>
public sealed class CatalogNotFoundException(string? code, string? message) : Exception(message)
{
    public string? Code { get; } = code;
}

/// <summary>Thrown for a write conflict — unique/FK constraint violation, optimistic-concurrency mismatch, or a business-rule
/// conflict such as "class already has enrollments" (maps to HTTP 409 with a <c>{ code, message }</c> body).</summary>
public sealed class CatalogConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Thrown when the caller is authenticated but not the owning lecturer/admin for a <see cref="Domain.Rubric"/> (maps to HTTP 403).</summary>
public sealed class RubricForbiddenException() : Exception("Requester is not authorized for this rubric.");

/// <summary>Thrown when a class was saved but publishing its <c>ClassLecturerAssigned</c> event failed — the save was rolled back
/// (maps to HTTP 503, matching the original endpoint's <c>Results.Problem(..., StatusCodes.Status503ServiceUnavailable)</c>).</summary>
public sealed class ClassEventPublishException(string message) : Exception(message);
