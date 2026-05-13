using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class FieldCodeRenderer
{
    private readonly ThesisFormatSpec _format;
    private readonly DocumentOverrides? _overrides;

    public FieldCodeRenderer(ThesisFormatSpec format, DocumentOverrides? overrides = null)
    {
        _format = format;
        _overrides = overrides;
    }

    public IEnumerable<W.Paragraph> CreateToc()
    {
        yield return new W.Paragraph(
            new W.ParagraphProperties(new W.ParagraphStyleId { Val = StyleIds.TocTitle }),
            new W.Run(new W.Text(_format.Toc.Title)));

        var includeSectionIds = _overrides?.Toc?.IncludeSectionIds;
        if (includeSectionIds is { Count: > 0 })
        {
            foreach (var sectionId in includeSectionIds)
            {
                yield return CreateTocFieldParagraph(TocBookmarkName(sectionId));
            }

            yield break;
        }

        yield return CreateTocFieldParagraph();
    }

    public static string TocBookmarkName(string sectionId)
    {
        var safe = new string(sectionId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        return "_TocSec_" + safe;
    }

    private W.Paragraph CreateTocFieldParagraph(string? bookmarkName = null)
    {
        var instruction = bookmarkName is null
            ? $"TOC \\o \"{_format.Toc.MinLevel}-{_format.Toc.MaxLevel}\" \\h \\z \\u"
            : $"TOC \\b {bookmarkName} \\o \"{_format.Toc.MinLevel}-{_format.Toc.MaxLevel}\" \\h \\z \\u";
        var field = new W.SimpleField
        {
            Instruction = instruction
        };
        field.AppendChild(new W.Run(new W.Text("Table of contents field. Update fields in Word to populate entries.")));

        return new W.Paragraph(
            new W.ParagraphProperties(new W.ParagraphStyleId { Val = StyleIds.ThesisBody }),
            field);
    }
}
