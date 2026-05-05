using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;
using ThesisDocx.Tests.OpenXmlAssertions;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class RenderingTests
{
    [Fact]
    public void RenderSimpleThesis_ShouldProduceValidOpenXml()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();

        var result = new OpenXmlPackageValidator().Validate(rendered.DocxPath);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void RenderSimpleThesis_ShouldContainExpectedStyles()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        using var document = OpenXmlAssert.OpenWordDocument(rendered.DocxPath);

        var bodyStyle = OpenXmlAssert.RequiredStyle(document, StyleIds.ThesisBody);
        var bodyRunProperties = bodyStyle.GetFirstChild<W.StyleRunProperties>();
        Assert.Equal("宋体", bodyRunProperties?.GetFirstChild<W.RunFonts>()?.EastAsia?.Value);
        Assert.Equal("Times New Roman", bodyRunProperties?.GetFirstChild<W.RunFonts>()?.Ascii?.Value);

        Assert.NotNull(OpenXmlAssert.RequiredStyle(document, StyleIds.Heading1));
        Assert.NotNull(OpenXmlAssert.RequiredStyle(document, StyleIds.Heading2));
        Assert.NotNull(OpenXmlAssert.RequiredStyle(document, StyleIds.Heading3));
        Assert.NotNull(OpenXmlAssert.RequiredStyle(document, StyleIds.Caption));
        Assert.NotNull(OpenXmlAssert.RequiredStyle(document, StyleIds.Bibliography));
    }

    [Fact]
    public void RenderSimpleThesis_ShouldContainExpectedSectionPageNumbering()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        using var document = OpenXmlAssert.OpenWordDocument(rendered.DocxPath);

        var sectionProperties = document.MainDocumentPart!.Document.Body!.Descendants<W.SectionProperties>().ToList();

        Assert.Equal(3, sectionProperties.Count);
        Assert.Null(sectionProperties[0].GetFirstChild<W.PageNumberType>());
        Assert.Equal(W.NumberFormatValues.LowerRoman, sectionProperties[1].GetFirstChild<W.PageNumberType>()!.Format!.Value);
        Assert.Equal(1, sectionProperties[1].GetFirstChild<W.PageNumberType>()!.Start!.Value);
        Assert.Equal(W.NumberFormatValues.Decimal, sectionProperties[2].GetFirstChild<W.PageNumberType>()!.Format!.Value);
        Assert.Equal(1, sectionProperties[2].GetFirstChild<W.PageNumberType>()!.Start!.Value);
    }

    [Fact]
    public void RenderSimpleThesis_ShouldContainTocFieldCode()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        using var document = OpenXmlAssert.OpenWordDocument(rendered.DocxPath);

        var field = document.MainDocumentPart!.Document.Descendants<W.SimpleField>()
            .Single(f => f.Instruction?.Value?.StartsWith("TOC", StringComparison.Ordinal) == true);

        Assert.Contains("\\o \"1-3\"", field.Instruction!.Value);
        Assert.Contains("\\h", field.Instruction!.Value);
    }

    [Fact]
    public void RenderSimpleThesis_ShouldRenderHeadingNumbering()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        using var document = OpenXmlAssert.OpenWordDocument(rendered.DocxPath);
        var paragraphs = OpenXmlAssert.Paragraphs(document);

        Assert.Contains(paragraphs, p => HasNumberedStyle(p, StyleIds.Heading1, 0));
        Assert.Contains(paragraphs, p => HasNumberedStyle(p, StyleIds.Heading2, 1));
        Assert.Contains(paragraphs, p => HasNumberedStyle(p, StyleIds.Heading3, 2));

        var levelTexts = document.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!.Descendants<W.LevelText>()
            .Select(t => t.Val?.Value)
            .ToList();
        Assert.Contains("第%1章", levelTexts);
        Assert.Contains("%1.%2", levelTexts);
        Assert.Contains("%1.%2.%3", levelTexts);
    }

    [Fact]
    public void RenderSimpleThesis_ShouldRenderThreeLineTable()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        using var document = OpenXmlAssert.OpenWordDocument(rendered.DocxPath);

        var table = Assert.Single(document.MainDocumentPart!.Document.Body!.Descendants<W.Table>());
        var borders = table.GetFirstChild<W.TableProperties>()!.GetFirstChild<W.TableBorders>()!;

        Assert.Equal(W.BorderValues.Single, borders.TopBorder!.Val!.Value);
        Assert.Equal(W.BorderValues.Single, borders.BottomBorder!.Val!.Value);
        Assert.Equal(W.BorderValues.Single, borders.InsideHorizontalBorder!.Val!.Value);
        Assert.Equal(W.BorderValues.Nil, borders.LeftBorder!.Val!.Value);
        Assert.Equal(W.BorderValues.Nil, borders.RightBorder!.Val!.Value);
    }

    [Fact]
    public void RenderSimpleThesis_ShouldRenderFigureDrawingAndCaption()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        using var document = OpenXmlAssert.OpenWordDocument(rendered.DocxPath);

        var drawing = Assert.Single(document.MainDocumentPart!.Document.Body!.Descendants<W.Drawing>());
        var blip = Assert.Single(drawing.Descendants<A.Blip>());
        Assert.False(string.IsNullOrWhiteSpace(blip.Embed?.Value));

        var captionTexts = OpenXmlAssert.Paragraphs(document).Select(OpenXmlAssert.TextOf).ToList();
        Assert.Contains(captionTexts, text => text.Contains("图1 渲染流程示意图", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderSimpleThesis_ShouldRenderBibliographyWithHangingIndent()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        using var document = OpenXmlAssert.OpenWordDocument(rendered.DocxPath);

        var bibliographyParagraphs = OpenXmlAssert.Paragraphs(document)
            .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == StyleIds.Bibliography)
            .ToList();

        Assert.Equal(2, bibliographyParagraphs.Count);
        foreach (var paragraph in bibliographyParagraphs)
        {
            Assert.Equal(NumberingBuilder.BibliographyNumberingId, paragraph.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value);
            Assert.Equal(UnitConverter.CentimetersToTwips(0.74).ToString(), paragraph.ParagraphProperties!.Indentation!.Hanging!.Value);
        }
    }

    [Fact]
    public void Snapshot_NormalizedDocxXml_ShouldMatchBaseline()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        var snapshot = new DocxSnapshotNormalizer().NormalizeToStableSnapshot(rendered.DocxPath);
        var baselinePath = Path.Combine(rendered.RepoRoot, "tests", "ThesisDocx.Tests", "Snapshots", "simple-thesis.snapshot.txt");

        Assert.Equal(File.ReadAllText(baselinePath).Replace("\r\n", "\n", StringComparison.Ordinal), snapshot);
    }

    private static bool HasNumberedStyle(W.Paragraph paragraph, string styleId, int level)
    {
        var properties = paragraph.ParagraphProperties;
        return properties?.ParagraphStyleId?.Val?.Value == styleId
            && properties.NumberingProperties?.NumberingLevelReference?.Val?.Value == level
            && properties.NumberingProperties?.NumberingId?.Val?.Value == NumberingBuilder.HeadingNumberingId;
    }
}
