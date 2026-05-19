using System.Text.Json.Serialization;

namespace ThesisDocx.Core.Models;

public sealed class ThesisDocument
{
    public string SchemaVersion { get; set; } = ThesisSchemaVersions.Current;

    public ThesisMetadata Metadata { get; set; } = new();

    public List<ThesisSection> Sections { get; set; } = [];
}

public sealed class ThesisMetadata
{
    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public string Author { get; set; } = string.Empty;

    public string College { get; set; } = string.Empty;

    public string Major { get; set; } = string.Empty;

    public string StudentId { get; set; } = string.Empty;

    public string Advisor { get; set; } = string.Empty;

    public string Date { get; set; } = string.Empty;

    public string Language { get; set; } = "zh-CN";
}

public sealed class ThesisSection
{
    public string? Id { get; set; }

    public ThesisSectionKind Kind { get; set; } = ThesisSectionKind.Body;

    public string? Title { get; set; }

    public bool StartOnNewPage { get; set; }

    public List<BlockNode> Blocks { get; set; } = [];
}

public enum ThesisSectionKind
{
    Cover,
    OriginalityStatement,
    Abstract,
    Toc,
    Body,
    Acknowledgements,
    Bibliography,
    Appendix,
    TeacherComments
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ParagraphBlock), "paragraph")]
[JsonDerivedType(typeof(HeadingBlock), "heading")]
[JsonDerivedType(typeof(ListBlock), "list")]
[JsonDerivedType(typeof(FigureBlock), "figure")]
[JsonDerivedType(typeof(TableBlock), "table")]
[JsonDerivedType(typeof(QuoteBlock), "quote")]
[JsonDerivedType(typeof(EquationBlock), "equation")]
[JsonDerivedType(typeof(PreservedObjectBlock), "preservedObject")]
[JsonDerivedType(typeof(PageBreakBlock), "pageBreak")]
[JsonDerivedType(typeof(SectionBreakBlock), "sectionBreak")]
[JsonDerivedType(typeof(BibliographyBlock), "bibliography")]
[JsonDerivedType(typeof(FootnoteBlock), "footnote")]
[JsonDerivedType(typeof(EndnoteBlock), "endnote")]
public abstract class BlockNode
{
    public string? Id { get; set; }
}

public sealed class ParagraphBlock : BlockNode
{
    public List<InlineNode> Inlines { get; set; } = [];

    public string? StyleId { get; set; }

    public TextAlignment? Alignment { get; set; }
}

public sealed class HeadingBlock : BlockNode
{
    public int Level { get; set; } = 1;

    public List<InlineNode> Inlines { get; set; } = [];

    public string? BookmarkName { get; set; }

    public bool Numbered { get; set; } = true;
}

public sealed class ListBlock : BlockNode
{
    public bool Ordered { get; set; } = true;

    public List<ListItemNode> Items { get; set; } = [];
}

public sealed class ListItemNode
{
    public List<BlockNode> Blocks { get; set; } = [];
}

public sealed class FigureBlock : BlockNode
{
    public string Caption { get; set; } = string.Empty;

    public string? ImagePath { get; set; }

    public string? ImageDataBase64 { get; set; }

    public string ImageContentType { get; set; } = "image/png";

    public double? WidthCm { get; set; }

    public double? HeightCm { get; set; }

    public FigureCropSpec? Crop { get; set; }
}

public sealed class FigureCropSpec
{
    public double? LeftPercent { get; set; }

    public double? TopPercent { get; set; }

    public double? RightPercent { get; set; }

    public double? BottomPercent { get; set; }
}

public sealed class TableBlock : BlockNode
{
    public string? BookmarkId { get; set; }

    public string Caption { get; set; } = string.Empty;

    public CaptionPosition? CaptionPosition { get; set; }

    public TableStyleKind Style { get; set; } = TableStyleKind.Normal;

    public TableWidthSpec? Width { get; set; }

    public TextAlignment? Alignment { get; set; }

    public TableLayoutKind? Layout { get; set; }

    public bool? AllowRowBreakAcrossPages { get; set; }

    public int? RepeatHeaderRows { get; set; }

    public TableBordersSpec? Borders { get; set; }

    public TableCellMarginsSpec? CellMargins { get; set; }

    public List<TableRowNode> Rows { get; set; } = [];
}

public sealed class TableRowNode
{
    public string? Id { get; set; }

    public bool IsHeader { get; set; }

    public bool? CantSplit { get; set; }

    public double? HeightPt { get; set; }

    public List<TableCellNode> Cells { get; set; } = [];
}

public sealed class TableCellNode
{
    public string? Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public List<BlockNode> Blocks { get; set; } = [];

    public int GridSpan { get; set; } = 1;

    public VerticalMergeKind VerticalMerge { get; set; } = VerticalMergeKind.None;

    public TableWidthSpec? Width { get; set; }

    public double? WidthCm { get; set; }

    public TextAlignment? Alignment { get; set; }

    public TableCellVerticalAlignment? VerticalAlignment { get; set; }

    public string? Shading { get; set; }

    public TableBordersSpec? Borders { get; set; }

    public TableCellMarginsSpec? CellMargins { get; set; }

    public FontFormatSpec? Font { get; set; }

    public ParagraphFormatSpec? Paragraph { get; set; }
}

public enum TableStyleKind
{
    Normal,
    ThreeLine,
    Custom
}

public enum CaptionPosition
{
    Before,
    After
}

public enum TableWidthKind
{
    Auto,
    Percent,
    Dxa
}

public sealed class TableWidthSpec
{
    public TableWidthKind Type { get; set; } = TableWidthKind.Auto;

    public double? Value { get; set; }
}

public enum TableLayoutKind
{
    Autofit,
    Fixed
}

public enum VerticalMergeKind
{
    None,
    Restart,
    Continue
}

public enum TableCellVerticalAlignment
{
    Top,
    Center,
    Bottom
}

public sealed class TableBordersSpec
{
    public BorderSpec? Top { get; set; }

    public BorderSpec? Bottom { get; set; }

    public BorderSpec? Left { get; set; }

    public BorderSpec? Right { get; set; }

    public BorderSpec? InsideH { get; set; }

    public BorderSpec? InsideV { get; set; }
}

public sealed class BorderSpec
{
    public BorderStyleKind Style { get; set; } = BorderStyleKind.Single;

    public int Size { get; set; } = 4;

    public string Color { get; set; } = "000000";

    public int Space { get; set; }
}

public enum BorderStyleKind
{
    Nil,
    None,
    Single,
    Double,
    Dotted,
    Dashed
}

public sealed class TableCellMarginsSpec
{
    public double? TopCm { get; set; }

    public double? BottomCm { get; set; }

    public double? LeftCm { get; set; }

    public double? RightCm { get; set; }
}

public sealed class QuoteBlock : BlockNode
{
    public List<InlineNode> Inlines { get; set; } = [];
}

public sealed class EquationBlock : BlockNode
{
    public string Placeholder { get; set; } = string.Empty;

    public string? BookmarkName { get; set; }

    public string? BookmarkId { get; set; }

    public EquationSourceType SourceType { get; set; } = EquationSourceType.Plain;

    public string? Omml { get; set; }

    public string? Latex { get; set; }

    public string? PlainText { get; set; }

    public bool Display { get; set; } = true;

    public TextAlignment Alignment { get; set; } = TextAlignment.Center;

    public string? Caption { get; set; }

    public EquationNumberingSpec? Numbering { get; set; }

    public bool? AllowWordUpdate { get; set; }
}

public sealed class PreservedObjectBlock : BlockNode
{
    public PreservedObjectType ObjectType { get; set; } = PreservedObjectType.Drawing;

    public PreservedObjectMode PreservationMode { get; set; } = PreservedObjectMode.ReviewOnly;

    public string? RawXml { get; set; }

    public string? GraphicDataUri { get; set; }

    public List<string> RelationshipIds { get; set; } = [];

    public List<PreservedObjectPart> Parts { get; set; } = [];

    public double? WidthCm { get; set; }

    public double? HeightCm { get; set; }

    public string? AnchorType { get; set; }

    public string? ExtractedText { get; set; }

    public string? EvidencePath { get; set; }
}

public enum PreservedObjectType
{
    Chart,
    SmartArt,
    Shape,
    TextBox,
    Picture,
    Drawing
}

public enum PreservedObjectMode
{
    ReviewOnly,
    ExtractText,
    Passthrough
}

public sealed class PreservedObjectPart
{
    public string RelationshipId { get; set; } = string.Empty;

    public string RelationshipType { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string DataBase64 { get; set; } = string.Empty;

    public List<PreservedObjectPart> Children { get; set; } = [];
}

public enum EquationSourceType
{
    Omml,
    Latex,
    Plain
}

public sealed class EquationNumberingSpec
{
    public bool Enabled { get; set; }

    public string Label { get; set; } = "公式";

    public string Format { get; set; } = "({chapter}.{index})";

    public int RestartByHeadingLevel { get; set; } = 1;
}

public sealed class PageBreakBlock : BlockNode;

public sealed class SectionBreakBlock : BlockNode;

public sealed class BibliographyBlock : BlockNode
{
    public List<BibliographyEntryNode> Entries { get; set; } = [];
}

public sealed class FootnoteBlock : BlockNode
{
    public string NoteId { get; set; } = string.Empty;

    public List<InlineNode> Inlines { get; set; } = [];
}

public sealed class EndnoteBlock : BlockNode
{
    public string NoteId { get; set; } = string.Empty;

    public List<InlineNode> Inlines { get; set; } = [];
}

public sealed class BibliographyEntryNode
{
    public string Id { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextInline), "text")]
[JsonDerivedType(typeof(HyperlinkInline), "hyperlink")]
[JsonDerivedType(typeof(CitationInline), "citation")]
[JsonDerivedType(typeof(BookmarkInline), "bookmark")]
[JsonDerivedType(typeof(ReferenceInline), "reference")]
[JsonDerivedType(typeof(FootnoteInline), "footnote")]
[JsonDerivedType(typeof(EndnoteInline), "endnote")]
public abstract class InlineNode;

public sealed class TextInline : InlineNode
{
    public string Text { get; set; } = string.Empty;

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    public bool Underline { get; set; }

    public VerticalAlignment? VerticalAlignment { get; set; }
}

public sealed class HyperlinkInline : InlineNode
{
    public string Text { get; set; } = string.Empty;

    public string Uri { get; set; } = string.Empty;
}

public sealed class CitationInline : InlineNode
{
    public string TargetId { get; set; } = string.Empty;

    public string DisplayText { get; set; } = string.Empty;
}

public sealed class BookmarkInline : InlineNode
{
    public string Name { get; set; } = string.Empty;

    public List<InlineNode> Inlines { get; set; } = [];
}

public sealed class ReferenceInline : InlineNode
{
    public string BookmarkName { get; set; } = string.Empty;

    public string? FallbackText { get; set; }
}

public sealed class FootnoteInline : InlineNode
{
    public string NoteId { get; set; } = string.Empty;

    public List<InlineNode> Inlines { get; set; } = [];
}

public sealed class EndnoteInline : InlineNode
{
    public string NoteId { get; set; } = string.Empty;

    public List<InlineNode> Inlines { get; set; } = [];
}

public enum VerticalAlignment
{
    Baseline,
    Subscript,
    Superscript
}

public enum TextAlignment
{
    Left,
    Center,
    Right,
    Both
}

public static class ThesisSchemaVersions
{
    public const string Version100 = "1.0.0";
    public const string Version110 = "1.1.0";
    public const string Version120 = "1.2.0";
    public const string Current = Version120;

    public static bool IsSupported(string? schemaVersion)
    {
        return schemaVersion is Version100 or Version110 or Version120;
    }

    public static bool IsSupportedFormat(string? schemaVersion)
    {
        return schemaVersion is Version100 or Version110 or Version120;
    }
}
