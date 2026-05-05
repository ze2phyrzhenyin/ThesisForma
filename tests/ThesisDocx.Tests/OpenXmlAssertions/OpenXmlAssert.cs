using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests.OpenXmlAssertions;

internal static class OpenXmlAssert
{
    public static WordprocessingDocument OpenWordDocument(string path)
    {
        return WordprocessingDocument.Open(path, false);
    }

    public static string TextOf(W.Paragraph paragraph)
    {
        return string.Concat(paragraph.Descendants<W.Text>().Select(t => t.Text));
    }

    public static IReadOnlyList<W.Paragraph> Paragraphs(WordprocessingDocument document)
    {
        return document.MainDocumentPart?.Document.Body?.Descendants<W.Paragraph>().ToList()
            ?? [];
    }

    public static W.Style RequiredStyle(WordprocessingDocument document, string styleId)
    {
        return document.MainDocumentPart?.StyleDefinitionsPart?.Styles?.Elements<W.Style>()
            .SingleOrDefault(s => s.StyleId?.Value == styleId)
            ?? throw new Xunit.Sdk.XunitException($"Missing style '{styleId}'.");
    }
}
