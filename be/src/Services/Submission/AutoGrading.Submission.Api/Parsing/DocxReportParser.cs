using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AutoGrading.SubmissionSvc.Api.Parsing;

/// <summary>
/// Extracts report text grouped by heading, per docs/submission-template-guidelines.md: paragraphs
/// using a "HeadingN" style start a new section, all other paragraphs are appended to the current
/// (or an "Untitled" leading) section. Text inside images is intentionally not extracted.
/// </summary>
public sealed class DocxReportParser
{
    public Task<ParsedArtifact> ParseAsync(Stream stream, string objectKey, CancellationToken cancellationToken = default)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document.Body;
        if (body is null)
        {
            return Task.FromResult(new ParsedArtifact(null, ["The report document has no readable body content."]));
        }

        var sections = new List<(string Heading, StringBuilder Text)>();

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text)).Trim();
            if (text.Length == 0)
            {
                continue;
            }

            if (IsHeading(paragraph))
            {
                sections.Add((text, new StringBuilder()));
                continue;
            }

            if (sections.Count == 0)
            {
                sections.Add(("Untitled", new StringBuilder()));
            }

            sections[^1].Text.AppendLine(text);
        }

        if (sections.Count == 0)
        {
            return Task.FromResult(new ParsedArtifact(null, ["No text content was found in the report document."]));
        }

        var content = new StringBuilder();
        foreach (var (heading, text) in sections)
        {
            var sectionText = text.ToString().Trim();
            if (sectionText.Length == 0)
            {
                continue;
            }

            content.AppendLine($"## {heading}").AppendLine(sectionText).AppendLine();
        }

        return Task.FromResult(new ParsedArtifact(content.ToString().Trim(), []));
    }

    private static bool IsHeading(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        return styleId is not null && styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase);
    }
}
