namespace ThesisDocx.Core.Validation;

public sealed class DocxInspectionResult
{
    public string ReportVersion { get; set; } = "1.0.0";

    public List<string> Entries { get; set; } = [];

    public List<string> PackageParts { get; set; } = [];

    public List<string> Styles { get; set; } = [];

    public List<StyleSummary> StylesSummary { get; set; } = [];

    public List<string> NumberingLevelTexts { get; set; } = [];

    public List<NumberingSummary> NumberingSummary { get; set; } = [];

    public List<string> FieldCodes { get; set; } = [];

    public List<string> TocFields { get; set; } = [];

    public List<string> RefFields { get; set; } = [];

    public List<SectionSummary> SectionsSummary { get; set; } = [];

    public List<HeaderFooterSummary> HeadersFootersSummary { get; set; } = [];

    public List<string> SectionPageNumberFormats { get; set; } = [];

    public List<string> Bookmarks { get; set; } = [];

    public NoteInspectionSummary Footnotes { get; set; } = new();

    public NoteInspectionSummary Endnotes { get; set; } = new();

    public BibliographyInspectionSummary Bibliography { get; set; } = new();

    public EquationInspectionSummary Equations { get; set; } = new();

    public TableInspectionSummary Tables { get; set; } = new();

    public TemplateRenderingInspectionSummary TemplateRendering { get; set; } = new();

    public int OpenXmlValidatorErrorCount { get; set; }

    public int ParagraphCount { get; set; }

    public int TableCount { get; set; }

    public int DrawingCount { get; set; }

    public int FiguresCount { get; set; }

    public int HeaderCount { get; set; }

    public int FooterCount { get; set; }
}

public sealed class StyleSummary
{
    public string StyleId { get; set; } = string.Empty;

    public string? Type { get; set; }

    public string? BasedOn { get; set; }
}

public sealed class NumberingSummary
{
    public int? AbstractNumberId { get; set; }

    public List<string> LevelTexts { get; set; } = [];
}

public sealed class SectionSummary
{
    public int Index { get; set; }

    public string PageNumberFormat { get; set; } = "none";

    public int? PageNumberStart { get; set; }

    public string? WidthTwips { get; set; }

    public string? HeightTwips { get; set; }

    public string? TopMarginTwips { get; set; }

    public string? BottomMarginTwips { get; set; }

    public string? LeftMarginTwips { get; set; }

    public string? RightMarginTwips { get; set; }
}

public sealed class HeaderFooterSummary
{
    public string Kind { get; set; } = string.Empty;

    public string RelationshipId { get; set; } = string.Empty;

    public bool HasPageField { get; set; }

    public bool HasHeaderLine { get; set; }
}

public sealed class NoteInspectionSummary
{
    public int Count { get; set; }

    public List<long> Ids { get; set; } = [];

    public bool HasPart { get; set; }

    public bool HasSeparator { get; set; }

    public bool HasContinuationSeparator { get; set; }
}

public sealed class BibliographyInspectionSummary
{
    public int EntryCount { get; set; }

    public List<string> HangingIndents { get; set; } = [];
}

public sealed class EquationInspectionSummary
{
    public int Count { get; set; }

    public List<string> Ids { get; set; } = [];

    public List<string> SourceTypes { get; set; } = [];

    public bool HasNumbering { get; set; }

    public List<string> Bookmarks { get; set; } = [];

    public int OmmlElementCount { get; set; }

    public List<string> RefFields { get; set; } = [];
}

public sealed class TableInspectionSummary
{
    public int Count { get; set; }

    public List<string> Captions { get; set; } = [];

    public List<string> Bookmarks { get; set; } = [];

    public List<string> Styles { get; set; } = [];

    public bool HasGridSpan { get; set; }

    public bool HasVerticalMerge { get; set; }

    public bool HasRepeatHeaderRows { get; set; }

    public bool HasCantSplitRows { get; set; }

    public List<string> WidthTypes { get; set; } = [];

    public List<string> BorderSummary { get; set; } = [];
}

public sealed class TemplateRenderingInspectionSummary
{
    public string? TemplateId { get; set; }

    public string? TemplateVersion { get; set; }

    public string? RendererVersion { get; set; }

    public string? ResolvedFormatSpecVersion { get; set; }

    public List<string> RenderedPageTemplates { get; set; } = [];

    public List<string> RenderedVariables { get; set; } = [];

    public List<string> RenderedAssets { get; set; } = [];

    public CoverInspectionSummary CoverSummary { get; set; } = new();

    public DeclarationInspectionSummary DeclarationSummary { get; set; } = new();
}

public sealed class CoverInspectionSummary
{
    public bool HasTitle { get; set; }

    public bool HasMetadataFieldTable { get; set; }

    public bool HasLogoDrawing { get; set; }
}

public sealed class DeclarationInspectionSummary
{
    public bool HasDeclarationText { get; set; }

    public bool HasSignatureField { get; set; }
}
