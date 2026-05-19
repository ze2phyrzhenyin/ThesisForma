using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using ThesisDocx.Core.Models;

namespace ThesisDocx.Core.Rendering;

public sealed class SettingsBuilder
{
    public void Build(MainDocumentPart mainPart, ThesisFormatSpec format)
    {
        var settingsPart = mainPart.AddNewPart<DocumentSettingsPart>("rIdSettings");
        settingsPart.Settings = new W.Settings(
            new W.EvenAndOddHeaders { Val = format.HeaderFooter.DifferentOddEven },
            CreateFootnoteProperties(format.Notes.Footnote),
            CreateEndnoteProperties(format.Notes.Endnote),
            new W.Compatibility(
                new W.CompatibilitySetting
                {
                    Name = W.CompatSettingNameValues.CompatibilityMode,
                    Uri = "http://schemas.microsoft.com/office/word",
                    Val = "15"
                }));
        settingsPart.Settings.Save();
    }

    private static W.FootnoteDocumentWideProperties CreateFootnoteProperties(NoteFormatSpec note)
    {
        return new W.FootnoteDocumentWideProperties(
            new W.NumberingFormat { Val = ToNumberFormat(note.NumberFormat) },
            new W.NumberingStart { Val = (UInt16Value)(ushort)note.StartNumber },
            new W.NumberingRestart { Val = ToRestart(note.NumberingRestart) });
    }

    private static W.EndnoteDocumentWideProperties CreateEndnoteProperties(NoteFormatSpec note)
    {
        return new W.EndnoteDocumentWideProperties(
            new W.NumberingFormat { Val = ToNumberFormat(note.NumberFormat) },
            new W.NumberingStart { Val = (UInt16Value)(ushort)note.StartNumber },
            new W.NumberingRestart { Val = ToRestart(note.NumberingRestart) });
    }

    private static W.NumberFormatValues ToNumberFormat(NoteNumberFormat format)
    {
        return format switch
        {
            NoteNumberFormat.DecimalEnclosedCircle => W.NumberFormatValues.DecimalEnclosedCircle,
            NoteNumberFormat.DecimalEnclosedCircleChinese => W.NumberFormatValues.DecimalEnclosedCircleChinese,
            _ => W.NumberFormatValues.Decimal
        };
    }

    private static W.RestartNumberValues ToRestart(NoteNumberingRestart restart)
    {
        return restart switch
        {
            NoteNumberingRestart.EachPage => W.RestartNumberValues.EachPage,
            NoteNumberingRestart.EachSection => W.RestartNumberValues.EachSection,
            _ => W.RestartNumberValues.Continuous
        };
    }
}
