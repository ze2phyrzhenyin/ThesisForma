using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class FieldCodeRenderer
{
    private readonly ThesisFormatSpec _format;

    public FieldCodeRenderer(ThesisFormatSpec format)
    {
        _format = format;
    }

    public IEnumerable<W.Paragraph> CreateToc()
    {
        yield return new W.Paragraph(
            new W.ParagraphProperties(new W.ParagraphStyleId { Val = StyleIds.TocTitle }),
            new W.Run(new W.Text(_format.Toc.Title)));

        var field = new W.SimpleField
        {
            Instruction = $"TOC \\o \"{_format.Toc.MinLevel}-{_format.Toc.MaxLevel}\" \\h \\z \\u"
        };
        field.AppendChild(new W.Run(new W.Text("Table of contents field. Update fields in Word to populate entries.")));

        yield return new W.Paragraph(
            new W.ParagraphProperties(new W.ParagraphStyleId { Val = StyleIds.ThesisBody }),
            field);
    }
}
