using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AutoGrading.Identity.Api.RosterImport;

public sealed record RosterRow(int RowNumber, string Email, string? StudentCode, string ClassName);

public sealed record RosterFileParseResult(IReadOnlyList<RosterRow> Rows, string? Error);

/// <summary>Parses a bulk roster upload (.xlsx/.xls/.csv) into rows, mapping columns by header name
/// (Email, StudentCode, ClassName — case-insensitive) rather than fixed position.</summary>
public static class RosterFileParser
{
    private static readonly string[] RequiredColumns = ["Email", "StudentCode", "ClassName"];

    public static RosterFileParseResult Parse(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        List<string[]> rawRows;
        try
        {
            rawRows = extension switch
            {
                ".csv" => ParseCsv(stream),
                ".xlsx" or ".xls" => ParseExcel(stream),
                _ => throw new NotSupportedException(),
            };
        }
        catch (NotSupportedException)
        {
            return new RosterFileParseResult([], $"Unsupported file type '{extension}'; expected .xlsx, .xls, or .csv.");
        }

        if (rawRows.Count == 0)
        {
            return new RosterFileParseResult([], "File is empty.");
        }

        var header = rawRows[0];
        var columnIndex = RequiredColumns.ToDictionary(
            name => name,
            name => Array.FindIndex(header, cell => string.Equals(cell.Trim(), name, StringComparison.OrdinalIgnoreCase)));

        var missingColumn = RequiredColumns.FirstOrDefault(name => columnIndex[name] < 0);
        if (missingColumn is not null)
        {
            return new RosterFileParseResult([], $"Invalid file format: missing column {missingColumn}.");
        }

        var rows = new List<RosterRow>();
        for (var i = 1; i < rawRows.Count; i++)
        {
            var cells = rawRows[i];
            var email = Cell(cells, columnIndex["Email"]);
            var className = Cell(cells, columnIndex["ClassName"]);
            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(className))
            {
                continue;
            }

            var studentCode = Cell(cells, columnIndex["StudentCode"]);
            rows.Add(new RosterRow(i + 1, email, string.IsNullOrWhiteSpace(studentCode) ? null : studentCode, className));
        }

        return new RosterFileParseResult(rows, null);
    }

    private static string Cell(string[] cells, int index) => index < cells.Length ? cells[index].Trim() : string.Empty;

    private static List<string[]> ParseCsv(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var rows = new List<string[]>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            rows.Add(SplitCsvLine(line));
        }

        return rows;
    }

    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }

    private static List<string[]> ParseExcel(Stream stream)
    {
        using var document = SpreadsheetDocument.Open(stream, isEditable: false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Workbook has no WorkbookPart.");
        var sheet = workbookPart.Workbook.Descendants<Sheet>().FirstOrDefault()
            ?? throw new InvalidOperationException("Workbook has no sheets.");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

        return worksheetPart.Worksheet.Descendants<Row>()
            .Select(row => ParseExcelRow(row, sharedStrings))
            .ToList();
    }

    /// <summary>Maps each cell by its column letter (from CellReference, e.g. "C5" → column 2), not by its
    /// position within the row's child elements — OpenXml omits cells for blank values, so positional
    /// indexing would silently misalign columns whenever an earlier cell in the row is empty.</summary>
    private static string[] ParseExcelRow(Row row, SharedStringTable? sharedStrings)
    {
        var cells = row.Elements<Cell>().ToList();
        if (cells.Count == 0)
        {
            return [];
        }

        var maxColumn = cells.Max(c => GetColumnIndex(c.CellReference!.Value!));
        var result = new string[maxColumn + 1];
        Array.Fill(result, string.Empty);

        foreach (var cell in cells)
        {
            result[GetColumnIndex(cell.CellReference!.Value!)] = GetCellValue(cell, sharedStrings);
        }

        return result;
    }

    private static int GetColumnIndex(string cellReference)
    {
        var columnIndex = 0;
        foreach (var c in cellReference.TakeWhile(char.IsLetter))
        {
            columnIndex = (columnIndex * 26) + (c - 'A' + 1);
        }

        return columnIndex - 1;
    }

    private static string GetCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        var value = cell.CellValue?.InnerText ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString && sharedStrings is not null && int.TryParse(value, out var index))
        {
            return sharedStrings.ElementAt(index).InnerText;
        }

        return value;
    }
}
