using AutoGrading.Catalog.Api.Domain;

namespace AutoGrading.Catalog.Api.Dto;

/// <summary>Deliberately omits <see cref="Assignment.Subject"/>/<see cref="Assignment.Rubrics"/> — the original
/// endpoint serialized the raw entity, which always emitted these as <c>null</c>/<c>[]</c> since no code path ever
/// includes them; dropping them here is a documented minor cleanup, not a functional contract change.</summary>
public sealed record AssignmentResponse(
    Guid Id,
    Guid SubjectId,
    string Title,
    string? Description,
    DateTimeOffset? DueDate,
    int MaxAttempts,
    DateTimeOffset CreatedAt)
{
    public static AssignmentResponse FromDomain(Assignment assignment) => new(
        assignment.Id,
        assignment.SubjectId,
        assignment.Title,
        assignment.Description,
        assignment.DueDate,
        assignment.MaxAttempts,
        assignment.CreatedAt);
}

public sealed record CreateAssignmentRequest(Guid SubjectId, string Title, string? Description, DateTimeOffset? DueDate, int MaxAttempts = 1);

public sealed record UpdateAssignmentRequest(string Title, string? Description, DateTimeOffset? DueDate, int MaxAttempts);
