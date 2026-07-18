namespace AutoGrading.Contracts.Events;

public sealed record SubmissionStatusChanged(
    Guid SubmissionId,
    Guid StudentId,
    string Status,
    string? ErrorMessage = null
) : IntegrationEvent;
