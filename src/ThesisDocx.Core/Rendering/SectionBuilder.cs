using DocumentFormat.OpenXml;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Utilities;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class SectionBuilder
{
    private readonly ThesisFormatSpec _format;
    private readonly HeaderFooterBuilder _headerFooterBuilder;
    private readonly IReadOnlyDictionary<SectionProfile, PageSetupSpec> _pageSetupOverrides;

    public SectionBuilder(
        ThesisFormatSpec format,
        HeaderFooterBuilder headerFooterBuilder,
        IReadOnlyDictionary<SectionProfile, PageSetupSpec>? pageSetupOverrides = null)
    {
        _format = format;
        _headerFooterBuilder = headerFooterBuilder;
        _pageSetupOverrides = pageSetupOverrides ?? new Dictionary<SectionProfile, PageSetupSpec>();
    }

    public W.Paragraph CreateSectionBreakParagraph(SectionProfile profile, ThesisSection? section = null)
    {
        return new W.Paragraph(new W.ParagraphProperties(CreateSectionProperties(profile, section)));
    }

    public W.SectionProperties CreateSectionProperties(SectionProfile profile, ThesisSection? section = null)
    {
        var sectionFormat = _headerFooterBuilder.GetEffectiveSectionFormat(profile, section);
        var pageSetup = _pageSetupOverrides.TryGetValue(profile, out var overrideSetup)
            ? overrideSetup
            : _format.PageSetup;
        var references = _headerFooterBuilder.EnsureReferences(profile, section);

        var sectionProperties = new W.SectionProperties();
        if (!string.IsNullOrWhiteSpace(references.HeaderId))
        {
            sectionProperties.AppendChild(new W.HeaderReference
            {
                Type = W.HeaderFooterValues.Default,
                Id = references.HeaderId
            });
        }

        if (!string.IsNullOrWhiteSpace(references.EvenHeaderId))
        {
            sectionProperties.AppendChild(new W.HeaderReference
            {
                Type = W.HeaderFooterValues.Even,
                Id = references.EvenHeaderId
            });
        }

        if (!string.IsNullOrWhiteSpace(references.FooterId))
        {
            sectionProperties.AppendChild(new W.FooterReference
            {
                Type = W.HeaderFooterValues.Default,
                Id = references.FooterId
            });
        }

        if (!string.IsNullOrWhiteSpace(references.EvenFooterId))
        {
            sectionProperties.AppendChild(new W.FooterReference
            {
                Type = W.HeaderFooterValues.Even,
                Id = references.EvenFooterId
            });
        }

        sectionProperties.AppendChild(new W.SectionType { Val = W.SectionMarkValues.NextPage });
        sectionProperties.AppendChild(CreatePageSize(pageSetup));
        sectionProperties.AppendChild(new W.PageMargin
        {
            Top = UnitConverter.CentimetersToTwips(pageSetup.TopMarginCm),
            Right = (UInt32Value)(uint)UnitConverter.CentimetersToTwips(pageSetup.RightMarginCm),
            Bottom = UnitConverter.CentimetersToTwips(pageSetup.BottomMarginCm),
            Left = (UInt32Value)(uint)UnitConverter.CentimetersToTwips(pageSetup.LeftMarginCm),
            Header = (UInt32Value)(uint)UnitConverter.CentimetersToTwips(pageSetup.HeaderDistanceCm),
            Footer = (UInt32Value)(uint)UnitConverter.CentimetersToTwips(pageSetup.FooterDistanceCm),
            Gutter = (UInt32Value)(uint)UnitConverter.CentimetersToTwips(pageSetup.GutterCm)
        });

        if (pageSetup.Columns > 1)
        {
            sectionProperties.AppendChild(new W.Columns { ColumnCount = (Int16Value)(short)pageSetup.Columns });
        }

        if (sectionFormat.PageNumberStyle != PageNumberStyle.None)
        {
            sectionProperties.AppendChild(new W.PageNumberType
            {
                Format = ToNumberFormat(sectionFormat.PageNumberStyle),
                Start = sectionFormat.RestartPageNumbering ? sectionFormat.StartPageNumber : null
            });
        }

        if (_format.HeaderFooter.DifferentFirstPage)
        {
            sectionProperties.AppendChild(new W.TitlePage());
        }

        return sectionProperties;
    }

    private static W.PageSize CreatePageSize(PageSetupSpec pageSetup)
    {
        var (width, height) = pageSetup.PaperSize switch
        {
            PaperSizeKind.Letter => (UnitConverter.InchesToTwips(8.5), UnitConverter.InchesToTwips(11)),
            _ => (UnitConverter.MillimetersToTwips(210), UnitConverter.MillimetersToTwips(297))
        };

        if (pageSetup.Orientation == PageOrientationKind.Landscape)
        {
            (width, height) = (height, width);
        }

        return new W.PageSize
        {
            Width = (UInt32Value)(uint)width,
            Height = (UInt32Value)(uint)height,
            Orient = pageSetup.Orientation == PageOrientationKind.Landscape
                ? W.PageOrientationValues.Landscape
                : W.PageOrientationValues.Portrait
        };
    }

    private static W.NumberFormatValues ToNumberFormat(PageNumberStyle style)
    {
        return style switch
        {
            PageNumberStyle.LowerRoman => W.NumberFormatValues.LowerRoman,
            PageNumberStyle.UpperRoman => W.NumberFormatValues.UpperRoman,
            _ => W.NumberFormatValues.Decimal
        };
    }
}
