using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class FootnoteEndnoteTests
{
    [Fact]
    public void RenderFootnotes_ShouldCreateFootnotesPart()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);

        Assert.NotNull(document.MainDocumentPart!.FootnotesPart);
        Assert.Contains(document.MainDocumentPart.FootnotesPart!.Footnotes!.Elements<W.Footnote>(), note => note.Id?.Value == 1);
    }

    [Fact]
    public void RenderFootnotes_ShouldInsertFootnoteReferenceInBody()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.FootnoteReference>(), reference => reference.Id?.Value == 1);
    }

    [Fact]
    public void RenderFootnotes_ShouldContainSeparatorAndContinuationSeparator()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);
        var footnotes = document.MainDocumentPart!.FootnotesPart!.Footnotes!;

        Assert.Contains(footnotes.Elements<W.Footnote>(), note => note.Type?.Value == W.FootnoteEndnoteValues.Separator && note.Id?.Value == -1);
        Assert.Contains(footnotes.Elements<W.Footnote>(), note => note.Type?.Value == W.FootnoteEndnoteValues.ContinuationSeparator && note.Id?.Value == 0);
    }

    [Fact]
    public void RenderEndnotes_ShouldCreateEndnotesPart()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);

        Assert.NotNull(document.MainDocumentPart!.EndnotesPart);
        Assert.Contains(document.MainDocumentPart.EndnotesPart!.Endnotes!.Elements<W.Endnote>(), note => note.Id?.Value == 1);
    }

    [Fact]
    public void RenderEndnotes_ShouldInsertEndnoteReferenceInBody()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.EndnoteReference>(), reference => reference.Id?.Value == 1);
    }

    [Fact]
    public void FormatValidator_ShouldCatchMissingFootnotePart()
    {
        var rendered = TestRenderHelper.RenderFullThesis();
        var copy = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", $"{Guid.NewGuid():N}.docx");
        File.Copy(rendered.DocxPath, copy, overwrite: true);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            document.MainDocumentPart!.DeletePart(document.MainDocumentPart.FootnotesPart!);
        }

        var result = new FormatConformanceValidator().Validate(copy, rendered.Format);

        Assert.Contains(result.Errors, error => error.Code == "notes.footnote.partMissing");
    }

    [Fact]
    public void Inspect_ShouldReportFootnotesAndEndnotes()
    {
        var rendered = TestRenderHelper.RenderFullThesis();

        var result = new DocxInspector().Inspect(rendered.DocxPath);

        Assert.True(result.Footnotes.HasPart);
        Assert.Equal(1, result.Footnotes.Count);
        Assert.True(result.Endnotes.HasPart);
        Assert.Equal(1, result.Endnotes.Count);
    }
}
