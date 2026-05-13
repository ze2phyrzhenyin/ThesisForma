namespace ThesisDocx.Core.Models;

public sealed class ThesisFormatSpec
{
    public string SchemaVersion { get; set; } = ThesisSchemaVersions.Current;

    public string Name { get; set; } = "basic-cn-thesis";

    public PageSetupSpec PageSetup { get; set; } = new();

    public FontFormatSpec DefaultFont { get; set; } = new();

    public ParagraphFormatSpec BodyParagraph { get; set; } = new();

    public Dictionary<int, HeadingFormatSpec> Headings { get; set; } = DefaultHeadingFormats();

    public HeaderFooterFormatSpec HeaderFooter { get; set; } = new();

    public TocFormatSpec Toc { get; set; } = new();

    public TableFormatSpec Tables { get; set; } = new();

    public EquationFormatSpec Equations { get; set; } = new();

    public NotesFormatSpec Notes { get; set; } = new();

    public FigureFormatSpec Figures { get; set; } = new();

    public CaptionFormatSpec Captions { get; set; } = new();

    public BibliographyFormatSpec Bibliography { get; set; } = new();

    public NumberingFormatSpec Numbering { get; set; } = new();

    public CompatibilityFormatSpec Compatibility { get; set; } = new();

    public Dictionary<string, SectionFormatSpec> Sections { get; set; } = DefaultSectionFormats();

    public CoverPageFormatSpec CoverPage { get; set; } = new();

    public ValidationBehaviorSpec Validation { get; set; } = new();

    private static Dictionary<int, HeadingFormatSpec> DefaultHeadingFormats()
    {
        return new Dictionary<int, HeadingFormatSpec>
        {
            [1] = new() { Level = 1, Font = new FontFormatSpec { SizePt = 16, Bold = true }, SpaceBeforePt = 12, SpaceAfterPt = 6 },
            [2] = new() { Level = 2, Font = new FontFormatSpec { SizePt = 14, Bold = true }, SpaceBeforePt = 8, SpaceAfterPt = 4 },
            [3] = new() { Level = 3, Font = new FontFormatSpec { SizePt = 12, Bold = true }, SpaceBeforePt = 6, SpaceAfterPt = 3 }
        };
    }

    private static Dictionary<string, SectionFormatSpec> DefaultSectionFormats()
    {
        return new Dictionary<string, SectionFormatSpec>
        {
            ["cover"] = new() { PageNumberStyle = PageNumberStyle.None, RestartPageNumbering = true },
            ["frontMatter"] = new() { PageNumberStyle = PageNumberStyle.LowerRoman, RestartPageNumbering = true },
            ["body"] = new() { PageNumberStyle = PageNumberStyle.Decimal, RestartPageNumbering = true }
        };
    }
}

public sealed class ValidationBehaviorSpec
{
    public bool AllowHeadingLevelSkips { get; set; }
}

public sealed class PageSetupSpec
{
    public PaperSizeKind PaperSize { get; set; } = PaperSizeKind.A4;

    public PageOrientationKind Orientation { get; set; } = PageOrientationKind.Portrait;

    public double TopMarginCm { get; set; } = 2.54;

    public double BottomMarginCm { get; set; } = 2.54;

    public double LeftMarginCm { get; set; } = 3.0;

    public double RightMarginCm { get; set; } = 2.5;

    public double GutterCm { get; set; }

    public double HeaderDistanceCm { get; set; } = 1.5;

    public double FooterDistanceCm { get; set; } = 1.75;

    public int Columns { get; set; } = 1;
}

public enum PaperSizeKind
{
    A4,
    Letter
}

public enum PageOrientationKind
{
    Portrait,
    Landscape
}

public sealed class FontFormatSpec
{
    public string EastAsia { get; set; } = "宋体";

    public string Latin { get; set; } = "Times New Roman";

    public double SizePt { get; set; } = 12;

    public bool Bold { get; set; }

    public bool Italic { get; set; }
}

public sealed class ParagraphFormatSpec
{
    public double LineSpacingMultiple { get; set; } = 1.5;

    public double? LineSpacingExactPt { get; set; }

    public double SpaceBeforePt { get; set; }

    public double SpaceAfterPt { get; set; }

    public double FirstLineIndentChars { get; set; } = 2;

    public double HangingIndentCm { get; set; }

    public TextAlignment Alignment { get; set; } = TextAlignment.Both;

    public bool WidowControl { get; set; } = true;
}

public sealed class HeadingFormatSpec
{
    public int Level { get; set; }

    public FontFormatSpec Font { get; set; } = new() { Bold = true };

    public double SpaceBeforePt { get; set; }

    public double SpaceAfterPt { get; set; }

    public bool Numbered { get; set; } = true;

    public bool PageBreakBefore { get; set; }

    public int OutlineLevel { get; set; }

    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
}

public sealed class HeaderFooterFormatSpec
{
    public string HeaderText { get; set; } = string.Empty;

    public bool DrawHeaderLine { get; set; } = true;

    public TextAlignment HeaderAlignment { get; set; } = TextAlignment.Center;

    public TextAlignment PageNumberAlignment { get; set; } = TextAlignment.Center;

    public bool HidePageNumberOnCover { get; set; } = true;

    public bool DifferentFirstPage { get; set; }

    public bool DifferentOddEven { get; set; }
}

public sealed class TocFormatSpec
{
    public string Title { get; set; } = "目录";

    public int MinLevel { get; set; } = 1;

    public int MaxLevel { get; set; } = 3;

    public bool UseWordFieldCode { get; set; } = true;
}

public sealed class TableFormatSpec
{
    public double WidthPercent { get; set; } = 100;

    public double CellMarginCm { get; set; } = 0.1;

    public bool UseThreeLineTables { get; set; } = true;

    public string CaptionPosition { get; set; } = "above";

    public TextAlignment DefaultAlignment { get; set; } = TextAlignment.Center;

    public TableWidthSpec DefaultWidth { get; set; } = new() { Type = TableWidthKind.Percent, Value = 100 };

    public TableLayoutKind DefaultLayout { get; set; } = TableLayoutKind.Autofit;

    public TableCellMarginsSpec DefaultCellMargins { get; set; } = new() { TopCm = 0.1, BottomCm = 0.1, LeftCm = 0.1, RightCm = 0.1 };

    public TableBordersSpec DefaultBorders { get; set; } = new()
    {
        Top = new BorderSpec(),
        Bottom = new BorderSpec(),
        Left = new BorderSpec(),
        Right = new BorderSpec(),
        InsideH = new BorderSpec(),
        InsideV = new BorderSpec()
    };

    public TableBordersSpec ThreeLineTableBorders { get; set; } = new()
    {
        Top = new BorderSpec { Style = BorderStyleKind.Single, Size = 12 },
        Bottom = new BorderSpec { Style = BorderStyleKind.Single, Size = 12 },
        Left = new BorderSpec { Style = BorderStyleKind.Nil },
        Right = new BorderSpec { Style = BorderStyleKind.Nil },
        InsideH = new BorderSpec { Style = BorderStyleKind.Single, Size = 6 },
        InsideV = new BorderSpec { Style = BorderStyleKind.Nil }
    };

    public TableCaptionFormatSpec Caption { get; set; } = new();

    public int RepeatHeaderRowsDefault { get; set; }

    public bool AllowRowBreakAcrossPagesDefault { get; set; } = true;
}

public sealed class TableCaptionFormatSpec
{
    public CaptionPosition Position { get; set; } = CaptionPosition.Before;

    public string Style { get; set; } = "ThesisCaption";

    public bool Numbering { get; set; } = true;
}

public sealed class EquationFormatSpec
{
    public TextAlignment DefaultAlignment { get; set; } = TextAlignment.Center;

    public double FontSizePt { get; set; } = 12;

    public EquationNumberingFormatSpec Numbering { get; set; } = new();

    public double SpacingBeforePt { get; set; } = 6;

    public double SpacingAfterPt { get; set; } = 6;

    public string CaptionStyle { get; set; } = "ThesisCaption";

    public bool AllowLatexFallbackToPlain { get; set; } = true;

    public string OmmlSafetyMode { get; set; } = "strict";
}

public sealed class EquationNumberingFormatSpec
{
    public bool Enabled { get; set; } = true;

    public string Label { get; set; } = "公式";

    public string Format { get; set; } = "({chapter}.{index})";

    public int RestartByHeadingLevel { get; set; } = 1;

    public EquationNumberPosition Position { get; set; } = EquationNumberPosition.Right;
}

public enum EquationNumberPosition
{
    Right,
    Caption
}

public sealed class NotesFormatSpec
{
    public NoteFormatSpec Footnote { get; set; } = new()
    {
        StyleId = "ThesisFootnoteText",
        Font = new FontFormatSpec { SizePt = 10.5 },
        Paragraph = new ParagraphFormatSpec
        {
            LineSpacingMultiple = 1.0,
            SpaceBeforePt = 0,
            SpaceAfterPt = 0,
            FirstLineIndentChars = 0,
            HangingIndentCm = 0,
            Alignment = TextAlignment.Both,
            WidowControl = true
        }
    };

    public NoteFormatSpec Endnote { get; set; } = new()
    {
        StyleId = "ThesisEndnoteText",
        Font = new FontFormatSpec { SizePt = 10.5 },
        Paragraph = new ParagraphFormatSpec
        {
            LineSpacingMultiple = 1.0,
            SpaceBeforePt = 0,
            SpaceAfterPt = 0,
            FirstLineIndentChars = 0,
            HangingIndentCm = 0,
            Alignment = TextAlignment.Both,
            WidowControl = true
        }
    };
}

public sealed class NoteFormatSpec
{
    public string StyleId { get; set; } = string.Empty;

    public FontFormatSpec Font { get; set; } = new() { SizePt = 10.5 };

    public ParagraphFormatSpec Paragraph { get; set; } = new()
    {
        LineSpacingMultiple = 1.0,
        FirstLineIndentChars = 0,
        Alignment = TextAlignment.Both
    };

    public bool SuperscriptReferenceMark { get; set; } = true;
}

public sealed class FigureFormatSpec
{
    public double DefaultWidthCm { get; set; } = 12;

    public double? DefaultHeightCm { get; set; }

    public bool Center { get; set; } = true;

    public string CaptionPosition { get; set; } = "below";
}

public sealed class CaptionFormatSpec
{
    public string FigureLabel { get; set; } = "图";

    public string TableLabel { get; set; } = "表";

    public string EquationLabel { get; set; } = "式";

    public string Separator { get; set; } = " ";

    public string NumberFormat { get; set; } = "{label}{number}{separator}{text}";
}

public sealed class BibliographyFormatSpec
{
    public string Title { get; set; } = "参考文献";

    public ParagraphFormatSpec EntryParagraph { get; set; } = new()
    {
        FirstLineIndentChars = 0,
        HangingIndentCm = 0.74,
        LineSpacingMultiple = 1.0
    };

    public string NumberFormat { get; set; } = "[%1]";
}

public sealed class NumberingFormatSpec
{
    public string HeadingLevel1Text { get; set; } = "第%1章";

    public string HeadingLevel2Text { get; set; } = "%1.%2";

    public string HeadingLevel3Text { get; set; } = "%1.%2.%3";

    public string OrderedListText { get; set; } = "%1.";

    public string BibliographyText { get; set; } = "[%1]";
}

public sealed class CompatibilityFormatSpec
{
    public string DefaultLanguage { get; set; } = "zh-CN";

    public bool UseEastAsiaFonts { get; set; } = true;

    public string WordCompatibilityMode { get; set; } = "Word2019";
}

public sealed class SectionFormatSpec
{
    public PageNumberStyle PageNumberStyle { get; set; } = PageNumberStyle.Decimal;

    public bool RestartPageNumbering { get; set; }

    public int StartPageNumber { get; set; } = 1;

    public bool IncludeHeader { get; set; } = true;

    public bool IncludeFooter { get; set; } = true;
}

public enum PageNumberStyle
{
    None,
    Decimal,
    LowerRoman,
    UpperRoman
}

public sealed class CoverPageFormatSpec
{
    public string[] FieldOrder { get; set; } = ["title", "author", "studentId", "college", "major", "advisor", "date"];
}
