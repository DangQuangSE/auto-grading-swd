using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AutoGrading.Catalog.Api.Parsing;

/// <summary>
/// Parses the rubric table described in docs/rubric-docx-format.md: the first table whose
/// header row contains "Ma tieu chi", "Noi dung can cham", "Mo ta" and "Diem toi da" (accented
/// or unaccented Vietnamese) is treated as the rubric; one row becomes one criterion.
/// </summary>
public sealed class DocxRubricParser : IRubricParser
{
    private const string CodeHeader = "ma tieu chi";
    private const string TitleHeader = "noi dung can cham";
    private const string DescriptionHeader = "mo ta";
    private const string MaxScoreHeader = "diem toi da";
    private const string GuidanceHeader = "muc dat";
    private const string DeductionHeader = "loi tru diem";

    private static readonly string[] RequiredHeaders = [CodeHeader, TitleHeader, DescriptionHeader, MaxScoreHeader];

    public RubricParseResult Parse(Stream fileStream)
    {
        using var document = WordprocessingDocument.Open(fileStream, false);
        var body = document.MainDocumentPart?.Document.Body;
        if (body is null)
        {
            return new RubricParseResult(false, [], ["The document has no readable body content."]);
        }

        foreach (var table in body.Descendants<Table>())
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count < 2)
            {
                continue;
            }

            var columnMap = MapColumns(GetRowCellsText(rows[0]));
            if (columnMap is null)
            {
                continue;
            }

            return ParseCriteriaRows(rows.Skip(1), columnMap);
        }

        return new RubricParseResult(false, [], [
            $"No table found with the required headers: {string.Join(", ", RequiredHeaders)}.",
        ]);
    }

    private static Dictionary<string, int>? MapColumns(IReadOnlyList<string> headerCells)
    {
        var normalized = headerCells.Select(NormalizeHeader).ToList();
        if (!RequiredHeaders.All(normalized.Contains))
        {
            return null;
        }

        var map = new Dictionary<string, int>();
        for (var i = 0; i < normalized.Count; i++)
        {
            map.TryAdd(normalized[i], i);
        }

        return map;
    }

    private static RubricParseResult ParseCriteriaRows(IEnumerable<TableRow> rows, IReadOnlyDictionary<string, int> columnMap)
    {
        var criteria = new List<ParsedRubricCriterion>();
        var errors = new List<string>();
        var displayOrder = 0;

        foreach (var row in rows)
        {
            var cells = GetRowCellsText(row);
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var rowNumber = displayOrder + 2;
            var code = GetCell(cells, columnMap, CodeHeader);
            var title = GetCell(cells, columnMap, TitleHeader);
            var description = GetCell(cells, columnMap, DescriptionHeader);
            var maxScoreRaw = GetCell(cells, columnMap, MaxScoreHeader);

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title))
            {
                errors.Add($"Row {rowNumber}: '{CodeHeader}' and '{TitleHeader}' are required.");
                continue;
            }

            if (!decimal.TryParse(maxScoreRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var maxScore) || maxScore <= 0)
            {
                errors.Add($"Row {rowNumber}: '{MaxScoreHeader}' must be a positive number, got '{maxScoreRaw}'.");
                continue;
            }

            criteria.Add(new ParsedRubricCriterion(
                CriterionCode: code!.Trim(),
                Title: title!.Trim(),
                Description: description?.Trim(),
                MaxScore: maxScore,
                GradingGuidance: GetCell(cells, columnMap, GuidanceHeader)?.Trim(),
                DeductionNotes: GetCell(cells, columnMap, DeductionHeader)?.Trim(),
                DisplayOrder: displayOrder));

            displayOrder++;
        }

        if (criteria.Count == 0)
        {
            errors.Add("No valid criterion rows were found in the rubric table.");
            return new RubricParseResult(false, [], errors);
        }

        return new RubricParseResult(errors.Count == 0, criteria, errors);
    }

    private static string? GetCell(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> columnMap, string header)
    {
        if (!columnMap.TryGetValue(header, out var index) || index >= cells.Count)
        {
            return null;
        }

        var value = cells[index];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static List<string> GetRowCellsText(TableRow row) =>
        row.Elements<TableCell>()
            .Select(cell => string.Join(" ", cell.Descendants<Text>().Select(t => t.Text)).Trim())
            .ToList();

    private static string NormalizeHeader(string value)
    {
        var withoutDStroke = value.Replace('đ', 'd').Replace('Đ', 'D');
        var decomposed = withoutDStroke.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
    }
}
