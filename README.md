# Parser MCP Document Tools

[![Product Page](https://img.shields.io/badge/Product%20Page-2865E0?style=for-the-badge&logo=appveyor&logoColor=white)](https://github.com/groupdocs-parser/GroupDocs.Parser-Docs)
[![Docs](https://img.shields.io/badge/Docs-2865E0?style=for-the-badge&logo=Hugo&logoColor=white)](https://docs.groupdocs.com/parser/net/)
[![Blog](https://img.shields.io/badge/Blog-2865E0?style=for-the-badge&logo=WordPress&logoColor=white)](https://blog.groupdocs.com/categories/groupdocs.parser-product-family/)
[![Free Support](https://img.shields.io/badge/Free%20Support-2865E0?style=for-the-badge&logo=Discourse&logoColor=white)](https://forum.groupdocs.com/c/parser/17)
[![Temporary License](https://img.shields.io/badge/Temporary%20License-2865E0?style=for-the-badge&logo=rocket&logoColor=white)](https://purchase.groupdocs.com/temp-license/100308)

## Overview

`parser-mcp-document-tools-dotnet` is a runnable .NET 8 console host that
registers GroupDocs.Parser.Mcp for Docker-based MCP clients and exercises the
same document tools the server exposes (`get_document_info`, `extract_text`,
and `extract_tables` as Markdown) through GroupDocs.Parser 26.10.0 library
APIs. The sample writes an `mcpServers` JSON snippet that mounts a host
documents folder at `/data`, then verifies tool-equivalent calls locally so
you can inspect outputs without a live MCP handshake. Built for platform
engineers who need parse, text, and table results from DOCX and XLSX files
inside MCP client workflows.

## Technology Stack

- **Platform**: .NET 8
- **Product**: GroupDocs.Parser 26.10.0
- **Language**: C#
- **MCP server image**: `ghcr.io/groupdocs-parser/parser-net-mcp` (Docker-only;
  pin a version tag such as `:26.9.0` in shared configs)
- **Sample inputs**: `documents/sample.docx`, `documents/sample.xlsx`,
  `documents/sample.pdf`

## Problem Statement

MCP clients expect a small, named tool surface - document info, plain text,
tables as Markdown - while the parsing engine behind those tools is large
enough that GroupDocs.Parser.Mcp ships only as a container image, not as a
NuGet or `dnx` package. That split creates two jobs for a .NET team: register
the Docker MCP server with a volume mount into `/data`, and prove that the
underlying GroupDocs.Parser APIs return the same facts the tools would return
in production.

I built this when I needed a config snippet I could paste into an MCP client
and a local console run that wrote JSON, text, and Markdown under `Result/`
before anyone wired a remote handshake. Without that dual path, registration
errors and empty extracts only show up after the container is already in the
MCP client tool list.

## Solution Overview

GroupDocs.Parser for .NET addresses these challenges by exposing
`Parser.GetDocumentInfo`, `Parser.GetText`, `GetFormattedText(Markdown)`, and
`GetTables` - the same capabilities GroupDocs.Parser.Mcp surfaces as MCP tools.
Key technical features include:

- **Docker MCP registration**: Writes `mcpServers.groupdocs_parser` with
  `docker run --rm -i -v <host>:/data` and lists seven tool names.
- **Document info mirror**: Serializes file type, page count, and size to JSON
  matching `get_document_info`.
- **Text extract mirror**: Streams `GetText` into UTF-8 output for
  `extract_text`.
- **Table Markdown mirror**: Prefers formatted Markdown, then pipe tables from
  `GetTables` for `extract_tables`.
- **Local verification**: Runs without a live MCP session so CI and demos stay
  offline-friendly.

## When should I use this demo?

Use this demo when you must register GroupDocs.Parser.Mcp under Docker and
confirm that local GroupDocs.Parser calls match `get_document_info`,
`extract_text`, and `extract_tables` before connecting an MCP client. Pin
`ghcr.io/groupdocs-parser/parser-net-mcp` tags, mount files at `/data`, and
review JSON, text, and Markdown under `Result/`. Skip it for pure chunking
pipelines with no MCP registration step.

## Prerequisites

Before running these examples, ensure you have:

- **Runtime** - .NET 8 SDK
- **Package** - GroupDocs.Parser 26.10.0 (referenced in `ParseViaMcp.csproj`)
- **Docker** - required only when you actually start GroupDocs.Parser.Mcp; the
  local library demo does not need a running container
- **License** - optional `.lic` path via `REPLACE_WITH_YOUR_LICENSE_PATH`
  ([temporary license](https://purchase.groupdocs.com/temp-license/100308))

## Getting Started

### Installation

**Using Package Manager:**

```bash
dotnet add package GroupDocs.Parser --version 26.10.0
```

**Manual Installation:**

Open `ParseViaMcp.csproj` and keep the `PackageReference` for
`GroupDocs.Parser` Version `26.10.0`. Restore with `dotnet restore`.

### Configuration

1. Place sample files under `documents/` (the repo already includes DOCX, XLSX,
   and PDF samples).
2. Optionally set `REPLACE_WITH_YOUR_LICENSE_PATH` to your `.lic` file.
3. Run `dotnet run` - the app writes `Result/mcp-server.example.json` plus
   info, text, and table outputs. Point your MCP client at the Docker image
   using that JSON when you are ready for a live server.

## Repository Structure

```
parser-mcp-document-tools-dotnet/
│
├── Program.cs
├── ParseViaMcp.csproj
├── .gitignore
├── documents/
│   ├── sample.docx
│   ├── sample.xlsx
│   └── sample.pdf
└── Result/
    ├── mcp-server.example.json
    ├── document-info.json
    ├── extract-text.txt
    └── extract-tables.md
```

### File Descriptions

- **Program.cs** - Console entry point and four MCP-mirror methods
- **ParseViaMcp.csproj** - .NET 8 project with GroupDocs.Parser 26.10.0
- **.gitignore** - Ignores `bin/`, `obj/`, and generated `Result/`
- **documents/sample.docx** - Word sample for info and text tools
- **documents/sample.xlsx** - Spreadsheet sample for Markdown tables
- **documents/sample.pdf** - Extra PDF sample for format coverage
- **Result/** - Generated MCP config and tool-equivalent outputs

## Code Implementation

### Implementation: Writes an MCP server registration snippet for GroupDocs.Parser.Mcp (Docker)

GroupDocs.Parser.Mcp ships only as `ghcr.io/groupdocs-parser/parser-net-mcp`
because the engine exceeds NuGet size limits. This method builds an
`mcpServers` entry that runs Docker interactively and mounts the host documents
directory at `/data`, then lists the tool names the image advertises. Pin a
concrete tag such as `:26.9.0` instead of `:latest` in shared environments.

```csharp
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
```

Continue with the tool list, pin-tag note, and UTF-8 write:

```csharp
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
```

#### Technical Details

The registration JSON is the contract MCP clients read; the volume mount path
must match where you place files the tools will open under `/data`.

**Key Components:**
- `EnsureMcpServerConfig`: Builds and writes the MCP registration JSON
- `McpImage`: Container image constant for GroupDocs.Parser.Mcp
- `JsonSerializer`: Indented UTF-8 JSON for human review

**Parameters:**
- `hostDocumentsPath`: Absolute host folder mounted at `/data`
- `outputPath`: Destination for `mcp-server.example.json`

**Output:** UTF-8 JSON with `mcpServers`, `tools`, and pin-tag notes

---

### Implementation: Returns document type, page count and size - same facts as MCP tool get_document_info

The Docker MCP server exposes `get_document_info` to MCP clients. This method
calls `Parser.GetDocumentInfo` locally and writes JSON so the demo stays
verifiable without a live handshake. On the included `sample.docx` run, the
result reports a Microsoft Word Open XML type, 2 pages, and 15214 bytes.

```csharp
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
```

#### Technical Details

Null-conditional reads keep evaluation or unsupported formats from throwing
when info fields are absent.

**Key Components:**
- `Parser`: Opens the source document
- `GetDocumentInfo`: Returns type, pages, and size
- `JsonSerializer`: Writes the MCP-shaped payload

**Parameters:**
- `sourcePath`: Input document path
- `outputPath`: JSON output path

**Output:** `document-info.json` with fileName, fileType, pageCount, sizeBytes

---

### Implementation: Extracts plain text from a document - same role as MCP tool extract_text

This path mirrors the MCP `extract_text` tool using `Parser.GetText`. When the
reader is null, the method writes an empty UTF-8 file and returns 0 so callers
can treat missing text as a measurable failure instead of a crash.

```csharp
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
```

#### Technical Details

Returning the character count gives the console and CI a quick success signal
tied to the same artifact MCP clients would receive from `extract_text`.

**Key Components:**
- `GetText`: Opens a `TextReader` over document text
- `ReadToEnd`: Materializes the extract for disk output

**Parameters:**
- `sourcePath`: Input document path
- `outputPath`: Plain-text output path

**Output:** `extract-text.txt` and the written character count

---

### Implementation: Extracts tables and writes them as Markdown - same role as MCP tool extract_tables

The MCP `extract_tables` tool returns Markdown by default for chat rendering.
This demo prefers `GetFormattedText` with `FormattedTextMode.Markdown` when
available; otherwise it flattens `GetTables` cells into pipe tables. Prefer the
formatted path first - it already matches chat-friendly Markdown.

```csharp
using var parser = new Parser(sourcePath);

using (TextReader? formatted = parser.GetFormattedText(
           new FormattedTextOptions(FormattedTextMode.Markdown)))
{
    if (formatted != null)
    {
        string markdown = formatted.ReadToEnd();
        File.WriteAllText(outputPath, markdown, Encoding.UTF8);
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
```

If formatted Markdown is unavailable, fall back to feature detection and
`GetTables`:

```csharp
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
    File.WriteAllText(outputPath, "_No tables returned._\n", Encoding.UTF8);
    return 0;
}
```

Build pipe tables cell by cell when structural tables are present:

```csharp
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
```

Finish each row with separators, then write the Markdown file:

```csharp
        sb.Append("| ");
        sb.Append(string.Join(" | ", cells));
        sb.AppendLine(" |");
        if (row == 0)
        {
            sb.Append("| ");
            sb.Append(string.Join(
                " | ", Enumerable.Repeat("---", table.ColumnCount)));
            sb.AppendLine(" |");
        }
        totalRows++;
    }
    sb.AppendLine();
}
File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
return totalRows;
```

#### Technical Details

Escaping `|` inside cells keeps Markdown pipe tables valid when cell text
contains the delimiter. The sample XLSX run produces Sheet1 and Sheet2 tables
with header separators.

**Key Components:**
- `GetFormattedText` / `FormattedTextMode.Markdown`: Preferred Markdown path
- `Features.Tables` / `GetTables`: Structural table fallback
- `PageTableArea`: Row/column cell access for pipe output

**Parameters:**
- `sourcePath`: Prefer XLSX when present; otherwise DOCX
- `outputPath`: Markdown output path

**Output:** `extract-tables.md` and a row-count progress signal

## Best Practices

When implementing Parser MCP document tools, consider these best practices:

- **Pin image tags**: Replace `:latest` with a version such as `:26.9.0` in
  shared MCP configs
- **Mount `/data` deliberately**: Host paths in the volume flag must match the
  files your tools open inside the container
- **Verify offline first**: Run the library mirrors and inspect `Result/` before
  registering the server with an MCP client
- **Dispose parsers**: Keep `using var parser` so file handles release promptly
- **Prefer Markdown tables for chat**: Match `extract_tables` defaults so MCP
  client UIs render pipe tables without extra conversion
- **License for production**: Apply a temporary or purchased license to remove
  evaluation banners from extracts

## Additional Resources

For more in-depth information about parse documents via MCP with
GroupDocs.Parser for .NET, explore these technical resources:

* **[Step-by-step use case guide in the documentation](https://docs.groupdocs.com/parser/net/use-cases/parse-documents-via-mcp/)** – architecture notes for MCP registration and tool-equivalent Parser APIs: [Read the article →](https://docs.groupdocs.com/parser/net/use-cases/parse-documents-via-mcp/)

* **[In-depth blog article about this project](https://blog.groupdocs.com/parser/parse-documents-via-mcp-net/)** – business context for platform teams wiring document tools into MCP clients: [Read the article →](https://blog.groupdocs.com/parser/parse-documents-via-mcp-net/)

* **[GroupDocs.Parser MCP documentation](https://docs.groupdocs.com/parser/net/mcp/)** – Docker-only server setup and tool list: [Read the article →](https://docs.groupdocs.com/parser/net/mcp/)

* **[MCP troubleshooting for GroupDocs.Parser](https://docs.groupdocs.com/parser/net/mcp/troubleshooting/)** – common Docker and client registration issues: [Read the article →](https://docs.groupdocs.com/parser/net/mcp/troubleshooting/)

* **[GroupDocs.Parser.Mcp source repository](https://github.com/groupdocs-parser/GroupDocs.Parser.Mcp)** – container project sources for the MCP server image

* **[Extract tables from PDF to JSON via MCP](https://blog.groupdocs.com/parser/extract-tables-pdf-to-json-ai-agent-mcp/)** – Related blog on table extraction through the Docker MCP server

* **[Three ways to extract document data via MCP](https://blog.groupdocs.com/parser/3-ways-extract-document-data-ai-agents-mcp/)** – Related blog on text, tables, and images through MCP tools

## Keywords

`GroupDocs.Parser`, `GroupDocs.Parser.Mcp`, `MCP server`, `Docker`,
`extract_text`, `extract_tables`, `get_document_info`, `.NET`, `C#`,
`document parsing`, `Markdown tables`, `mcpServers`, `parser-net-mcp`,
`GetDocumentInfo`, `GetFormattedText`, `NuGet size limits`, `volume mount`,
`/data`, `DOCX`, `XLSX`, `document tools for assistants`

## Support

For technical support, visit:
- [Free Support Forum](https://forum.groupdocs.com/c/parser/17)
- [Product Documentation](https://docs.groupdocs.com/parser/net/)
- [Get Temporary License](https://purchase.groupdocs.com/temp-license/100308)
