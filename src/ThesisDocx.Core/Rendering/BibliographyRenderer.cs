using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Utilities;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class BibliographyRenderer
{
    private readonly ThesisFormatSpec _format;

    public BibliographyRenderer(ThesisFormatSpec format)
    {
        _format = format;
    }

    public IEnumerable<W.Paragraph> Render(BibliographyBlock block)
    {
        foreach (var entry in block.Entries)
        {
            yield return new W.Paragraph(
                new W.ParagraphProperties(
                    new W.ParagraphStyleId { Val = StyleIds.Bibliography },
                    new W.NumberingProperties(
                        new W.NumberingLevelReference { Val = 0 },
                        new W.NumberingId { Val = NumberingBuilder.BibliographyNumberingId }),
                    new W.Indentation
                    {
                        Hanging = UnitConverter.CentimetersToTwips(_format.Bibliography.EntryParagraph.HangingIndentCm).ToString()
                    }),
                new W.Run(new W.Text(entry.Text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }));
        }
    }
}
