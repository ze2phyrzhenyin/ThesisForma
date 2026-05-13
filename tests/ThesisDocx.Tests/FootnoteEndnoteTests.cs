using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Utilities;
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

    [Fact]
    public void RenderNotes_ShouldApplyConfiguredNoteStyles()
    {
        var format = new ThesisFormatSpec
        {
            Notes = new NotesFormatSpec
            {
                Footnote = CreateNoteFormat("CustomFootnoteText", 9, superscript: true),
                Endnote = CreateNoteFormat("CustomEndnoteText", 10, superscript: false)
            }
        };
        var rendered = TestRenderHelper.RenderDocument(CreateNotesDocument(), format);
        var validation = new OpenXmlPackageValidator().Validate(rendered.DocxPath);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));

        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);
        var styles = document.MainDocumentPart!.StyleDefinitionsPart!.Styles!;
        AssertNoteStyle(styles, "CustomFootnoteText", 9);
        AssertNoteStyle(styles, "CustomEndnoteText", 10);

        var footnoteParagraph = document.MainDocumentPart.FootnotesPart!.Footnotes!.Elements<W.Footnote>().Single(note => note.Id?.Value == 1).Elements<W.Paragraph>().Single();
        var endnoteParagraph = document.MainDocumentPart.EndnotesPart!.Endnotes!.Elements<W.Endnote>().Single(note => note.Id?.Value == 1).Elements<W.Paragraph>().Single();
        Assert.Equal("CustomFootnoteText", footnoteParagraph.ParagraphProperties!.ParagraphStyleId!.Val!.Value);
        Assert.Equal("CustomEndnoteText", endnoteParagraph.ParagraphProperties!.ParagraphStyleId!.Val!.Value);

        var endnoteRun = document.MainDocumentPart.Document.Descendants<W.EndnoteReference>().Single(reference => reference.Id?.Value == 1).Ancestors<W.Run>().Single();
        Assert.Null(endnoteRun.RunProperties?.GetFirstChild<W.VerticalTextAlignment>());

        var inspect = new DocxInspector().Inspect(rendered.DocxPath);
        Assert.Contains("CustomFootnoteText", inspect.Footnotes.StyleIds);
        Assert.Contains("CustomEndnoteText", inspect.Endnotes.StyleIds);
        Assert.Equal(1, inspect.Footnotes.ReferenceMarkCount);
        Assert.Equal(1, inspect.Endnotes.ReferenceMarkCount);
    }

    private static NoteFormatSpec CreateNoteFormat(string styleId, double sizePt, bool superscript)
    {
        return new NoteFormatSpec
        {
            StyleId = styleId,
            Font = new FontFormatSpec
            {
                EastAsia = "宋体",
                Latin = "Times New Roman",
                SizePt = sizePt
            },
            Paragraph = new ParagraphFormatSpec
            {
                LineSpacingMultiple = 1.0,
                SpaceBeforePt = 0,
                SpaceAfterPt = 0,
                FirstLineIndentChars = 0,
                HangingIndentCm = 0,
                Alignment = TextAlignment.Both,
                WidowControl = true
            },
            SuperscriptReferenceMark = superscript
        };
    }

    private static void AssertNoteStyle(W.Styles styles, string styleId, double sizePt)
    {
        var style = styles.Elements<W.Style>().Single(style => style.StyleId?.Value == styleId);
        Assert.Equal(UnitConverter.PointsToHalfPoints(sizePt).ToString(), style.GetFirstChild<W.StyleRunProperties>()!.GetFirstChild<W.FontSize>()!.Val!.Value);
        Assert.Equal("240", style.GetFirstChild<W.StyleParagraphProperties>()!.GetFirstChild<W.SpacingBetweenLines>()!.Line!.Value);
    }

    private static ThesisDocument CreateNotesDocument()
    {
        return new ThesisDocument
        {
            Metadata = new ThesisMetadata
            {
                Title = "Notes",
                Author = "测试作者",
                College = "示例学院",
                Major = "软件工程",
                StudentId = "20260001",
                Advisor = "导师",
                Date = "2026-05-12"
            },
            Sections =
            [
                new ThesisSection
                {
                    Kind = ThesisSectionKind.Body,
                    Blocks =
                    [
                        new ParagraphBlock
                        {
                            Inlines =
                            [
                                new TextInline { Text = "脚注" },
                                new FootnoteInline { NoteId = "fn-style", Inlines = [new TextInline { Text = "脚注样式内容" }] },
                                new TextInline { Text = "尾注" },
                                new EndnoteInline { NoteId = "en-style", Inlines = [new TextInline { Text = "尾注样式内容" }] }
                            ]
                        }
                    ]
                }
            ]
        };
    }
}
