namespace ThesisDocx.Core.Diff.Layout;

public sealed class DocxLayoutSignature
{
    public string SourcePath { get; set; } = string.Empty;

    public List<LayoutSectionSignature> Sections { get; set; } = [];

    public List<LayoutStyleSignature> Styles { get; set; } = [];

    public List<LayoutTableSignature> Tables { get; set; } = [];

    public List<LayoutFigureSignature> Figures { get; set; } = [];

    public LayoutEquationSignature Equations { get; set; } = new();

    public LayoutFieldSignature Fields { get; set; } = new();

    public LayoutNoteSignature Footnotes { get; set; } = new();

    public LayoutNoteSignature Endnotes { get; set; } = new();

    public LayoutBibliographySignature Bibliography { get; set; } = new();

    public Dictionary<string, string> CustomProperties { get; set; } = new(StringComparer.Ordinal);
}

public sealed class LayoutSectionSignature
{
    public int Index { get; set; }

    public string? PageWidthTwips { get; set; }

    public string? PageHeightTwips { get; set; }

    public string? TopMarginTwips { get; set; }

    public string? BottomMarginTwips { get; set; }

    public string? LeftMarginTwips { get; set; }

    public string? RightMarginTwips { get; set; }

    public string? HeaderDistanceTwips { get; set; }

    public string? FooterDistanceTwips { get; set; }

    public string PageNumberFormat { get; set; } = "none";

    public int? PageNumberStart { get; set; }
}

public sealed class LayoutStyleSignature
{
    public string StyleId { get; set; } = string.Empty;

    public string? EastAsiaFont { get; set; }

    public string? LatinFont { get; set; }

    public string? FontSizeHalfPoints { get; set; }

    public string? Bold { get; set; }

    public string? LineSpacing { get; set; }

    public string? FirstLineIndent { get; set; }

    public string? HangingIndent { get; set; }

    public string? Alignment { get; set; }

    public string? OutlineLevel { get; set; }
}

public sealed class LayoutTableSignature
{
    public int Index { get; set; }

    public string? WidthType { get; set; }

    public string? Width { get; set; }

    public string Borders { get; set; } = string.Empty;

    public bool HasGridSpan { get; set; }

    public bool HasVerticalMerge { get; set; }

    public bool HasRepeatHeaderRows { get; set; }

    public bool HasCantSplitRows { get; set; }
}

public sealed class LayoutFigureSignature
{
    public int Index { get; set; }

    public string? Cx { get; set; }

    public string? Cy { get; set; }

    public string? Caption { get; set; }
}

public sealed class LayoutEquationSignature
{
    public int OmmlCount { get; set; }

    public bool HasNumbering { get; set; }

    public List<string> Bookmarks { get; set; } = [];
}

public sealed class LayoutFieldSignature
{
    public int TocCount { get; set; }

    public int PageCount { get; set; }

    public int RefCount { get; set; }

    public List<string> Instructions { get; set; } = [];
}

public sealed class LayoutNoteSignature
{
    public bool HasPart { get; set; }

    public int Count { get; set; }

    public List<long> Ids { get; set; } = [];
}

public sealed class LayoutBibliographySignature
{
    public int EntryCount { get; set; }

    public List<string> HangingIndents { get; set; } = [];
}

public sealed class LayoutSignatureCompareResult
{
    public double SimilarityScore { get; set; }

    public bool MeetsThreshold { get; set; }

    public double Threshold { get; set; }

    public List<LayoutSignatureDifference> BreakingDifferences { get; set; } = [];

    public List<LayoutSignatureDifference> Warnings { get; set; } = [];

    public List<LayoutSignatureDifference> Differences { get; set; } = [];
}

public sealed class LayoutSignatureDifference
{
    public string Path { get; set; } = string.Empty;

    public string? BaseValue { get; set; }

    public string? TargetValue { get; set; }

    public string Category { get; set; } = "unknown";

    public DocxDiffSeverity Severity { get; set; } = DocxDiffSeverity.Warning;
}
