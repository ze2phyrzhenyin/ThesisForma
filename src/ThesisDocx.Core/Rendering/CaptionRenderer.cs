using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class CaptionRenderer
{
    private readonly ThesisFormatSpec _format;
    private int _figureCounter;
    private int _tableCounter;

    public CaptionRenderer(ThesisFormatSpec format)
    {
        _format = format;
    }

    public W.Paragraph CreateFigureCaption(string caption)
    {
        _figureCounter++;
        return CreateCaption(_format.Captions.FigureLabel, _figureCounter, caption);
    }

    public W.Paragraph CreateTableCaption(string caption)
    {
        _tableCounter++;
        return CreateCaption(_format.Captions.TableLabel, _tableCounter, caption);
    }

    private W.Paragraph CreateCaption(string label, int number, string caption)
    {
        var text = _format.Captions.NumberFormat
            .Replace("{label}", label, StringComparison.Ordinal)
            .Replace("{number}", number.ToString(), StringComparison.Ordinal)
            .Replace("{separator}", _format.Captions.Separator, StringComparison.Ordinal)
            .Replace("{text}", caption, StringComparison.Ordinal);

        return new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphStyleId { Val = StyleIds.Caption },
                new W.Justification { Val = W.JustificationValues.Center }),
            new W.Run(new W.Text(text)));
    }
}
