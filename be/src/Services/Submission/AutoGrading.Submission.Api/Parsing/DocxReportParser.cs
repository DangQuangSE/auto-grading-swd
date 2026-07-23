using System.Text;
using AutoGrading.SubmissionSvc.Api.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AutoGrading.SubmissionSvc.Api.Parsing;

/// <summary>
/// Extracts report text grouped by heading, per docs/submission-template-guidelines.md: paragraphs
/// using a "HeadingN" style start a new section, all other paragraphs are appended to the current
/// (or an "Untitled" leading) section. Embedded images are extracted separately as base64 data URLs
/// so a vision-capable model can grade diagrams/screenshots pasted directly into the report.
/// </summary>
public sealed class DocxReportParser
{
    private const int MaxImages = 8;
    private const int MaxImageBytes = 4 * 1024 * 1024;

    public async Task<ParsedArtifact> ParseAsync(Stream stream, string objectKey, CancellationToken cancellationToken = default)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document.Body;
        if (body is null)
        {
            return new ParsedArtifact(null, ["The report document has no readable body content."]);
        }

        var images = await ExtractImagesAsync(document, cancellationToken);
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

        if (sections.Count == 0 && images.DataUrls.Count == 0)
        {
            return new ParsedArtifact(null, ["No text content was found in the report document."]);
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

        var warnings = images.Skipped > 0
            ? new[] { $"{images.Skipped} embedded image(s) were skipped (too many or too large)." }
            : [];

        return new ParsedArtifact(content.ToString().Trim(), warnings, images.DataUrls.ToArray());
    }

    private static bool IsHeading(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        return styleId is not null && styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(List<string> DataUrls, int Skipped)> ExtractImagesAsync(
        WordprocessingDocument document, CancellationToken cancellationToken)
    {
        var dataUrls = new List<string>();
        var skipped = 0;

        var imageParts = document.MainDocumentPart?.ImageParts ?? [];
        foreach (var imagePart in imageParts)
        {
            if (dataUrls.Count >= MaxImages)
            {
                skipped++;
                continue;
            }

            await using var partStream = imagePart.GetStream();
            using var buffer = new MemoryStream();
            await partStream.CopyToAsync(buffer, cancellationToken);

            if (buffer.Length == 0 || buffer.Length > MaxImageBytes)
            {
                skipped++;
                continue;
            }

            var base64 = Convert.ToBase64String(buffer.ToArray());
            dataUrls.Add($"data:{imagePart.ContentType};base64,{base64}");
        }

        return (dataUrls, skipped);
    }
}
