using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.OpenXml;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace ThesisDocx.Core.Diff.Layout;

public sealed class DocxLayoutSignatureExtractor
{
    private const string WordprocessingNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public DocxLayoutSignature Extract(string docxPath)
    {
        using var document = WordprocessingDocument.Open(docxPath, false);
        var mainPart = document.MainDocumentPart ?? throw new InvalidOperationException("Missing main document part.");
        var fieldInstructions = CollectFields(document).Order(StringComparer.Ordinal).ToList();
        return new DocxLayoutSignature
        {
            SourcePath = docxPath,
            Sections = ExtractSections(mainPart),
            Styles = ExtractStyles(mainPart),
            Tables = ExtractTables(mainPart),
            Figures = ExtractFigures(mainPart),
            Equations = ExtractEquations(mainPart),
            Fields = new LayoutFieldSignature
            {
                TocCount = fieldInstructions.Count(f => f.Contains("TOC", StringComparison.OrdinalIgnoreCase)),
                PageCount = fieldInstructions.Count(f => f.Contains("PAGE", StringComparison.OrdinalIgnoreCase)),
                RefCount = fieldInstructions.Count(f => f.TrimStart().StartsWith("REF", StringComparison.OrdinalIgnoreCase)),
                Instructions = fieldInstructions
            },
            Footnotes = ExtractFootnotes(mainPart),
            Endnotes = ExtractEndnotes(mainPart),
            Bibliography = ExtractBibliography(mainPart),
            CustomProperties = ExtractCustomProperties(document)
        };
    }

    private static List<LayoutSectionSignature> ExtractSections(MainDocumentPart mainPart)
    {
        return mainPart.Document.Body?.Descendants<W.SectionProperties>()
            .Select((section, index) =>
            {
                var pageSize = section.GetFirstChild<W.PageSize>();
                var margin = section.GetFirstChild<W.PageMargin>();
                var pageNumber = section.GetFirstChild<W.PageNumberType>();
                return new LayoutSectionSignature
                {
                    Index = index,
                    PageWidthTwips = pageSize?.Width?.Value.ToString(),
                    PageHeightTwips = pageSize?.Height?.Value.ToString(),
                    TopMarginTwips = margin?.Top?.Value.ToString(),
                    BottomMarginTwips = margin?.Bottom?.Value.ToString(),
                    LeftMarginTwips = margin?.Left?.Value.ToString(),
                    RightMarginTwips = margin?.Right?.Value.ToString(),
                    HeaderDistanceTwips = margin?.Header?.Value.ToString(),
                    FooterDistanceTwips = margin?.Footer?.Value.ToString(),
                    PageNumberFormat = pageNumber?.Format?.Value.ToString() ?? "none",
                    PageNumberStart = pageNumber?.Start?.Value
                };
            })
            .ToList() ?? [];
    }

    private static List<LayoutStyleSignature> ExtractStyles(MainDocumentPart mainPart)
    {
        var ids = new HashSet<string>(["ThesisBody", "Heading1", "Heading2", "Heading3", "ThesisBibliography"], StringComparer.Ordinal);
        return mainPart.StyleDefinitionsPart?.Styles?.Elements<W.Style>()
            .Where(style => ids.Contains(style.StyleId?.Value ?? string.Empty))
            .Select(style =>
            {
                var rPr = style.GetFirstChild<W.StyleRunProperties>();
                var pPr = style.GetFirstChild<W.StyleParagraphProperties>();
                var fonts = rPr?.GetFirstChild<W.RunFonts>();
                var spacing = pPr?.GetFirstChild<W.SpacingBetweenLines>();
                var indent = pPr?.GetFirstChild<W.Indentation>();
                return new LayoutStyleSignature
                {
                    StyleId = style.StyleId?.Value ?? string.Empty,
                    EastAsiaFont = fonts?.EastAsia?.Value,
                    LatinFont = fonts?.Ascii?.Value,
                    FontSizeHalfPoints = rPr?.GetFirstChild<W.FontSize>()?.Val?.Value,
                    Bold = (rPr?.GetFirstChild<W.Bold>() is not null).ToString(),
                    LineSpacing = spacing?.Line?.Value,
                    FirstLineIndent = indent?.FirstLine?.Value,
                    HangingIndent = indent?.Hanging?.Value,
                    Alignment = pPr?.GetFirstChild<W.Justification>()?.Val?.Value.ToString(),
                    OutlineLevel = pPr?.GetFirstChild<W.OutlineLevel>()?.Val?.Value.ToString()
                };
            })
            .OrderBy(style => style.StyleId, StringComparer.Ordinal)
            .ToList() ?? [];
    }

    private static List<LayoutTableSignature> ExtractTables(MainDocumentPart mainPart)
    {
        return mainPart.Document.Descendants<W.Table>()
            .Select((table, index) =>
            {
                var props = table.GetFirstChild<W.TableProperties>();
                var width = props?.GetFirstChild<W.TableWidth>();
                var layout = props?.GetFirstChild<W.TableLayout>();
                var gridSpanValues = table.Descendants<W.GridSpan>().Select(span => WordAttribute(span, "val") ?? span.Val?.Value.ToString() ?? "missing").ToList();
                var verticalMergeValues = table.Descendants<W.VerticalMerge>().Select(merge => WordAttribute(merge, "val") ?? "continue").ToList();
                var cellWidths = table.Descendants<W.TableCellWidth>().Select(width => $"{WordAttribute(width, "type") ?? "missing"}:{WordAttribute(width, "w") ?? "missing"}").ToList();
                var cellVerticalAlignments = table.Descendants<W.TableCellVerticalAlignment>().Select(alignment => WordAttribute(alignment, "val") ?? "missing").ToList();
                return new LayoutTableSignature
                {
                    Index = index,
                    LayoutType = layout is null ? null : WordAttribute(layout, "type"),
                    WidthType = width?.Type?.Value.ToString(),
                    Width = width?.Width?.Value,
                    Borders = SummarizeBorders(props?.GetFirstChild<W.TableBorders>()),
                    HasGridSpan = gridSpanValues.Count > 0,
                    GridSpanValues = gridSpanValues,
                    HasVerticalMerge = verticalMergeValues.Count > 0,
                    VerticalMergeValues = verticalMergeValues,
                    HasRepeatHeaderRows = table.Descendants<W.TableHeader>().Any(),
                    RepeatHeaderRowCount = table.Descendants<W.TableHeader>().Count(),
                    HasCantSplitRows = table.Descendants<W.CantSplit>().Any(),
                    CantSplitRowCount = table.Descendants<W.CantSplit>().Count(),
                    CellWidths = cellWidths,
                    CellVerticalAlignments = cellVerticalAlignments,
                    CellBorders = table.Descendants<W.TableCellBorders>().Select(SummarizeCellBorders).ToList()
                };
            })
            .ToList();
    }

    private static List<LayoutFigureSignature> ExtractFigures(MainDocumentPart mainPart)
    {
        var captions = mainPart.Document.Descendants<W.Paragraph>()
            .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == StyleIds.Caption)
            .Select(p => string.Concat(p.Descendants<W.Text>().Select(t => t.Text)))
            .Where(text => text.StartsWith("图", StringComparison.Ordinal))
            .ToList();
        return mainPart.Document.Descendants<W.Drawing>()
            .Select((drawing, index) =>
            {
                var extent = drawing.Descendants<WP.Extent>().FirstOrDefault();
                return new LayoutFigureSignature
                {
                    Index = index,
                    Cx = extent?.Cx?.Value.ToString(),
                    Cy = extent?.Cy?.Value.ToString(),
                    Caption = index < captions.Count ? captions[index] : null
                };
            })
            .ToList();
    }

    private static LayoutEquationSignature ExtractEquations(MainDocumentPart mainPart)
    {
        var equationParagraphs = mainPart.Document.Descendants<W.Paragraph>()
            .Where(p => p.Descendants<M.OfficeMath>().Any() || p.Descendants<M.Paragraph>().Any())
            .ToList();
        return new LayoutEquationSignature
        {
            OmmlCount = mainPart.Document.Descendants<M.OfficeMath>().Count() + mainPart.Document.Descendants<M.Paragraph>().Count(),
            HasNumbering = equationParagraphs.Any(p => p.Elements<W.Run>().SelectMany(r => r.Elements<W.Text>()).Any(t => t.Text.Trim().StartsWith("(", StringComparison.Ordinal) && t.Text.Trim().EndsWith(")", StringComparison.Ordinal))),
            Bookmarks = equationParagraphs.SelectMany(p => p.Descendants<W.BookmarkStart>()).Select(b => b.Name?.Value).Where(v => !string.IsNullOrWhiteSpace(v)).Cast<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList()
        };
    }

    private static LayoutNoteSignature ExtractFootnotes(MainDocumentPart mainPart)
    {
        var ids = mainPart.FootnotesPart?.Footnotes?.Elements<W.Footnote>().Select(n => n.Id?.Value).Where(v => v.HasValue && v.Value > 0).Select(v => v!.Value).Order().ToList() ?? [];
        return new LayoutNoteSignature { HasPart = mainPart.FootnotesPart is not null, Count = ids.Count, Ids = ids };
    }

    private static LayoutNoteSignature ExtractEndnotes(MainDocumentPart mainPart)
    {
        var ids = mainPart.EndnotesPart?.Endnotes?.Elements<W.Endnote>().Select(n => n.Id?.Value).Where(v => v.HasValue && v.Value > 0).Select(v => v!.Value).Order().ToList() ?? [];
        return new LayoutNoteSignature { HasPart = mainPart.EndnotesPart is not null, Count = ids.Count, Ids = ids };
    }

    private static LayoutBibliographySignature ExtractBibliography(MainDocumentPart mainPart)
    {
        var paragraphs = mainPart.Document.Descendants<W.Paragraph>().Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == StyleIds.Bibliography).ToList();
        return new LayoutBibliographySignature
        {
            EntryCount = paragraphs.Count,
            HangingIndents = paragraphs.Select(p => p.ParagraphProperties?.Indentation?.Hanging?.Value).Where(v => !string.IsNullOrWhiteSpace(v)).Cast<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList()
        };
    }

    private static Dictionary<string, string> ExtractCustomProperties(WordprocessingDocument document)
    {
        return document.CustomFilePropertiesPart?.Properties?.Elements<CustomDocumentProperty>()
            .Where(p => !string.IsNullOrWhiteSpace(p.Name?.Value))
            .ToDictionary(p => p.Name!.Value!, p => p.VTLPWSTR?.Text ?? string.Empty, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static List<string> CollectFields(WordprocessingDocument document)
    {
        var fields = new List<string>();
        if (document.MainDocumentPart is not null)
        {
            fields.AddRange(document.MainDocumentPart.Document.Descendants<W.SimpleField>().Select(f => f.Instruction?.Value).Where(v => !string.IsNullOrWhiteSpace(v)).Cast<string>());
            fields.AddRange(document.MainDocumentPart.FooterParts.SelectMany(f => f.Footer.Descendants<W.SimpleField>()).Select(f => f.Instruction?.Value).Where(v => !string.IsNullOrWhiteSpace(v)).Cast<string>());
        }

        return fields;
    }

    private static string SummarizeBorders(W.TableBorders? borders)
    {
        if (borders is null)
        {
            return "missing";
        }

        return string.Join(";", new[]
        {
            $"top={borders.TopBorder?.Val?.Value}:{borders.TopBorder?.Size?.Value}",
            $"bottom={borders.BottomBorder?.Val?.Value}:{borders.BottomBorder?.Size?.Value}",
            $"left={borders.LeftBorder?.Val?.Value}:{borders.LeftBorder?.Size?.Value}",
            $"right={borders.RightBorder?.Val?.Value}:{borders.RightBorder?.Size?.Value}",
            $"insideH={borders.InsideHorizontalBorder?.Val?.Value}:{borders.InsideHorizontalBorder?.Size?.Value}",
            $"insideV={borders.InsideVerticalBorder?.Val?.Value}:{borders.InsideVerticalBorder?.Size?.Value}"
        });
    }

    private static string SummarizeCellBorders(W.TableCellBorders borders)
    {
        return string.Join(";", new[]
        {
            $"top={WordAttribute(borders.TopBorder, "val")}:{WordAttribute(borders.TopBorder, "sz")}:{WordAttribute(borders.TopBorder, "color")}",
            $"bottom={WordAttribute(borders.BottomBorder, "val")}:{WordAttribute(borders.BottomBorder, "sz")}:{WordAttribute(borders.BottomBorder, "color")}",
            $"left={WordAttribute(borders.LeftBorder, "val")}:{WordAttribute(borders.LeftBorder, "sz")}:{WordAttribute(borders.LeftBorder, "color")}",
            $"right={WordAttribute(borders.RightBorder, "val")}:{WordAttribute(borders.RightBorder, "sz")}:{WordAttribute(borders.RightBorder, "color")}"
        });
    }

    private static string? WordAttribute(OpenXmlElement? element, string localName)
    {
        if (element is null)
        {
            return null;
        }

        var attribute = element.GetAttributes()
            .FirstOrDefault(a =>
                string.Equals(a.LocalName, localName, StringComparison.Ordinal)
                && (string.Equals(a.NamespaceUri, WordprocessingNamespace, StringComparison.Ordinal) || string.IsNullOrEmpty(a.NamespaceUri)));
        return string.IsNullOrWhiteSpace(attribute.Value) ? null : attribute.Value;
    }
}
