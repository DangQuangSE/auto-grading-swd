using AutoGrading.Catalog.Api.Domain;

namespace AutoGrading.Catalog.Api.Dto;

/// <summary>Deliberately omits <see cref="Rubric.Subject"/>/<see cref="Rubric.Assignment"/> — the original endpoint
/// serialized the raw entity, which always emitted these as <c>null</c> since no code path ever includes them;
/// dropping them here is a documented minor cleanup, not a functional contract change. Also omits
/// <see cref="Rubric.RowVersion"/> — unlike Enrollment (which round-trips its RowVersion through
/// <c>UpsertEnrollmentRequest</c> for optimistic concurrency), no Rubric endpoint ever accepts a RowVersion back from
/// the client, so exposing this raw EF concurrency token would be a pure implementation-detail leak.
/// <see cref="Rubric.Criteria"/> is kept as-is (the domain type already serializes cleanly — its own back-reference
/// to <c>Rubric</c> is <c>[JsonIgnore]</c>'d — so a redundant wrapper DTO adds no value).</summary>
public sealed record RubricResponse(
    Guid Id,
    Guid SubjectId,
    Guid? AssignmentId,
    string Name,
    string? FileObjectKey,
    DateTimeOffset CreatedAt,
    RubricStatus Status,
    RubricScope Scope,
    Guid? LecturerId,
    List<RubricCriterion> Criteria)
{
    public static RubricResponse FromDomain(Rubric rubric) => new(
        rubric.Id,
        rubric.SubjectId,
        rubric.AssignmentId,
        rubric.Name,
        rubric.FileObjectKey,
        rubric.CreatedAt,
        rubric.Status,
        rubric.Scope,
        rubric.LecturerId,
        rubric.Criteria);
}

public sealed record UpdateCriterionRequest(string Name, string? Description, decimal MaxScore, int OrderIndex);
