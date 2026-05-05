using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class HeadingRenderer
{
    private readonly RelationshipManager _relationshipManager;
    private readonly ParagraphRenderer _paragraphRenderer;

    public HeadingRenderer(RelationshipManager relationshipManager, ParagraphRenderer paragraphRenderer)
    {
        _relationshipManager = relationshipManager;
        _paragraphRenderer = paragraphRenderer;
    }

    public W.Paragraph CreateHeading(HeadingBlock block)
    {
        var level = Math.Clamp(block.Level, 1, 3);
        var paragraphProperties = new W.ParagraphProperties(
            new W.ParagraphStyleId { Val = ToStyleId(level) });

        if (block.Numbered)
        {
            paragraphProperties.AppendChild(new W.NumberingProperties(
                new W.NumberingLevelReference { Val = level - 1 },
                new W.NumberingId { Val = NumberingBuilder.HeadingNumberingId }));
        }

        paragraphProperties.AppendChild(new W.OutlineLevel { Val = level - 1 });
        var paragraph = new W.Paragraph(paragraphProperties);

        string? bookmarkId = null;
        if (!string.IsNullOrWhiteSpace(block.BookmarkName))
        {
            bookmarkId = _relationshipManager.AllocateBookmarkId().ToString();
            paragraph.AppendChild(new W.BookmarkStart { Id = bookmarkId, Name = block.BookmarkName });
        }

        foreach (var inline in _paragraphRenderer.CreateInlineElements(block.Inlines))
        {
            paragraph.AppendChild(inline);
        }

        if (bookmarkId is not null)
        {
            paragraph.AppendChild(new W.BookmarkEnd { Id = bookmarkId });
        }

        return paragraph;
    }

    private static string ToStyleId(int level)
    {
        return level switch
        {
            1 => StyleIds.Heading1,
            2 => StyleIds.Heading2,
            _ => StyleIds.Heading3
        };
    }
}
