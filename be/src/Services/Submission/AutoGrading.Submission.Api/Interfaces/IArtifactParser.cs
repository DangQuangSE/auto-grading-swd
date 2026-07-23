using AutoGrading.SubmissionSvc.Api.Domain;

namespace AutoGrading.SubmissionSvc.Api.Interfaces;

public sealed record ParsedArtifact(string? Content, string[] Warnings, string[]? ImageDataUrls = null);

/// <summary>Parses report (.docx) and diagram (.drawio) submission files into structured content.</summary>
public interface IArtifactParser
{
    Task<ParsedArtifact> ParseAsync(ArtifactKind kind, Stream stream, string objectKey, CancellationToken cancellationToken = default);
}
