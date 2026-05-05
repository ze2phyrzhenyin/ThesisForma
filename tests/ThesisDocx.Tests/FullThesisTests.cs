using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class FullThesisTests
{
    [Fact]
    public void RenderFullThesis_ShouldProduceValidOpenXml()
    {
        var rendered = TestRenderHelper.RenderFullThesis();

        var result = new OpenXmlPackageValidator().Validate(rendered.DocxPath);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void RenderFullThesis_ShouldPassFormatConformanceValidation()
    {
        var rendered = TestRenderHelper.RenderFullThesis();

        var result = new FormatConformanceValidator().Validate(rendered.DocxPath, rendered.Format);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void RenderFullThesis_ShouldContainExpectedFootnotesAndEndnotes()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);

        Assert.Single(document.MainDocumentPart!.Document.Descendants<W.FootnoteReference>());
        Assert.Single(document.MainDocumentPart.Document.Descendants<W.EndnoteReference>());
        Assert.Equal(3, document.MainDocumentPart.FootnotesPart!.Footnotes!.Elements<W.Footnote>().Count());
        Assert.Equal(3, document.MainDocumentPart.EndnotesPart!.Endnotes!.Elements<W.Endnote>().Count());
    }

    [Fact]
    public void Snapshot_FullThesis_NormalizedDocxXml_ShouldMatchBaseline()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        var snapshot = new DocxSnapshotNormalizer().NormalizeToStableSnapshot(rendered.DocxPath);
        var baselinePath = Path.Combine(rendered.RepoRoot, "tests", "ThesisDocx.Tests", "Snapshots", "full-thesis.snapshot.txt");

        Assert.Equal(File.ReadAllText(baselinePath).Replace("\r\n", "\n", StringComparison.Ordinal), snapshot);
    }
}
