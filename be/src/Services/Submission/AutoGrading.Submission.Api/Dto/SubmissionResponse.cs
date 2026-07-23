using AutoGrading.SubmissionSvc.Api.Domain;

namespace AutoGrading.SubmissionSvc.Api.Dto;

public sealed record SubmissionResponse(
    Guid Id,
    Guid AssignmentId,
    Guid StudentId,
    int AttemptNumber,
    string ReportObjectKey,
    string? DiagramObjectKey,
    SubmissionState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ExtractedArtifactResponse> Artifacts)
{
    public static SubmissionResponse FromDomain(Submission submission) => new(
        submission.Id,
        submission.AssignmentId,
        submission.StudentId,
        submission.AttemptNumber,
        submission.ReportObjectKey,
        submission.DiagramObjectKey,
        submission.State,
        submission.CreatedAt,
        submission.UpdatedAt,
        submission.Artifacts.Select(ExtractedArtifactResponse.FromDomain).ToList());
}
