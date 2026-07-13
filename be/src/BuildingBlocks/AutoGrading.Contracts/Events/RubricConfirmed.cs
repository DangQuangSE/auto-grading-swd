namespace AutoGrading.Contracts.Events;

public sealed record RubricConfirmedCriterion(Guid RubricCriterionId, string Name, string? Description, decimal MaxScore, int OrderIndex);

/// <summary>Published by Catalog Service when a lecturer/admin confirms a rubric's criteria, making them the version Grading reads for scoring.</summary>
public sealed record RubricConfirmed(
    Guid RubricId,
    Guid SubjectId,
    Guid? AssignmentId,
    string Scope,
    IReadOnlyList<RubricConfirmedCriterion> Criteria) : IntegrationEvent;
