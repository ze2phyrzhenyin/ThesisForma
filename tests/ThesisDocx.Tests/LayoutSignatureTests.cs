using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;
using ThesisDocx.Core.Diff.Layout;
using ThesisDocx.Tests.Fixtures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class LayoutSignatureTests
{
    [Fact]
    public void LayoutSignature_ShouldExtractPageSetup()
    {
        var signature = ExtractFull();

        Assert.True(signature.Sections.Count >= 3);
        Assert.Contains(signature.Sections, section => section.TopMarginTwips is not null && section.PageWidthTwips is not null);
    }

    [Fact]
    public void LayoutSignature_ShouldExtractHeadingStyles()
    {
        var signature = ExtractFull();

        Assert.Contains(signature.Styles, style => style.StyleId == "Heading1" && style.OutlineLevel == "0");
        Assert.Contains(signature.Styles, style => style.StyleId == "ThesisBody" && style.LineSpacing is not null);
    }

    [Fact]
    public void LayoutSignature_ShouldExtractTableSummary()
    {
        var signature = ExtractFull();

        Assert.True(signature.Tables.Count > 0);
        Assert.Contains(signature.Tables, table => table.HasGridSpan || table.Borders.Contains("top", StringComparison.Ordinal));
    }

    [Fact]
    public void LayoutSignature_ShouldExtractAdvancedTableDetails()
    {
        var signature = new DocxLayoutSignatureExtractor().Extract(StructuralDocxFixtureFactory.RenderAdvancedTableDocx());
        var table = Assert.Single(signature.Tables);

        Assert.Contains("fixed", table.LayoutType ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(table.WidthType));
        Assert.Contains(table.GridSpanValues, value => value == "2");
        Assert.Contains(table.VerticalMergeValues, value => value.Contains("restart", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(table.VerticalMergeValues, value => value.Contains("continue", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, table.RepeatHeaderRowCount);
        Assert.True(table.CantSplitRowCount >= 1);
        Assert.Contains(table.CellWidths, width => width.Contains("dxa", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(table.CellVerticalAlignments, alignment => alignment.Contains("center", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(table.CellBorders, border => border.Contains("double", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LayoutSignature_ShouldExtractFigureSummary()
    {
        var signature = ExtractFull();

        Assert.True(signature.Figures.Count > 0);
        Assert.Contains(signature.Figures, figure => !string.IsNullOrWhiteSpace(figure.Cx) && !string.IsNullOrWhiteSpace(figure.Cy));
    }

    [Fact]
    public void LayoutSignature_ShouldExtractEquationSummary()
    {
        var signature = ExtractFull();

        Assert.True(signature.Equations.OmmlCount >= 3);
        Assert.True(signature.Equations.HasNumbering);
    }

    [Fact]
    public void LayoutSignature_ShouldExtractFieldSummary()
    {
        var signature = ExtractFull();

        Assert.True(signature.Fields.TocCount >= 1);
        Assert.True(signature.Fields.PageCount >= 1);
        Assert.True(signature.Fields.RefCount >= 1);
    }

    [Fact]
    public void LayoutSignatureComparer_ShouldReturnHighScoreForSameDocx()
    {
        var signature = ExtractFull();

        var result = new LayoutSignatureComparer().Compare(signature, signature, 0.99);

        Assert.True(result.MeetsThreshold);
        Assert.Equal(1.0, result.SimilarityScore);
    }

    [Fact]
    public void LayoutSignatureComparer_ShouldDetectMarginDifference()
    {
        var baseSignature = ExtractFull();
        var changed = MutateFull(document => document.MainDocumentPart!.Document.Descendants<W.PageMargin>().First().Top = 999);
        var targetSignature = new DocxLayoutSignatureExtractor().Extract(changed);

        var result = new LayoutSignatureComparer().Compare(baseSignature, targetSignature, 0.99);

        Assert.Contains(result.BreakingDifferences, diff => diff.Category == "pageSetup");
    }

    [Fact]
    public void LayoutSignatureComparer_ShouldDetectHeadingDifference()
    {
        var baseSignature = ExtractFull();
        var changed = MutateFull(document =>
        {
            var style = document.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<W.Style>().First(s => s.StyleId?.Value == "Heading1");
            style.GetFirstChild<W.StyleRunProperties>()!.GetFirstChild<W.FontSize>()!.Val = "40";
            document.MainDocumentPart.StyleDefinitionsPart.Styles.Save();
        });
        var targetSignature = new DocxLayoutSignatureExtractor().Extract(changed);

        var result = new LayoutSignatureComparer().Compare(baseSignature, targetSignature, 0.99);

        Assert.Contains(result.BreakingDifferences, diff => diff.Category == "headingStyle");
    }

    [Fact]
    public void LayoutSignatureComparer_ShouldApplyThreshold()
    {
        var baseSignature = ExtractFull();
        var changed = MutateFull(document => document.MainDocumentPart!.Document.Descendants<W.PageMargin>().First().Top = 999);
        var targetSignature = new DocxLayoutSignatureExtractor().Extract(changed);

        var result = new LayoutSignatureComparer().Compare(baseSignature, targetSignature, 1.0);

        Assert.False(result.MeetsThreshold);
        Assert.True(result.SimilarityScore < 1.0);
    }

    [Fact]
    public void LayoutSignatureComparer_ShouldDetectNoteChanges()
    {
        var source = StructuralDocxFixtureFactory.RenderNotesDocx();
        var baseSignature = new DocxLayoutSignatureExtractor().Extract(source);
        var changed = Mutate(source, document =>
        {
            document.MainDocumentPart!.FootnotesPart!.Footnotes!.Elements<W.Footnote>().First(note => note.Id?.Value > 0).Remove();
            document.MainDocumentPart.FootnotesPart.Footnotes.Save();
        });
        var targetSignature = new DocxLayoutSignatureExtractor().Extract(changed);

        var result = new LayoutSignatureComparer().Compare(baseSignature, targetSignature, 1.0);

        Assert.Contains(result.BreakingDifferences, diff => diff.Category == "notes");
    }

    [Fact]
    public void LayoutSignatureComparer_ShouldDetectEndnoteChanges()
    {
        var source = StructuralDocxFixtureFactory.RenderNotesDocx();
        var baseSignature = new DocxLayoutSignatureExtractor().Extract(source);
        var changed = Mutate(source, document =>
        {
            document.MainDocumentPart!.EndnotesPart!.Endnotes!.Elements<W.Endnote>().First(note => note.Id?.Value > 0).Remove();
            document.MainDocumentPart.EndnotesPart.Endnotes.Save();
        });
        var targetSignature = new DocxLayoutSignatureExtractor().Extract(changed);

        var result = new LayoutSignatureComparer().Compare(baseSignature, targetSignature, 1.0);

        Assert.Contains(result.BreakingDifferences, diff =>
            diff.Category == "notes"
            && diff.Path.Contains("endnotes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LayoutSignatureComparer_ShouldDetectCustomPropertyChanges()
    {
        var source = StructuralDocxFixtureFactory.RenderCustomPropertiesDocx();
        var baseSignature = new DocxLayoutSignatureExtractor().Extract(source);
        var changed = Mutate(source, document =>
        {
            var property = document.CustomFilePropertiesPart!.Properties!.Elements<DocumentFormat.OpenXml.CustomProperties.CustomDocumentProperty>()
                .First(property => property.Name?.Value == "ThesisDocx.TemplateId");
            property.RemoveAllChildren();
            property.AppendChild(new VTLPWSTR("changed-template"));
            document.CustomFilePropertiesPart.Properties.Save();
        });
        var targetSignature = new DocxLayoutSignatureExtractor().Extract(changed);

        var result = new LayoutSignatureComparer().Compare(baseSignature, targetSignature, 1.0);

        Assert.Contains(result.Differences, diff => diff.Category == "customProperties");
    }

    [Fact]
    public void LayoutSignatureComparer_ShouldDetectRemovedCustomProperty()
    {
        var source = StructuralDocxFixtureFactory.RenderCustomPropertiesDocx();
        var baseSignature = new DocxLayoutSignatureExtractor().Extract(source);
        var changed = Mutate(source, document =>
        {
            var property = document.CustomFilePropertiesPart!.Properties!.Elements<DocumentFormat.OpenXml.CustomProperties.CustomDocumentProperty>()
                .First(property => property.Name?.Value == "ThesisDocx.TemplateId");
            property.Remove();
            document.CustomFilePropertiesPart.Properties.Save();
        });
        var targetSignature = new DocxLayoutSignatureExtractor().Extract(changed);

        var result = new LayoutSignatureComparer().Compare(baseSignature, targetSignature, 1.0);

        Assert.Contains(result.Warnings, diff =>
            diff.Category == "customProperties"
            && diff.Path.Contains("ThesisDocx.TemplateId", StringComparison.Ordinal));
    }

    [Fact]
    public void LayoutSignatureComparer_ShouldDetectAdvancedTableChanges()
    {
        var source = StructuralDocxFixtureFactory.RenderAdvancedTableDocx();
        var baseSignature = new DocxLayoutSignatureExtractor().Extract(source);
        var changed = Mutate(source, document =>
        {
            document.MainDocumentPart!.Document.Descendants<W.GridSpan>().First().Remove();
            document.MainDocumentPart.Document.Save();
        });
        var targetSignature = new DocxLayoutSignatureExtractor().Extract(changed);

        var result = new LayoutSignatureComparer().Compare(baseSignature, targetSignature, 1.0);

        Assert.Contains(result.BreakingDifferences, diff => diff.Category == "table");
    }

    [Fact]
    public void LayoutSignatureComparer_ShouldDetectAdvancedTableVerticalMergeValueChanges()
    {
        var source = StructuralDocxFixtureFactory.RenderAdvancedTableDocx();
        var baseSignature = new DocxLayoutSignatureExtractor().Extract(source);
        var changed = Mutate(source, document =>
        {
            var merge = document.MainDocumentPart!.Document.Descendants<W.VerticalMerge>()
                .First(m => m.Val?.Value == W.MergedCellValues.Restart);
            merge.Val = W.MergedCellValues.Continue;
            document.MainDocumentPart.Document.Save();
        });
        var targetSignature = new DocxLayoutSignatureExtractor().Extract(changed);

        var result = new LayoutSignatureComparer().Compare(baseSignature, targetSignature, 1.0);

        Assert.Contains(result.BreakingDifferences, diff =>
            diff.Category == "table"
            && diff.Path.Contains("verticalMergeValues", StringComparison.OrdinalIgnoreCase));
    }

    private static DocxLayoutSignature ExtractFull()
    {
        return new DocxLayoutSignatureExtractor().Extract(TestRenderHelper.RenderFullThesis().DocxPath);
    }

    private static string MutateFull(Action<WordprocessingDocument> mutate)
    {
        var source = TestRenderHelper.RenderFullThesis().DocxPath;
        return Mutate(source, mutate);
    }

    private static string Mutate(string source, Action<WordprocessingDocument> mutate)
    {
        var copy = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", $"{Guid.NewGuid():N}.docx");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(source, copy);
        using var document = WordprocessingDocument.Open(copy, true);
        mutate(document);
        document.MainDocumentPart?.Document.Save();
        return copy;
    }
}
