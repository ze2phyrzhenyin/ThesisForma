using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class InspectTests
{
    [Fact]
    public void Inspect_ShouldIncludePackageParts()
    {
        var rendered = TestRenderHelper.RenderFullThesis();

        var result = new DocxInspector().Inspect(rendered.DocxPath);

        Assert.Contains("word/document.xml", result.PackageParts);
        Assert.Contains("word/footnotes.xml", result.PackageParts);
        Assert.Contains("word/endnotes.xml", result.PackageParts);
    }

    [Fact]
    public void Inspect_ShouldIncludeStylesAndNumbering()
    {
        var rendered = TestRenderHelper.RenderFullThesis();

        var result = new DocxInspector().Inspect(rendered.DocxPath);

        Assert.Contains(result.StylesSummary, style => style.StyleId == "ThesisBody");
        Assert.Contains(result.NumberingSummary, numbering => numbering.LevelTexts.Contains("第%1章"));
    }

    [Fact]
    public void Inspect_ShouldIncludeSectionsAndPageNumbering()
    {
        var rendered = TestRenderHelper.RenderFullThesis();

        var result = new DocxInspector().Inspect(rendered.DocxPath);

        Assert.Contains(result.SectionsSummary, section => section.PageNumberFormat == "lowerRoman");
        Assert.Contains(result.SectionsSummary, section => section.PageNumberFormat == "decimal");
    }

    [Fact]
    public void Inspect_ShouldIncludeFieldsBookmarksFootnotesEndnotes()
    {
        var rendered = TestRenderHelper.RenderFullThesis();

        var result = new DocxInspector().Inspect(rendered.DocxPath);

        Assert.NotEmpty(result.TocFields);
        Assert.NotEmpty(result.RefFields);
        Assert.Contains("bm-validation", result.Bookmarks);
        Assert.Equal(1, result.Footnotes.Count);
        Assert.Equal(1, result.Endnotes.Count);
    }
}
