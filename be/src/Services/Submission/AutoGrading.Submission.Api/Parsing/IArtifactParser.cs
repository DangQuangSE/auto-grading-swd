using AutoGrading.SubmissionSvc.Api.Domain;

namespace AutoGrading.SubmissionSvc.Api.Parsing;

public sealed record ParsedArtifact(string? Content, string[] Warnings);

/// <summary>Parses report (.docx) and diagram (.drawio) submission files into structured content.</summary>
public interface IArtifactParser
{
    Task<ParsedArtifact> ParseAsync(ArtifactKind kind, Stream stream, string objectKey, CancellationToken cancellationToken = default);
}

/// <summary>Dispatches to the concrete parser for the given artifact kind.</summary>
public sealed class ArtifactParser(DocxReportParser reportParser, DrawioDiagramParser diagramParser) : IArtifactParser
{
    public Task<ParsedArtifact> ParseAsync(ArtifactKind kind, Stream stream, string objectKey, CancellationToken cancellationToken = default) =>
        kind switch
        {
            ArtifactKind.Report => reportParser.ParseAsync(stream, objectKey, cancellationToken),
            ArtifactKind.Diagram => diagramParser.ParseAsync(stream, objectKey, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported artifact kind."),
        };
}
