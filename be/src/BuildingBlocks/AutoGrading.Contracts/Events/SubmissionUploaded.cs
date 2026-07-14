namespace AutoGrading.Contracts.Events;

/// <summary>Published by Submission Service after report file (and optionally diagram) are stored in MinIO.</summary>
public sealed record SubmissionUploaded(
    Guid SubmissionId,
    Guid AssignmentId,
    Guid StudentId,
    string ReportObjectKey,
    string? DiagramObjectKey
) : IntegrationEvent;
