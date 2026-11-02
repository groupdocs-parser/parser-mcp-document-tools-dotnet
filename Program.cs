// Topic: Prepare an MCP server config for GroupDocs.Parser.Mcp and exercise
// the same document tools (info, text, tables-as-Markdown) the server exposes.
// Uses GroupDocs.Parser for .NET locally so the demo is runnable without Docker;
// the written mcp config points agents at ghcr.io/groupdocs-parser/parser-net-mcp.

using System.Text;
using System.Text.Json;
using GroupDocs.Parser;
using GroupDocs.Parser.Data;
using GroupDocs.Parser.Options;

namespace Demo.ParseViaMcp;

internal static class Program
{
    private const string DocsFolder = "documents";
    private const string ResultFolder = "Result";
    private const string McpImage =
        "ghcr.io/groupdocs-parser/parser-net-mcp:latest";

    private static readonly string SourceDocx =
        Path.Combine(DocsFolder, "sample.docx");
    private static readonly string SourceXlsx =
        Path.Combine(DocsFolder, "sample.xlsx");

    private static int Main()
    {
        Directory.CreateDirectory(DocsFolder);
        Directory.CreateDirectory(ResultFolder);
        ApplyLicense();

        if (!File.Exists(SourceDocx))
        {
            Console.Error.WriteLine(
                $"Missing source document: {Path.GetFullPath(SourceDocx)}");
            return 1;
        }

        string configPath = Path.Combine(
            ResultFolder, "mcp-server.example.json");
        EnsureMcpServerConfig(Path.GetFullPath(DocsFolder), configPath);
        Console.WriteLine($"MCP config: {Path.GetFullPath(configPath)}");

        string infoPath = Path.Combine(ResultFolder, "document-info.json");
        GetDocumentInfoViaMcp(SourceDocx, infoPath);
        Console.WriteLine($"Document info: {Path.GetFullPath(infoPath)}");

        string textPath = Path.Combine(ResultFolder, "extract-text.txt");
        int textChars = ExtractTextViaMcp(SourceDocx, textPath);
        Console.WriteLine(
            $"Extracted text chars: {textChars} -> "
            + $"{Path.GetFullPath(textPath)}");

        string tablesPath = Path.Combine(ResultFolder, "extract-tables.md");
        string tablesSource =
            File.Exists(SourceXlsx) ? SourceXlsx : SourceDocx;
        int tableRows = ExtractTablesAsMarkdownViaMcp(
            tablesSource, tablesPath);
        Console.WriteLine(
            $"Table rows written: {tableRows} -> "
            + $"{Path.GetFullPath(tablesPath)}");

        return textChars > 0 ? 0 : 2;
    }

    private static void ApplyLicense()
    {
        // Point this at your .lic file to remove evaluation limits.
        // Temporary licence: https://purchase.groupdocs.com/temp-license/100308
        const string licensePath = "REPLACE_WITH_YOUR_LICENSE_PATH";
        if (File.Exists(licensePath))
        {
            new License().SetLicense(licensePath);
            Console.WriteLine("[license] applied");
        }
        else
        {
            Console.WriteLine(
                "[license] no licence set - running in evaluation mode");
        }
    }

    /// <summary>
    /// Writes an MCP server registration snippet for GroupDocs.Parser.Mcp
    /// (Docker).
    /// </summary>
    /// <remarks>
    /// GroupDocs.Parser.Mcp ships only as a container image
    /// (<c>ghcr.io/groupdocs-parser/parser-net-mcp</c>) because the engine
    /// exceeds NuGet size limits. The JSON mirrors an MCP client
    /// <c>mcpServers</c> entry that mounts a host documents folder at
    /// <c>/data</c>.
    /// </remarks>
    private static void EnsureMcpServerConfig(
        string hostDocumentsPath, string outputPath)
    {
        // JSON for MCP clients: docker run --rm -i -v <host>:/data <image>
        string normalizedHost = hostDocumentsPath.Replace('\\', '/');
        var payload = new
        {
            mcpServers = new
            {
                groupdocs_parser = new
                {
                    command = "docker",
                    args = new[]
                    {
                        "run", "--rm", "-i",
                        "-v", $"{normalizedHost}:/data",
                        McpImage
                    }
                }
            },
            tools = new[]
            {
                "extract_text",
                "extract_tables",
                "extract_metadata",
                "extract_images",
                "extract_barcodes",
                "get_document_info",
                "get_license_status"
            },
            notes = "Pin the image tag in shared configs "
                + "(for example :26.9.0) instead of :latest."
        };

        var json = JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    /// <summary>
    /// Returns document type, page count and size - same facts as MCP tool
    /// get_document_info.
    /// </summary>
    /// <remarks>
    /// The Docker MCP server exposes <c>get_document_info</c> to agents. This
    /// method calls the underlying GroupDocs.Parser API locally and writes
    /// JSON to <paramref name="outputPath"/> so the demo stays verifiable
    /// without a live MCP client.
    /// </remarks>
    private static void GetDocumentInfoViaMcp(
        string sourcePath, string outputPath)
    {
        using var parser = new Parser(sourcePath);
        var info = parser.GetDocumentInfo();

        var payload = new
        {
            fileName = Path.GetFileName(sourcePath),
            fileType = info?.FileType?.ToString(),
            pageCount = info?.PageCount,
            sizeBytes = info?.Size
        };

        File.WriteAllText(
            outputPath,
            JsonSerializer.Serialize(
                payload,
                new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);
    }

    /// <summary>
    /// Extracts plain text from a document - same role as MCP tool
    /// extract_text.
    /// </summary>
    /// <remarks>
    /// Mirrors the MCP <c>extract_text</c> tool using <c>Parser.GetText</c>.
    /// Returns the number of characters written.
    /// </remarks>
    private static int ExtractTextViaMcp(string sourcePath, string outputPath)
    {
        using var parser = new Parser(sourcePath);
        using TextReader? reader = parser.GetText();
        if (reader == null)
        {
            File.WriteAllText(outputPath, string.Empty, Encoding.UTF8);
            return 0;
        }

        string text = reader.ReadToEnd();
        File.WriteAllText(outputPath, text, Encoding.UTF8);
        return text.Length;
    }

    /// <summary>
    /// Extracts tables and writes them as Markdown - same role as MCP tool
    /// extract_tables.
    /// </summary>
    /// <remarks>
    /// The MCP <c>extract_tables</c> tool returns Markdown by default for chat
    /// rendering. This demo prefers Markdown from
    /// <c>GetFormattedText(Markdown)</c> when available; otherwise it flattens
    /// <c>GetTables()</c> cells into pipe tables.
    /// </remarks>
    private static int ExtractTablesAsMarkdownViaMcp(
        string sourcePath, string outputPath)
    {
        using var parser = new Parser(sourcePath);

        using (TextReader? formatted = parser.GetFormattedText(
                   new FormattedTextOptions(FormattedTextMode.Markdown)))
        {
            if (formatted != null)
            {
                string markdown = formatted.ReadToEnd();
                File.WriteAllText(outputPath, markdown, Encoding.UTF8);
                // Count pipe-table rows as a rough progress signal.
                int rows = 0;
                using var sr = new StringReader(markdown);
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains('|', StringComparison.Ordinal))
                        rows++;
                }

                return rows > 0
                    ? rows
                    : (string.IsNullOrWhiteSpace(markdown) ? 0 : 1);
            }
        }

        if (!parser.Features.Tables)
        {
            File.WriteAllText(
                outputPath,
                "_Tables extraction is not supported for this file._\n",
                Encoding.UTF8);
            return 0;
        }

        IEnumerable<PageTableArea>? tables = parser.GetTables();
        if (tables == null)
        {
            File.WriteAllText(
                outputPath, "_No tables returned._\n", Encoding.UTF8);
            return 0;
        }

        var sb = new StringBuilder();
        int totalRows = 0;
        int tableIndex = 0;
        foreach (PageTableArea table in tables)
        {
            tableIndex++;
            sb.AppendLine($"## Table {tableIndex}");
            sb.AppendLine();

            for (int row = 0; row < table.RowCount; row++)
            {
                var cells = new List<string>();
                for (int col = 0; col < table.ColumnCount; col++)
                {
                    PageTableAreaCell? cell = table[row, col];
                    string text = (cell?.Text ?? string.Empty)
                        .Replace("|", "\\|").Trim();
                    cells.Add(text);
                }

                sb.Append("| ");
                sb.Append(string.Join(" | ", cells));
                sb.AppendLine(" |");

                if (row == 0)
                {
                    sb.Append("| ");
                    sb.Append(string.Join(
                        " | ",
                        Enumerable.Repeat("---", table.ColumnCount)));
                    sb.AppendLine(" |");
                }

                totalRows++;
            }

            sb.AppendLine();
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        return totalRows;
    }
}
