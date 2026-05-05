using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Models;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class NumberingBuilder
{
    public const int HeadingNumberingId = 1;
    public const int BibliographyNumberingId = 2;
    public const int OrderedListNumberingId = 3;

    public void Build(MainDocumentPart mainPart, ThesisFormatSpec format)
    {
        var numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>("rIdNumbering");

        numberingPart.Numbering = new W.Numbering(
            CreateHeadingAbstractNum(format),
            CreateBibliographyAbstractNum(format),
            CreateOrderedListAbstractNum(format),
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = HeadingNumberingId },
            new W.NumberingInstance(new W.AbstractNumId { Val = 2 }) { NumberID = BibliographyNumberingId },
            new W.NumberingInstance(new W.AbstractNumId { Val = 3 }) { NumberID = OrderedListNumberingId });

        numberingPart.Numbering.Save();
    }

    private static W.AbstractNum CreateHeadingAbstractNum(ThesisFormatSpec format)
    {
        return new W.AbstractNum(
            new W.MultiLevelType { Val = W.MultiLevelValues.HybridMultilevel },
            CreateLevel(0, format.Numbering.HeadingLevel1Text),
            CreateLevel(1, format.Numbering.HeadingLevel2Text, left: "720", hanging: "360"),
            CreateLevel(2, format.Numbering.HeadingLevel3Text, left: "1080", hanging: "360"))
        {
            AbstractNumberId = 1
        };
    }

    private static W.AbstractNum CreateBibliographyAbstractNum(ThesisFormatSpec format)
    {
        return new W.AbstractNum(
            new W.MultiLevelType { Val = W.MultiLevelValues.SingleLevel },
            CreateLevel(0, format.Numbering.BibliographyText, left: "720", hanging: "360"))
        {
            AbstractNumberId = 2
        };
    }

    private static W.AbstractNum CreateOrderedListAbstractNum(ThesisFormatSpec format)
    {
        return new W.AbstractNum(
            new W.MultiLevelType { Val = W.MultiLevelValues.SingleLevel },
            CreateLevel(0, format.Numbering.OrderedListText, left: "720", hanging: "360"))
        {
            AbstractNumberId = 3
        };
    }

    private static W.Level CreateLevel(int index, string text, string left = "0", string hanging = "0")
    {
        return new W.Level(
            new W.StartNumberingValue { Val = 1 },
            new W.NumberingFormat { Val = W.NumberFormatValues.Decimal },
            new W.LevelText { Val = text },
            new W.LevelJustification { Val = W.LevelJustificationValues.Left },
            new W.PreviousParagraphProperties(
                new W.Indentation { Left = left, Hanging = hanging }))
        {
            LevelIndex = index
        };
    }
}
