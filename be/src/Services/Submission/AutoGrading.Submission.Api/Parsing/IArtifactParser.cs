namespace AutoGrading.SubmissionSvc.Api.Parsing;

public sealed record ParsedArtifact(string? Content, string[] Warnings);

/// <summary>
/// Parses report (.docx) and diagram (.drawio) submission files into structured content.
/// Stub implementation: real parsing (port of the Supabase docxParser/drawioParser Edge Functions)
/// is deferred to a later phase; this always returns an empty result with no warnings.
/// </summary>
public interface IArtifactParser
{
    Task<ParsedArtifact> ParseAsync(Stream stream, string objectKey, CancellationToken cancellationToken = default);
}

public sealed class StubArtifactParser : IArtifactParser
{
    public Task<ParsedArtifact> ParseAsync(Stream stream, string objectKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new ParsedArtifact(Content: null, Warnings: Array.Empty<string>()));
}
