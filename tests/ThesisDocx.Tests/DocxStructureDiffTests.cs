using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Diff;
using ThesisDocx.Tests.Fixtures;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class DocxStructureDiffTests
{
    [Fact]
    public void DocxStructureDiff_ShouldReturnEqualForSameDocx()
    {
        var docx = TestRenderHelper.RenderFullThesis().DocxPath;

        var result = new DocxStructureDiffEngine().Compare(docx, docx);

        Assert.True(result.IsEqual, string.Join(Environment.NewLine, result.Changes.Select(c => c.Path)));
    }

    [Fact]
    public void DocxStructureDiff_ShouldIgnoreVolatileRsid()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        var copy = CopyDocx(rendered.DocxPath);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            var paragraph = document.MainDocumentPart!.Document.Descendants<W.Paragraph>().First();
            paragraph.SetAttribute(new OpenXmlAttribute("w", "rsidR", "http://schemas.openxmlformats.org/wordprocessingml/2006/main", "00ABCDEF"));
            document.MainDocumentPart.Document.Save();
        }

        var result = new DocxStructureDiffEngine().Compare(rendered.DocxPath, copy);

        Assert.True(result.IsEqual, string.Join(Environment.NewLine, result.Changes.Select(c => c.Path)));
    }

    [Fact]
    public void DocxStructureDiff_ShouldDetectChangedPageMargin()
    {
        var copy = MutateFullDocx(document =>
        {
            document.MainDocumentPart!.Document.Descendants<W.PageMargin>().First().Top = 999;
        });

        var result = DiffAgainstFull(copy);

        Assert.Contains(result.Changes, change => change.Category == "pageSetup" && change.Path.Contains(".margins", StringComparison.Ordinal));
    }

    [Fact]
    public void DocxStructureDiff_ShouldDetectChangedHeadingStyle()
    {
        var copy = MutateFullDocx(document =>
        {
            var style = document.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<W.Style>().First(s => s.StyleId?.Value == "Heading1");
            style.GetFirstChild<W.StyleRunProperties>()!.GetFirstChild<W.FontSize>()!.Val = "40";
            document.MainDocumentPart.StyleDefinitionsPart.Styles.Save();
        });

        var result = DiffAgainstFull(copy);

        Assert.Contains(result.Changes, change => change.Category == "headingStyle" && change.PartName == "word/styles.xml");
    }

    [Fact]
    public void DocxStructureDiff_ShouldDetectMissingTocField()
    {
        var copy = MutateFullDocx(document =>
        {
            foreach (var field in document.MainDocumentPart!.Document.Descendants<W.SimpleField>().Where(f => f.Instruction?.Value?.Contains("TOC", StringComparison.OrdinalIgnoreCase) == true).ToList())
            {
                field.Remove();
            }
        });

        var result = DiffAgainstFull(copy);

        Assert.Contains(result.Changes, change => change.Category == "toc");
    }

    [Fact]
    public void DocxStructureDiff_ShouldDetectChangedTableBorders()
    {
        var copy = MutateFullDocx(document =>
        {
            var border = document.MainDocumentPart!.Document.Descendants<W.Table>().First().GetFirstChild<W.TableProperties>()!.GetFirstChild<W.TableBorders>()!.TopBorder!;
            border.Val = W.BorderValues.Double;
        });

        var result = DiffAgainstFull(copy);

        Assert.Contains(result.Changes, change => change.Category == "table" && change.Path.Contains(".borders", StringComparison.Ordinal));
    }

    [Fact]
    public void DocxStructureDiff_ShouldDetectChangedFigureSize()
    {
        var copy = MutateFullDocx(document =>
        {
            document.MainDocumentPart!.Document.Descendants<WP.Extent>().First().Cx = 123456L;
        });

        var result = DiffAgainstFull(copy);

        Assert.Contains(result.Changes, change => change.Category == "figure" && change.Path.Contains(".size", StringComparison.Ordinal));
    }

    [Fact]
    public void DocxStructureDiff_ShouldDetectMissingFootnotesPart()
    {
        var copy = MutateFullDocx(document =>
        {
            if (document.MainDocumentPart!.FootnotesPart is not null)
            {
                document.MainDocumentPart.DeletePart(document.MainDocumentPart.FootnotesPart);
            }
        });

        var result = DiffAgainstFull(copy);

        Assert.Contains(result.Changes, change => change.Category == "notes" && change.ChangeType == DocxStructureDiffChangeType.Removed);
    }

    [Fact]
    public void DocxStructureDiff_ShouldDetectChangedCustomProperties()
    {
        var copy = MutateFullDocx(document =>
        {
            var property = document.CustomFilePropertiesPart!.Properties!.Elements<CustomDocumentProperty>().First(p => p.Name?.Value == "ThesisDocx.RendererVersion");
            property.VTLPWSTR!.Text = "changed";
            document.CustomFilePropertiesPart.Properties.Save();
        });

        var result = DiffAgainstFull(copy);

        Assert.Contains(result.Changes, change => change.Category == "customProperties" && change.Path.Contains("RendererVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void DocxStructureDiff_ShouldProduceDeterministicOrder()
    {
        var copy = MutateFullDocx(document =>
        {
            document.MainDocumentPart!.Document.Descendants<W.PageMargin>().First().Top = 999;
            document.MainDocumentPart.Document.Descendants<WP.Extent>().First().Cx = 123456L;
        });

        var first = DiffAgainstFull(copy).Changes.Select(change => change.Path).ToList();
        var second = DiffAgainstFull(copy).Changes.Select(change => change.Path).ToList();

        Assert.Equal(first, second);
        Assert.Equal(first.OrderBy(path => path, StringComparer.Ordinal), first);
    }

    private static DocxStructureDiffResult DiffAgainstFull(string target)
    {
        return new DocxStructureDiffEngine().Compare(TestRenderHelper.RenderFullThesis().DocxPath, target);
    }

    private static string MutateFullDocx(Action<WordprocessingDocument> mutate)
    {
        var source = TestRenderHelper.RenderFullThesis().DocxPath;
        var copy = CopyDocx(source);
        using var document = WordprocessingDocument.Open(copy, true);
        mutate(document);
        document.MainDocumentPart?.Document.Save();
        return copy;
    }

    private static string CopyDocx(string source)
    {
        var copy = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", $"{Guid.NewGuid():N}.docx");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(source, copy);
        return copy;
    }
}
