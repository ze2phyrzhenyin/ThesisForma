using DocumentFormat.OpenXml.Packaging;
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

    private static DocxLayoutSignature ExtractFull()
    {
        return new DocxLayoutSignatureExtractor().Extract(TestRenderHelper.RenderFullThesis().DocxPath);
    }

    private static string MutateFull(Action<WordprocessingDocument> mutate)
    {
        var source = TestRenderHelper.RenderFullThesis().DocxPath;
        var copy = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", $"{Guid.NewGuid():N}.docx");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(source, copy);
        using var document = WordprocessingDocument.Open(copy, true);
        mutate(document);
        document.MainDocumentPart?.Document.Save();
        return copy;
    }
}
