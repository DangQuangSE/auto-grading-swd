namespace AutoGrading.Contracts.Events;

/// <summary>Published by Catalog Service after a rubric document has been parsed into criteria.</summary>
public sealed record RubricParsed(Guid RubricId, Guid SubjectId, Guid? AssignmentId, int CriteriaCount) : IntegrationEvent;
