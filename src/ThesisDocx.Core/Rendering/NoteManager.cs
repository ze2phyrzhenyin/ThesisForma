using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Models;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class NoteManager
{
    private readonly MainDocumentPart _mainPart;
    private readonly Dictionary<string, NoteEntry> _footnotes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NoteEntry> _endnotes = new(StringComparer.Ordinal);
    private int _nextFootnoteId = 1;
    private int _nextEndnoteId = 1;

    public NoteManager(MainDocumentPart mainPart)
    {
        _mainPart = mainPart;
    }

    public W.Run CreateFootnoteReference(string noteId, IReadOnlyList<InlineNode> inlines)
    {
        var numericId = EnsureFootnote(noteId, inlines);
        return new W.Run(
            new W.RunProperties(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript }),
            new W.FootnoteReference { Id = numericId });
    }

    public W.Run CreateEndnoteReference(string noteId, IReadOnlyList<InlineNode> inlines)
    {
        var numericId = EnsureEndnote(noteId, inlines);
        return new W.Run(
            new W.RunProperties(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript }),
            new W.EndnoteReference { Id = numericId });
    }

    public int EnsureFootnote(string noteId, IReadOnlyList<InlineNode> inlines)
    {
        if (_footnotes.TryGetValue(noteId, out var existing))
        {
            return existing.NumericId;
        }

        var entry = new NoteEntry(_nextFootnoteId++, inlines.ToList());
        _footnotes[noteId] = entry;
        return entry.NumericId;
    }

    public int EnsureEndnote(string noteId, IReadOnlyList<InlineNode> inlines)
    {
        if (_endnotes.TryGetValue(noteId, out var existing))
        {
            return existing.NumericId;
        }

        var entry = new NoteEntry(_nextEndnoteId++, inlines.ToList());
        _endnotes[noteId] = entry;
        return entry.NumericId;
    }

    public void SaveParts()
    {
        if (_footnotes.Count > 0)
        {
            var footnotesPart = _mainPart.AddNewPart<FootnotesPart>("rIdFootnotes");
            footnotesPart.Footnotes = new W.Footnotes(
                CreateFootnoteSeparator(),
                CreateFootnoteContinuationSeparator());

            foreach (var entry in _footnotes.OrderBy(kvp => kvp.Value.NumericId))
            {
                footnotesPart.Footnotes.AppendChild(new W.Footnote(
                    CreateNoteParagraph(entry.Value.Inlines, isFootnote: true))
                {
                    Id = entry.Value.NumericId
                });
            }

            footnotesPart.Footnotes.Save();
        }

        if (_endnotes.Count > 0)
        {
            var endnotesPart = _mainPart.AddNewPart<EndnotesPart>("rIdEndnotes");
            endnotesPart.Endnotes = new W.Endnotes(
                CreateEndnoteSeparator(),
                CreateEndnoteContinuationSeparator());

            foreach (var entry in _endnotes.OrderBy(kvp => kvp.Value.NumericId))
            {
                endnotesPart.Endnotes.AppendChild(new W.Endnote(
                    CreateNoteParagraph(entry.Value.Inlines, isFootnote: false))
                {
                    Id = entry.Value.NumericId
                });
            }

            endnotesPart.Endnotes.Save();
        }
    }

    private static W.Footnote CreateFootnoteSeparator()
    {
        return new W.Footnote(new W.Paragraph(new W.Run(new W.SeparatorMark())))
        {
            Type = W.FootnoteEndnoteValues.Separator,
            Id = -1
        };
    }

    private static W.Footnote CreateFootnoteContinuationSeparator()
    {
        return new W.Footnote(new W.Paragraph(new W.Run(new W.ContinuationSeparatorMark())))
        {
            Type = W.FootnoteEndnoteValues.ContinuationSeparator,
            Id = 0
        };
    }

    private static W.Endnote CreateEndnoteSeparator()
    {
        return new W.Endnote(new W.Paragraph(new W.Run(new W.SeparatorMark())))
        {
            Type = W.FootnoteEndnoteValues.Separator,
            Id = -1
        };
    }

    private static W.Endnote CreateEndnoteContinuationSeparator()
    {
        return new W.Endnote(new W.Paragraph(new W.Run(new W.ContinuationSeparatorMark())))
        {
            Type = W.FootnoteEndnoteValues.ContinuationSeparator,
            Id = 0
        };
    }

    private static W.Paragraph CreateNoteParagraph(IReadOnlyList<InlineNode> inlines, bool isFootnote)
    {
        var paragraph = new W.Paragraph();
        paragraph.AppendChild(new W.Run(
            new W.RunProperties(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript }),
            isFootnote ? new W.FootnoteReferenceMark() : new W.EndnoteReferenceMark()));

        foreach (var inline in inlines)
        {
            paragraph.AppendChild(CreateNoteRun(inline));
        }

        return paragraph;
    }

    private static W.Run CreateNoteRun(InlineNode inline)
    {
        return inline switch
        {
            TextInline text => ParagraphRenderer.CreateTextRun(text),
            CitationInline citation => new W.Run(new W.Text(string.IsNullOrWhiteSpace(citation.DisplayText) ? $"[{citation.TargetId}]" : citation.DisplayText)),
            HyperlinkInline hyperlink => new W.Run(new W.Text(hyperlink.Text)),
            ReferenceInline reference => new W.Run(new W.Text(reference.FallbackText ?? reference.BookmarkName)),
            BookmarkInline bookmark => new W.Run(new W.Text(string.Concat(bookmark.Inlines.OfType<TextInline>().Select(t => t.Text)))),
            FootnoteInline footnote => new W.Run(new W.Text($"[{footnote.NoteId}]")),
            EndnoteInline endnote => new W.Run(new W.Text($"[{endnote.NoteId}]")),
            _ => new W.Run(new W.Text(string.Empty))
        };
    }

    private sealed record NoteEntry(int NumericId, List<InlineNode> Inlines);
}
