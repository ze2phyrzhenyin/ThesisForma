using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class HeaderFooterBuilder
{
    private readonly MainDocumentPart _mainPart;
    private readonly ThesisFormatSpec _format;
    private readonly Dictionary<SectionProfile, (string? HeaderId, string? FooterId)> _cache = [];

    public HeaderFooterBuilder(MainDocumentPart mainPart, ThesisFormatSpec format)
    {
        _mainPart = mainPart;
        _format = format;
    }

    public (string? HeaderId, string? FooterId) EnsureReferences(SectionProfile profile)
    {
        if (_cache.TryGetValue(profile, out var cached))
        {
            return cached;
        }

        var sectionFormat = GetSectionFormat(profile);
        string? headerId = null;
        string? footerId = null;

        if (sectionFormat.IncludeHeader && !string.IsNullOrWhiteSpace(_format.HeaderFooter.HeaderText))
        {
            headerId = $"rIdHeader{profile}";
            var headerPart = _mainPart.AddNewPart<HeaderPart>(headerId);
            headerPart.Header = CreateHeader();
            headerPart.Header.Save();
        }

        if (sectionFormat.IncludeFooter && sectionFormat.PageNumberStyle != PageNumberStyle.None)
        {
            footerId = $"rIdFooter{profile}";
            var footerPart = _mainPart.AddNewPart<FooterPart>(footerId);
            footerPart.Footer = CreateFooter();
            footerPart.Footer.Save();
        }

        var refs = (headerId, footerId);
        _cache[profile] = refs;
        return refs;
    }

    private W.Header CreateHeader()
    {
        var paragraphProperties = new W.ParagraphProperties(
            new W.ParagraphStyleId { Val = StyleIds.ThesisBody });

        if (_format.HeaderFooter.DrawHeaderLine)
        {
            paragraphProperties.AppendChild(new W.ParagraphBorders(
                new W.BottomBorder
                {
                    Val = W.BorderValues.Single,
                    Size = 6,
                    Space = 1,
                    Color = "000000"
                }));
        }

        paragraphProperties.AppendChild(new W.Justification { Val = StyleBuilder.ToJustification(_format.HeaderFooter.HeaderAlignment) });

        return new W.Header(new W.Paragraph(
            paragraphProperties,
            new W.Run(new W.Text(_format.HeaderFooter.HeaderText))));
    }

    private W.Footer CreateFooter()
    {
        return new W.Footer(new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphStyleId { Val = StyleIds.ThesisBody },
                new W.Justification { Val = StyleBuilder.ToJustification(_format.HeaderFooter.PageNumberAlignment) }),
            new W.SimpleField
            {
                Instruction = "PAGE \\* MERGEFORMAT"
            }));
    }

    private SectionFormatSpec GetSectionFormat(SectionProfile profile)
    {
        return _format.Sections.TryGetValue(profile.SpecKey(), out var configured)
            ? configured
            : new SectionFormatSpec();
    }
}
