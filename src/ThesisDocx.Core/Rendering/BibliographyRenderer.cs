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
                CreateRun(entry.Text));
        }
    }

    private W.Run CreateRun(string text)
    {
        var run = new W.Run();
        var properties = CreateRunProperties();
        if (properties is not null)
        {
            run.AppendChild(properties);
        }

        run.AppendChild(new W.Text(text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
        return run;
    }

    private W.RunProperties? CreateRunProperties()
    {
        if (_format.Bibliography.EntryFont is null)
        {
            return null;
        }

        return new W.RunProperties(
            StyleBuilder.CreateRunFonts(_format.Bibliography.EntryFont),
            new W.FontSize { Val = UnitConverter.PointsToHalfPoints(_format.Bibliography.EntryFont.SizePt).ToString() },
            new W.FontSizeComplexScript { Val = UnitConverter.PointsToHalfPoints(_format.Bibliography.EntryFont.SizePt).ToString() });
    }
}
