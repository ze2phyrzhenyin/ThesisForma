namespace ThesisDocx.Core.Extraction;

public sealed class DocxExtractionOptions
{
    public string InputPath { get; set; } = string.Empty;
    public string? OutputJsonPath { get; set; }
    public string? PlainTextPath { get; set; }
    public string? MarkdownPath { get; set; }
    public string? ArtifactsDirectory { get; set; }
    public string? WorkspaceRoot { get; set; }
    public long MaxInputBytes { get; set; } = 100 * 1024 * 1024;
    public int MaxZipEntryCount { get; set; } = 8_000;
    public long MaxUncompressedBytes { get; set; } = 400 * 1024 * 1024;
    public double MaxCompressionRatio { get; set; } = 200;
}

public sealed class DocxExtractionException : Exception
{
    public DocxExtractionException(string code, string path, string message, string fixHint, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Path = path;
        FixHint = fixHint;
    }

    public string Code { get; }

    public string Severity { get; } = "error";

    public string Path { get; }

    public string FixHint { get; }
}

public sealed class DocxExtractionResult
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public string InputFileName { get; set; } = string.Empty;
    public ExtractedDocument Document { get; set; } = new();
    public string PlainText { get; set; } = string.Empty;
    public List<ExtractedBlock> Blocks { get; set; } = [];
    public List<ExtractedParagraph> Paragraphs { get; set; } = [];
    public List<ExtractedTable> Tables { get; set; } = [];
    public List<ExtractedFigure> Figures { get; set; } = [];
    public List<ExtractedFootnote> Footnotes { get; set; } = [];
    public List<ExtractedEndnote> Endnotes { get; set; } = [];
    public List<ExtractedBookmark> Bookmarks { get; set; } = [];
    public List<ExtractedHyperlink> Hyperlinks { get; set; } = [];
    public List<ExtractedField> Fields { get; set; } = [];
    public List<ExtractedStyleUsage> Styles { get; set; } = [];
    public List<ExtractedNumberingUsage> Numbering { get; set; } = [];
    public List<ExtractedSection> Sections { get; set; } = [];
    public List<ExtractionEvidence> PossibleHeadings { get; set; } = [];
    public List<ExtractionEvidence> PossibleAbstract { get; set; } = [];
    public List<ExtractionEvidence> PossibleKeywords { get; set; } = [];
    public List<ExtractionEvidence> PossibleBibliography { get; set; } = [];
    public List<ExtractionEvidence> PossibleCaptions { get; set; } = [];
    public List<ExtractionEvidence> PossibleAppendix { get; set; } = [];
    public List<ExtractionIssue> ExtractionIssues { get; set; } = [];
}

public sealed class ExtractedDocument
{
    public string Id { get; set; } = "document";
    public int ParagraphCount { get; set; }
    public int TableCount { get; set; }
    public int FigureCount { get; set; }
    public int FootnoteCount { get; set; }
    public int EndnoteCount { get; set; }
}

public sealed class ExtractedSection
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; }
    public string EvidencePath { get; set; } = string.Empty;
    public string PageSize { get; set; } = string.Empty;
    public string Margins { get; set; } = string.Empty;
    public string PageNumbering { get; set; } = string.Empty;
}

public sealed class ExtractedBlock
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Index { get; set; }
    public string EvidencePath { get; set; } = string.Empty;
}

public sealed class ExtractedParagraph
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? StyleId { get; set; }
    public string? StyleName { get; set; }
    public int? OutlineLevel { get; set; }
    public string? NumberingId { get; set; }
    public int? NumberingLevel { get; set; }
    public string? Alignment { get; set; }
    public string? Indent { get; set; }
    public string? Spacing { get; set; }
    public ExtractedRunSummary RunSummary { get; set; } = new();
    public string PossibleRole { get; set; } = "body";
    public List<ExtractedRun> Runs { get; set; } = [];
    public string EvidencePath { get; set; } = string.Empty;
}

public sealed class ExtractedRunSummary
{
    public int Count { get; set; }
    public bool HasBold { get; set; }
    public bool HasItalic { get; set; }
    public bool HasUnderline { get; set; }
    public bool HasSuperscript { get; set; }
    public bool HasSubscript { get; set; }
}

public sealed class ExtractedRun
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public string? Font { get; set; }
    public double? FontSizePt { get; set; }
    public string? EastAsiaFont { get; set; }
    public string? Color { get; set; }
    public bool Superscript { get; set; }
    public bool Subscript { get; set; }
}

public sealed class ExtractedTable
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; }
    public List<ExtractedTableRow> Rows { get; set; } = [];
    public string Text { get; set; } = string.Empty;
    public string? Borders { get; set; }
    public string EvidencePath { get; set; } = string.Empty;
}

public sealed class ExtractedTableRow
{
    public int Index { get; set; }
    public List<ExtractedTableCell> Cells { get; set; } = [];
}

public sealed class ExtractedTableCell
{
    public int RowIndex { get; set; }
    public int CellIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public int GridSpan { get; set; } = 1;
    public string? VerticalMerge { get; set; }
    public string? Borders { get; set; }
    public string EvidencePath { get; set; } = string.Empty;
}

public sealed class ExtractedFigure
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; }
    public string? RelationshipId { get; set; }
    public string? ContentType { get; set; }
    public string? ArtifactPath { get; set; }
    public string? SuggestedCaption { get; set; }
    public string NearbyText { get; set; } = string.Empty;
    public string EvidencePath { get; set; } = string.Empty;
}

public sealed class ExtractedFootnote
{
    public string Id { get; set; } = string.Empty;
    public string NoteId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string EvidencePath { get; set; } = string.Empty;
}

public sealed class ExtractedEndnote
{
    public string Id { get; set; } = string.Empty;
    public string NoteId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string EvidencePath { get; set; } = string.Empty;
}

public sealed class ExtractedBookmark
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EvidencePath { get; set; } = string.Empty;
}

public sealed class ExtractedHyperlink
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Uri { get; set; }
    public string EvidencePath { get; set; } = string.Empty;
}

public sealed class ExtractedField
{
    public string Id { get; set; } = string.Empty;
    public string FieldType { get; set; } = "other";
    public string Instruction { get; set; } = string.Empty;
    public string EvidencePath { get; set; } = string.Empty;
}

public sealed class ExtractedStyleUsage
{
    public string Id { get; set; } = string.Empty;
    public string StyleId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Type { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public sealed class ExtractedNumberingUsage
{
    public string Id { get; set; } = string.Empty;
    public string NumberingId { get; set; } = string.Empty;
    public List<int> Levels { get; set; } = [];
    public int UsageCount { get; set; }
}

public sealed class ExtractionEvidence
{
    public string Id { get; set; } = string.Empty;
    public string EvidencePath { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public sealed class ExtractionIssue
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string Message { get; set; } = string.Empty;
    public string EvidencePath { get; set; } = string.Empty;
}
