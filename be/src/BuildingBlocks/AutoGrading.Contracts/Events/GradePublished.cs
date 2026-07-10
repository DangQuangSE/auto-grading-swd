namespace AutoGrading.Contracts.Events;

/// <summary>Published by Grading Service when a lecturer publishes the final grade for a submission.</summary>
public sealed record GradePublished(Guid SubmissionId, Guid FinalGradeId, decimal FinalScore, Guid PublishedByUserId) : IntegrationEvent;
