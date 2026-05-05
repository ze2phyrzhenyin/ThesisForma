using DocumentFormat.OpenXml;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class ParagraphRenderer
{
    private readonly RelationshipManager _relationshipManager;
    private readonly NoteManager _noteManager;

    public ParagraphRenderer(RelationshipManager relationshipManager, NoteManager noteManager)
    {
        _relationshipManager = relationshipManager;
        _noteManager = noteManager;
    }

    public W.Paragraph CreateParagraph(ParagraphBlock block)
    {
        var paragraph = new W.Paragraph(new W.ParagraphProperties(
            new W.ParagraphStyleId { Val = block.StyleId ?? StyleIds.ThesisBody }));

        if (block.Alignment.HasValue)
        {
            paragraph.ParagraphProperties!.AppendChild(new W.Justification
            {
                Val = StyleBuilder.ToJustification(block.Alignment.Value)
            });
        }

        foreach (var element in CreateInlineElements(block.Inlines))
        {
            paragraph.AppendChild(element);
        }

        return paragraph;
    }

    public W.Paragraph CreatePlainParagraph(string text, string styleId = StyleIds.ThesisBody, TextAlignment? alignment = null)
    {
        var paragraph = new W.Paragraph(new W.ParagraphProperties(
            new W.ParagraphStyleId { Val = styleId }));

        if (alignment.HasValue)
        {
            paragraph.ParagraphProperties!.AppendChild(new W.Justification
            {
                Val = StyleBuilder.ToJustification(alignment.Value)
            });
        }

        paragraph.AppendChild(new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        return paragraph;
    }

    public IEnumerable<OpenXmlElement> CreateInlineElements(IEnumerable<InlineNode> inlines)
    {
        foreach (var inline in inlines)
        {
            foreach (var element in CreateInlineElement(inline))
            {
                yield return element;
            }
        }
    }

    private IEnumerable<OpenXmlElement> CreateInlineElement(InlineNode inline)
    {
        switch (inline)
        {
            case TextInline text:
                yield return CreateTextRun(text);
                break;
            case CitationInline citation:
                yield return new W.Run(new W.Text(string.IsNullOrWhiteSpace(citation.DisplayText)
                    ? $"[{citation.TargetId}]"
                    : citation.DisplayText));
                break;
            case HyperlinkInline hyperlink:
                var relationshipId = _relationshipManager.AddHyperlink(new Uri(hyperlink.Uri));
                var link = new W.Hyperlink
                {
                    Id = relationshipId,
                    History = OnOffValue.FromBoolean(true)
                };
                link.AppendChild(new W.Run(
                    new W.RunProperties(
                        new W.Color { Val = "0563C1" },
                        new W.Underline { Val = W.UnderlineValues.Single }),
                    new W.Text(hyperlink.Text)));
                yield return link;
                break;
            case BookmarkInline bookmark:
                var bookmarkId = _relationshipManager.AllocateBookmarkId().ToString();
                yield return new W.BookmarkStart { Id = bookmarkId, Name = bookmark.Name };
                foreach (var child in CreateInlineElements(bookmark.Inlines))
                {
                    yield return child;
                }

                yield return new W.BookmarkEnd { Id = bookmarkId };
                break;
            case ReferenceInline reference:
                var field = new W.SimpleField
                {
                    Instruction = $"REF {reference.BookmarkName} \\h"
                };
                field.AppendChild(new W.Run(new W.Text(reference.FallbackText ?? reference.BookmarkName)));
                yield return field;
                break;
            case FootnoteInline footnote:
                yield return _noteManager.CreateFootnoteReference(footnote.NoteId, footnote.Inlines);
                break;
            case EndnoteInline endnote:
                yield return _noteManager.CreateEndnoteReference(endnote.NoteId, endnote.Inlines);
                break;
        }
    }

    internal static W.Run CreateTextRun(TextInline text)
    {
        var runProperties = new W.RunProperties();
        if (text.Bold)
        {
            runProperties.AppendChild(new W.Bold());
        }

        if (text.Italic)
        {
            runProperties.AppendChild(new W.Italic());
        }

        if (text.Underline)
        {
            runProperties.AppendChild(new W.Underline { Val = W.UnderlineValues.Single });
        }

        if (text.VerticalAlignment.HasValue && text.VerticalAlignment.Value != VerticalAlignment.Baseline)
        {
            runProperties.AppendChild(new W.VerticalTextAlignment
            {
                Val = text.VerticalAlignment.Value == VerticalAlignment.Subscript
                    ? W.VerticalPositionValues.Subscript
                    : W.VerticalPositionValues.Superscript
            });
        }

        var run = new W.Run();
        if (runProperties.HasChildren)
        {
            run.AppendChild(runProperties);
        }

        run.AppendChild(new W.Text(text.Text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }
}
