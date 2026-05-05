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
            new W.Compatibility(
                new W.CompatibilitySetting
                {
                    Name = W.CompatSettingNameValues.CompatibilityMode,
                    Uri = "http://schemas.microsoft.com/office/word",
                    Val = "15"
                }));
        settingsPart.Settings.Save();
    }
}
