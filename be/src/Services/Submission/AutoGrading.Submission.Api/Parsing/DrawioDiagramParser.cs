using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AutoGrading.SubmissionSvc.Api.Interfaces;

namespace AutoGrading.SubmissionSvc.Api.Parsing;

/// <summary>
/// Extracts labeled components and connectors from a .drawio (mxGraph) diagram, per
/// docs/submission-template-guidelines.md. Handles both plain-XML and the default
/// base64+deflate "compressed" &lt;diagram&gt; payload that the draw.io app produces.
/// </summary>
public sealed class DrawioDiagramParser
{
    public async Task<ParsedArtifact> ParseAsync(Stream stream, string objectKey, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(cancellationToken);

        XDocument document;
        try
        {
            document = XDocument.Parse(raw);
        }
        catch (XmlException ex)
        {
            return new ParsedArtifact(null, [$"The diagram file is not valid XML: {ex.Message}"]);
        }

        var warnings = new List<string>();
        var graphModel = ResolveGraphModel(document, warnings);
        if (graphModel is null)
        {
            warnings.Add("No mxGraphModel content was found in the diagram.");
            return new ParsedArtifact(null, warnings.ToArray());
        }

        var cells = graphModel.Descendants("mxCell").ToList();
        var labelsById = new Dictionary<string, string>();
        foreach (var cell in cells)
        {
            var id = (string?)cell.Attribute("id");
            var value = (string?)cell.Attribute("value");
            if (id is null || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            labelsById[id] = WebUtility.HtmlDecode(value).Trim();
        }

        var components = cells
            .Where(c => (string?)c.Attribute("vertex") == "1")
            .Select(c => (string?)c.Attribute("id"))
            .Where(id => id is not null && labelsById.ContainsKey(id))
            .Select(id => labelsById[id!])
            .Distinct()
            .ToList();

        var connectors = new List<string>();
        foreach (var edge in cells.Where(c => (string?)c.Attribute("edge") == "1"))
        {
            var sourceId = (string?)edge.Attribute("source");
            var targetId = (string?)edge.Attribute("target");
            if (sourceId is null || targetId is null
                || !labelsById.TryGetValue(sourceId, out var sourceLabel)
                || !labelsById.TryGetValue(targetId, out var targetLabel))
            {
                continue;
            }

            var edgeId = (string?)edge.Attribute("id");
            var edgeLabel = edgeId is not null ? labelsById.GetValueOrDefault(edgeId) : null;
            connectors.Add(edgeLabel is null ? $"{sourceLabel} -> {targetLabel}" : $"{sourceLabel} -> {targetLabel} ({edgeLabel})");
        }

        if (components.Count == 0 && connectors.Count == 0)
        {
            warnings.Add("No labeled components or connectors were found in the diagram.");
            return new ParsedArtifact(null, warnings.ToArray());
        }

        var content = new StringBuilder();
        content.AppendLine("## Components").AppendLine(string.Join('\n', components));
        content.AppendLine().AppendLine("## Connectors").AppendLine(string.Join('\n', connectors));

        return new ParsedArtifact(content.ToString().Trim(), warnings.ToArray());
    }

    private static XElement? ResolveGraphModel(XDocument document, List<string> warnings)
    {
        var direct = document.Descendants("mxGraphModel").FirstOrDefault();
        if (direct is not null)
        {
            return direct;
        }

        var encoded = document.Descendants("diagram").FirstOrDefault()?.Value?.Trim();
        if (string.IsNullOrEmpty(encoded))
        {
            return null;
        }

        try
        {
            return XElement.Parse(DecodeCompressedDiagram(encoded));
        }
        catch (Exception ex) when (ex is FormatException or XmlException or InvalidDataException)
        {
            warnings.Add($"Failed to decode compressed diagram content: {ex.Message}");
            return null;
        }
    }

    private static string DecodeCompressedDiagram(string encoded)
    {
        var compressedBytes = Convert.FromBase64String(encoded);
        using var compressedStream = new MemoryStream(compressedBytes);
        using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();
        deflateStream.CopyTo(resultStream);

        return Uri.UnescapeDataString(Encoding.UTF8.GetString(resultStream.ToArray()));
    }
}
