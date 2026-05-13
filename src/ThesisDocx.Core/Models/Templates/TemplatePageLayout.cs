using System.Text.Json.Serialization;

namespace ThesisDocx.Core.Models.Templates;

public sealed class TemplatePageLayout
{
    public string Id { get; set; } = string.Empty;

    public PageTemplateTargetSectionType TargetSectionType { get; set; } = PageTemplateTargetSectionType.Cover;

    public PageTemplateInsertPosition InsertPosition { get; set; } = PageTemplateInsertPosition.ReplaceSectionContent;

    public PageSetupSpec? PageSetupOverride { get; set; }

    public List<PageLayoutBlock> Blocks { get; set; } = [];
}

public enum PageTemplateTargetSectionType
{
    Cover,
    Declaration,
    Abstract,
    Toc,
    Body,
    Appendix
}

public enum PageTemplateInsertPosition
{
    BeforeSection,
    AfterSection,
    ReplaceSectionContent
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SpacerLayoutBlock), "spacer")]
[JsonDerivedType(typeof(TextLayoutBlock), "text")]
[JsonDerivedType(typeof(MetadataFieldLayoutBlock), "metadataField")]
[JsonDerivedType(typeof(ImageLayoutBlock), "image")]
[JsonDerivedType(typeof(FieldTableLayoutBlock), "fieldTable")]
[JsonDerivedType(typeof(DeclarationTextLayoutBlock), "declarationText")]
[JsonDerivedType(typeof(PageBreakLayoutBlock), "pageBreak")]
[JsonDerivedType(typeof(RuleLayoutBlock), "rule")]
public abstract class PageLayoutBlock;

public sealed class SpacerLayoutBlock : PageLayoutBlock
{
    public double HeightCm { get; set; } = 0.5;
}

public sealed class TextLayoutBlock : PageLayoutBlock
{
    public string Value { get; set; } = string.Empty;

    public string Style { get; set; } = "ThesisBody";

    public TextAlignment Alignment { get; set; } = TextAlignment.Center;

    public FontFormatSpec? FontOverride { get; set; }

    public double? SpacingBeforePt { get; set; }

    public double? SpacingAfterPt { get; set; }
}

public sealed class MetadataFieldLayoutBlock : PageLayoutBlock
{
    public string Label { get; set; } = string.Empty;

    public string? SourcePath { get; set; }

    public string? VariableName { get; set; }

    public string? ValueTemplate { get; set; }

    public MetadataFieldLayoutKind Layout { get; set; } = MetadataFieldLayoutKind.LabelValueLine;

    public bool Underline { get; set; }

    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
}

public enum MetadataFieldLayoutKind
{
    Inline,
    LabelValueLine,
    TableRow
}

public sealed class ImageLayoutBlock : PageLayoutBlock
{
    public string AssetId { get; set; } = string.Empty;

    public double WidthCm { get; set; } = 3;

    public double? HeightCm { get; set; }

    public TextAlignment Alignment { get; set; } = TextAlignment.Center;
}

public sealed class FieldTableLayoutBlock : PageLayoutBlock
{
    public int Columns { get; set; } = 2;

    public List<List<MetadataFieldLayoutBlock>> Rows { get; set; } = [];

    public FieldTableBorderMode BorderMode { get; set; } = FieldTableBorderMode.BottomLine;

    public double LabelColumnWidthCm { get; set; } = 3;

    public double ValueColumnWidthCm { get; set; } = 9;
}

public enum FieldTableBorderMode
{
    None,
    Full,
    BottomLine,
    Custom
}

public sealed class DeclarationTextLayoutBlock : PageLayoutBlock
{
    public List<string> Paragraphs { get; set; } = [];

    public List<MetadataFieldLayoutBlock> SignatureFields { get; set; } = [];
}

public sealed class PageBreakLayoutBlock : PageLayoutBlock;

public sealed class RuleLayoutBlock : PageLayoutBlock
{
    public double ThicknessPt { get; set; } = 1;

    public string Color { get; set; } = "000000";

    public TextAlignment Alignment { get; set; } = TextAlignment.Center;

    public double? SpacingBeforePt { get; set; }

    public double? SpacingAfterPt { get; set; }
}
