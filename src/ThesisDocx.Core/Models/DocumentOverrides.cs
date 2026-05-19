namespace ThesisDocx.Core.Models;

public sealed class DocumentOverrides
{
    public TocOverrideSpec? Toc { get; set; }

    public HeaderFooterOverrideSpec? HeaderFooter { get; set; }

    public FontOverrideSpec? DefaultFont { get; set; }

    public ParagraphOverrideSpec? BodyParagraph { get; set; }

    public Dictionary<int, HeadingOverrideSpec>? Headings { get; set; }

    public Dictionary<string, SectionFormatOverrideSpec>? SectionFormats { get; set; }

    public Dictionary<string, SectionInstanceOverrideSpec>? SectionInstances { get; set; }
}

public sealed class TocOverrideSpec
{
    public int? MinLevel { get; set; }

    public int? MaxLevel { get; set; }

    public string? Title { get; set; }

    public List<string>? IncludeSectionIds { get; set; }
}

public sealed class HeaderFooterOverrideSpec
{
    public string? HeaderText { get; set; }

    public bool? DrawHeaderLine { get; set; }

    public bool? HidePageNumberOnCover { get; set; }

    public bool? DifferentFirstPage { get; set; }
}

public sealed class FontOverrideSpec
{
    public string? EastAsia { get; set; }

    public string? Latin { get; set; }

    public double? SizePt { get; set; }

    public bool? Bold { get; set; }

    public bool? Italic { get; set; }
}

public sealed class ParagraphOverrideSpec
{
    public double? LineSpacingMultiple { get; set; }

    public double? LineSpacingExactPt { get; set; }

    public double? SpaceBeforePt { get; set; }

    public double? SpaceAfterPt { get; set; }

    public double? FirstLineIndentChars { get; set; }

    public double? HangingIndentCm { get; set; }

    public TextAlignment? Alignment { get; set; }

    public bool? WidowControl { get; set; }
}

public sealed class HeadingOverrideSpec
{
    public FontOverrideSpec? Font { get; set; }

    public double? SpaceBeforePt { get; set; }

    public double? SpaceAfterPt { get; set; }

    public bool? Numbered { get; set; }

    public bool? PageBreakBefore { get; set; }

    public int? OutlineLevel { get; set; }

    public TextAlignment? Alignment { get; set; }
}

public class SectionFormatOverrideSpec
{
    public PageNumberStyle? PageNumberStyle { get; set; }

    public int? StartPageNumber { get; set; }

    public bool? RestartPageNumbering { get; set; }

    public bool? IncludeHeader { get; set; }

    public bool? IncludeFooter { get; set; }
}

public sealed class SectionInstanceOverrideSpec : SectionFormatOverrideSpec
{
    public string? HeaderText { get; set; }

    public string? FooterText { get; set; }

    public FontOverrideSpec? TitleFont { get; set; }

    public ParagraphOverrideSpec? TitleParagraph { get; set; }

    public ParagraphOverrideSpec? Paragraph { get; set; }

    public FontOverrideSpec? DefaultFont { get; set; }

    public Dictionary<int, BlockFormatOverrideSpec>? BlockOverrides { get; set; }
}

public sealed class BlockFormatOverrideSpec
{
    public FontOverrideSpec? Font { get; set; }

    public ParagraphOverrideSpec? Paragraph { get; set; }
}
