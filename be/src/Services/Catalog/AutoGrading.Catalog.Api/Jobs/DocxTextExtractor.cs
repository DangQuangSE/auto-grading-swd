using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;

namespace AutoGrading.Catalog.Api.Jobs;

/// <summary>Extracts plain text runs from a .docx file, in document order, for feeding to the AI rubric extraction prompt.</summary>
public static class DocxTextExtractor
{
    public static string ExtractText(Stream docxStream)
    {
        using var document = WordprocessingDocument.Open(docxStream, isEditable: false);
        var body = document.MainDocumentPart?.Document.Body;
        if (body is null)
        {
            return string.Empty;
        }

        return string.Join('\n', body.Descendants<Paragraph>().Select(p => p.InnerText));
    }
}
