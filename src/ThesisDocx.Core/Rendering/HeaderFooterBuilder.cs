using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class HeaderFooterBuilder
{
    private readonly MainDocumentPart _mainPart;
    private readonly ThesisFormatSpec _format;
    private readonly DocumentOverrides? _overrides;
    private readonly Dictionary<string, HeaderFooterReferences> _cache = [];

    public HeaderFooterBuilder(MainDocumentPart mainPart, ThesisFormatSpec format, DocumentOverrides? overrides = null)
    {
        _mainPart = mainPart;
        _format = format;
        _overrides = overrides;
    }

    public HeaderFooterReferences EnsureReferences(SectionProfile profile, ThesisSection? section = null)
    {
        var cacheKey = CacheKey(profile, section);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var instance = FindInstance(section);
        var sectionFormat = GetEffectiveSectionFormat(profile, section);
        var headerText = instance?.HeaderText ?? _format.HeaderFooter.HeaderText;
        var footerText = instance?.FooterText;
        string? headerId = null;
        string? footerId = null;
        string? evenHeaderId = null;
        string? evenFooterId = null;

        if (sectionFormat.IncludeHeader && !string.IsNullOrWhiteSpace(headerText))
        {
            headerId = SafeRelationshipId($"rIdHeader{cacheKey}");
            var headerPart = _mainPart.AddNewPart<HeaderPart>(headerId);
            headerPart.Header = CreateHeader(headerText, _format.HeaderFooter.HeaderAlignment);
            headerPart.Header.Save();

            if (_format.HeaderFooter.DifferentOddEven)
            {
                evenHeaderId = SafeRelationshipId($"rIdEvenHeader{cacheKey}");
                var evenHeaderPart = _mainPart.AddNewPart<HeaderPart>(evenHeaderId);
                evenHeaderPart.Header = CreateHeader(headerText, _format.HeaderFooter.HeaderAlignment);
                evenHeaderPart.Header.Save();
            }
        }

        if (sectionFormat.IncludeFooter && sectionFormat.PageNumberStyle != PageNumberStyle.None)
        {
            footerId = SafeRelationshipId($"rIdFooter{cacheKey}");
            var footerPart = _mainPart.AddNewPart<FooterPart>(footerId);
            footerPart.Footer = CreateFooter(footerText, _format.HeaderFooter.DifferentOddEven
                ? _format.HeaderFooter.OddPageNumberAlignment
                : _format.HeaderFooter.PageNumberAlignment);
            footerPart.Footer.Save();

            if (_format.HeaderFooter.DifferentOddEven)
            {
                evenFooterId = SafeRelationshipId($"rIdEvenFooter{cacheKey}");
                var evenFooterPart = _mainPart.AddNewPart<FooterPart>(evenFooterId);
                evenFooterPart.Footer = CreateFooter(footerText, _format.HeaderFooter.EvenPageNumberAlignment);
                evenFooterPart.Footer.Save();
            }
        }

        var refs = new HeaderFooterReferences(headerId, footerId, evenHeaderId, evenFooterId);
        _cache[cacheKey] = refs;
        return refs;
    }

    public SectionFormatSpec GetEffectiveSectionFormat(SectionProfile profile, ThesisSection? section = null)
    {
        var sectionFormat = _format.Sections.TryGetValue(profile.SpecKey(), out var configured)
            ? configured
            : new SectionFormatSpec();
        return new DocumentOverridesFormatMerger().MergeSectionFormat(sectionFormat, FindInstance(section));
    }

    private W.Header CreateHeader(string headerText, TextAlignment alignment)
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

        paragraphProperties.AppendChild(new W.Justification { Val = StyleBuilder.ToJustification(alignment) });

        return new W.Header(new W.Paragraph(
            paragraphProperties,
            new W.Run(new W.Text(headerText))));
    }

    private W.Footer CreateFooter(string? footerText, TextAlignment alignment)
    {
        var paragraph = new W.Paragraph(
                new W.ParagraphProperties(
                    new W.ParagraphStyleId { Val = StyleIds.ThesisBody },
                    new W.Justification { Val = StyleBuilder.ToJustification(alignment) }));
        if (!string.IsNullOrWhiteSpace(footerText))
        {
            paragraph.AppendChild(new W.Run(new W.Text(footerText + " ") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }));
        }

        paragraph.AppendChild(new W.SimpleField
        {
            Instruction = "PAGE \\* MERGEFORMAT"
        });

        return new W.Footer(paragraph);
    }

    private SectionInstanceOverrideSpec? FindInstance(ThesisSection? section)
    {
        if (section?.Id is null || _overrides?.SectionInstances is null)
        {
            return null;
        }

        return _overrides.SectionInstances.TryGetValue(section.Id, out var instance)
            ? instance
            : null;
    }

    private string CacheKey(SectionProfile profile, ThesisSection? section)
    {
        var instance = FindInstance(section);
        return instance is null || string.IsNullOrWhiteSpace(section?.Id)
            ? profile.ToString()
            : $"{profile}_{section.Id}";
    }

    private static string SafeRelationshipId(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }
}

public sealed record HeaderFooterReferences(string? HeaderId, string? FooterId, string? EvenHeaderId, string? EvenFooterId);
