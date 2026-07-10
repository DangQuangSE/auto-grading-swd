namespace AutoGrading.Contracts.Events;

/// <summary>Published by Submission Service's extraction job once document/diagram content is parsed.</summary>
public sealed record ArtifactsExtracted(Guid SubmissionId, bool Success, string[] Warnings) : IntegrationEvent;
