using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class EquationAndAdvancedTableTests
{
    [Fact]
    public void RenderEquation_ShouldCreateOmmlMathElement()
    {
        using var document = OpenFull();

        Assert.True(document.MainDocumentPart!.Document.Descendants<M.OfficeMath>().Count() >= 3);
    }

    [Fact]
    public void RenderEquation_FromPlainText_ShouldCreateOMathRun()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<M.OfficeMath>(),
            math => math.Descendants<M.Run>().Any() && math.Descendants<M.Text>().Any(text => text.Text.Contains("E=mc", StringComparison.Ordinal)));
    }

    [Fact]
    public void RenderEquation_WithNumbering_ShouldRenderEquationNumber()
    {
        using var document = OpenFull();

        var equationParagraphs = document.MainDocumentPart!.Document.Descendants<W.Paragraph>()
            .Where(paragraph => paragraph.Descendants<M.OfficeMath>().Any())
            .Select(paragraph => string.Concat(paragraph.Descendants<W.Text>().Select(text => text.Text)))
            .ToList();

        Assert.Contains(equationParagraphs, text => text.Contains("(3.1)", StringComparison.Ordinal));
        Assert.Contains(equationParagraphs, text => text.Contains("(3.2)", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderEquation_WithBookmark_ShouldCreateBookmarkAroundReferenceTarget()
    {
        using var document = OpenFull();

        var bookmark = Assert.Single(document.MainDocumentPart!.Document.Descendants<W.BookmarkStart>(),
            bookmark => bookmark.Name?.Value == "bm-eq-energy");

        Assert.NotNull(bookmark.Ancestors<W.Paragraph>().Single().Descendants<M.OfficeMath>().FirstOrDefault());
    }

    [Fact]
    public void RenderEquationCrossReference_ShouldCreateRefField()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.SimpleField>(),
            field => field.Instruction?.Value?.Contains("REF bm-eq-energy", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void RenderEquation_ShouldRespectAlignmentAndSpacing()
    {
        using var document = OpenFull();

        var paragraph = document.MainDocumentPart!.Document.Descendants<W.Paragraph>()
            .First(p => p.Descendants<M.OfficeMath>().Any());
        var properties = paragraph.ParagraphProperties!;

        Assert.Equal(W.JustificationValues.Center, properties.Justification!.Val!.Value);
        Assert.Equal("120", properties.SpacingBetweenLines!.Before!.Value);
        Assert.Equal("120", properties.SpacingBetweenLines.After!.Value);
    }

    [Fact]
    public void RenderEquation_FromLatexSubset_ShouldCreateSuperscript()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<M.OfficeMath>(),
            math => math.Descendants<M.Superscript>().Any());
    }

    [Fact]
    public void RenderTable_ShouldRenderGridSpan()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.GridSpan>(), span => span.Val?.Value == 2);
    }

    [Fact]
    public void RenderTable_ShouldRenderVerticalMerge()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.VerticalMerge>(),
            merge => merge.Val?.Value == W.MergedCellValues.Restart);
        Assert.Contains(document.MainDocumentPart.Document.Descendants<W.VerticalMerge>(),
            merge => merge.Val?.Value == W.MergedCellValues.Continue);
    }

    [Fact]
    public void RenderTable_ShouldRenderRepeatHeaderRows()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.TableHeader>(), _ => true);
    }

    [Fact]
    public void RenderTable_ShouldRenderCantSplitRows()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.CantSplit>(), _ => true);
    }

    [Fact]
    public void RenderTable_ShouldRenderFixedLayout()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.TableLayout>(),
            layout => layout.Type?.Value == W.TableLayoutValues.Fixed);
    }

    [Fact]
    public void RenderTable_ShouldRenderPercentWidth()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.TableWidth>(),
            width => width.Type?.Value == W.TableWidthUnitValues.Pct);
    }

    [Fact]
    public void RenderTable_ShouldRenderCellMargins()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.TableCellMarginDefault>(), _ => true);
        Assert.Contains(document.MainDocumentPart.Document.Descendants<W.TableCellMargin>(), _ => true);
    }

    [Fact]
    public void RenderTable_ShouldRenderVerticalAlignment()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.TableCellVerticalAlignment>(),
            align => align.Val?.Value == W.TableVerticalAlignmentValues.Center);
    }

    [Fact]
    public void RenderTable_ShouldRenderCellBorderOverrides()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.TableCellBorders>(),
            borders => borders.BottomBorder?.Val?.Value == W.BorderValues.Double);
    }

    [Fact]
    public void RenderThreeLineTable_ShouldHaveOnlyTopHeaderBottomRules()
    {
        using var document = OpenFull();

        var table = document.MainDocumentPart!.Document.Descendants<W.Table>().First();
        var borders = table.GetFirstChild<W.TableProperties>()!.GetFirstChild<W.TableBorders>()!;

        Assert.Equal(W.BorderValues.Single, borders.TopBorder!.Val!.Value);
        Assert.Equal(W.BorderValues.Single, borders.BottomBorder!.Val!.Value);
        Assert.Equal(W.BorderValues.Nil, borders.LeftBorder!.Val!.Value);
        Assert.Equal(W.BorderValues.Nil, borders.RightBorder!.Val!.Value);
        Assert.Equal(W.BorderValues.Nil, borders.InsideVerticalBorder!.Val!.Value);
    }

    [Fact]
    public void RenderTableCrossReference_ShouldCreateRefField()
    {
        using var document = OpenFull();

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.SimpleField>(),
            field => field.Instruction?.Value?.Contains("REF bm-table-format-rules", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void FormatValidator_ShouldReportMissingEquationBookmark()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        var copy = CopyDocx(rendered.DocxPath);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            foreach (var bookmark in document.MainDocumentPart!.Document.Descendants<W.BookmarkStart>().Where(b => b.Name?.Value == "bm-eq-energy").ToList())
            {
                var id = bookmark.Id?.Value;
                bookmark.Remove();
                foreach (var end in document.MainDocumentPart.Document.Descendants<W.BookmarkEnd>().Where(e => e.Id?.Value == id).ToList())
                {
                    end.Remove();
                }
            }

            document.MainDocumentPart.Document.Save();
        }

        var result = new FormatConformanceValidator().Validate(copy, rendered.Format);

        Assert.Contains(result.Errors, error => error.Code is "equation.referenceBookmark.missing" or "equation.bookmark.missing");
    }

    [Fact]
    public void FormatValidator_ShouldReportMissingEquationOmml()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        var copy = CopyDocx(rendered.DocxPath);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            var paragraph = document.MainDocumentPart!.Document.Descendants<W.BookmarkStart>()
                .Single(b => b.Name?.Value == "bm-eq-energy")
                .Ancestors<W.Paragraph>()
                .Single();
            foreach (var math in paragraph.Descendants<M.OfficeMath>().ToList())
            {
                math.Remove();
            }

            document.MainDocumentPart.Document.Save();
        }

        var result = new FormatConformanceValidator().Validate(copy, rendered.Format);

        Assert.Contains(result.Errors, error => error.Code == "equation.omml.missing");
    }

    [Fact]
    public void FormatValidator_ShouldReportWrongEquationNumberFormat()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        var copy = CopyDocx(rendered.DocxPath);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            var text = document.MainDocumentPart!.Document.Descendants<W.Text>().First(t => t.Text == "(3.1)");
            text.Text = "(3-1)";
            document.MainDocumentPart.Document.Save();
        }

        var result = new FormatConformanceValidator().Validate(copy, rendered.Format);

        Assert.Contains(result.Errors, error => error.Code == "equation.numberFormat.wrong");
    }

    [Fact]
    public void FormatValidator_ShouldReportMissingGridSpan()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        var copy = CopyDocx(rendered.DocxPath);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            document.MainDocumentPart!.Document.Descendants<W.GridSpan>().First().Remove();
            document.MainDocumentPart.Document.Save();
        }

        var result = new FormatConformanceValidator().Validate(copy, rendered.Format);

        Assert.Contains(result.Errors, error => error.Code == "table.gridSpan.missing" || error.Code == "openxml.schema");
    }

    [Fact]
    public void FormatValidator_ShouldReportWrongTableBorder()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        var copy = CopyDocx(rendered.DocxPath);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            var border = document.MainDocumentPart!.Document.Descendants<W.Table>().First()
                .GetFirstChild<W.TableProperties>()!
                .GetFirstChild<W.TableBorders>()!
                .TopBorder!;
            border.Val = W.BorderValues.Double;
            document.MainDocumentPart.Document.Save();
        }

        var result = new FormatConformanceValidator().Validate(copy, rendered.Format);

        Assert.Contains(result.Errors, error => error.Code == "table.threeLine.top");
    }

    [Fact]
    public void FormatValidator_ShouldReportMissingTableCaptionBookmark()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        var copy = CopyDocx(rendered.DocxPath);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            foreach (var bookmark in document.MainDocumentPart!.Document.Descendants<W.BookmarkStart>().Where(b => b.Name?.Value == "bm-table-format-rules").ToList())
            {
                var id = bookmark.Id?.Value;
                bookmark.Remove();
                foreach (var end in document.MainDocumentPart.Document.Descendants<W.BookmarkEnd>().Where(e => e.Id?.Value == id).ToList())
                {
                    end.Remove();
                }
            }

            document.MainDocumentPart.Document.Save();
        }

        var result = new FormatConformanceValidator().Validate(copy, rendered.Format);

        Assert.Contains(result.Errors, error => error.Code == "table.captionBookmark.missing");
    }

    [Fact]
    public void FormatValidator_ShouldReportMissingRepeatHeaderRow()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        var copy = CopyDocx(rendered.DocxPath);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            foreach (var header in document.MainDocumentPart!.Document.Descendants<W.TableHeader>().ToList())
            {
                header.Remove();
            }

            document.MainDocumentPart.Document.Save();
        }

        var result = new FormatConformanceValidator().Validate(copy, rendered.Format);

        Assert.Contains(result.Errors, error => error.Code == "table.repeatHeader.missing");
    }

    [Fact]
    public void Inspect_ShouldReportEquations()
    {
        var rendered = TestRenderHelper.RenderFullThesis();

        var result = new DocxInspector().Inspect(rendered.DocxPath);

        Assert.True(result.Equations.Count >= 3);
        Assert.Contains("bm-eq-energy", result.Equations.Bookmarks);
        Assert.True(result.Equations.OmmlElementCount >= 3);
    }

    [Fact]
    public void Inspect_ShouldReportAdvancedTableSummary()
    {
        var rendered = TestRenderHelper.RenderFullThesis();

        var result = new DocxInspector().Inspect(rendered.DocxPath);

        Assert.True(result.Tables.HasGridSpan);
        Assert.True(result.Tables.HasVerticalMerge);
        Assert.True(result.Tables.HasRepeatHeaderRows);
        Assert.True(result.Tables.HasCantSplitRows);
        Assert.Contains("threeLine", result.Tables.Styles);
    }

    [Fact]
    public void Inspect_ShouldIncludeEquationAndTableReferenceFields()
    {
        var rendered = TestRenderHelper.RenderFullThesis();

        var result = new DocxInspector().Inspect(rendered.DocxPath);

        Assert.Contains(result.RefFields, field => field.Contains("bm-eq-energy", StringComparison.Ordinal));
        Assert.Contains(result.RefFields, field => field.Contains("bm-table-format-rules", StringComparison.Ordinal));
    }

    private static WordprocessingDocument OpenFull()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        return WordprocessingDocument.Open(rendered.DocxPath, false);
    }

    private static string CopyDocx(string source)
    {
        var copy = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", $"{Guid.NewGuid():N}.docx");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(source, copy);
        return copy;
    }
}
