using AutoGrading.SubmissionSvc.Api.Domain;

namespace AutoGrading.SubmissionSvc.Api.Dto;

public sealed record ExtractedArtifactResponse(
    Guid Id,
    Guid SubmissionId,
    ArtifactKind Kind,
    string? Content,
    string? Warnings,
    string? ImagesJson,
    DateTimeOffset CreatedAt)
{
    public static ExtractedArtifactResponse FromDomain(ExtractedArtifact artifact) => new(
        artifact.Id,
        artifact.SubmissionId,
        artifact.Kind,
        artifact.Content,
        artifact.Warnings,
        artifact.ImagesJson,
        artifact.CreatedAt);
}
