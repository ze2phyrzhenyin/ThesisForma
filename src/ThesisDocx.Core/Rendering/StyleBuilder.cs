using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Utilities;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class StyleBuilder
{
    public void Build(MainDocumentPart mainPart, ThesisFormatSpec format)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>("rIdStyles");
        var styles = new W.Styles();

        styles.AppendChild(CreateDocDefaults(format.DefaultFont));
        styles.AppendChild(CreateParagraphStyle(StyleIds.Normal, "Normal", format.DefaultFont, format.BodyParagraph, basedOn: null, isDefault: true));
        styles.AppendChild(CreateParagraphStyle(StyleIds.ThesisBody, "Thesis Body", format.DefaultFont, format.BodyParagraph, StyleIds.Normal));
        styles.AppendChild(CreateHeadingStyle(StyleIds.Heading1, "heading 1", format, 1));
        styles.AppendChild(CreateHeadingStyle(StyleIds.Heading2, "heading 2", format, 2));
        styles.AppendChild(CreateHeadingStyle(StyleIds.Heading3, "heading 3", format, 3));
        styles.AppendChild(CreateParagraphStyle(StyleIds.Caption, "Thesis Caption", format.DefaultFont, new ParagraphFormatSpec
        {
            Alignment = TextAlignment.Center,
            FirstLineIndentChars = 0,
            LineSpacingMultiple = 1.0,
            SpaceBeforePt = 3,
            SpaceAfterPt = 6
        }, StyleIds.Normal));
        styles.AppendChild(CreateParagraphStyle(StyleIds.Bibliography, "Thesis Bibliography", format.DefaultFont, format.Bibliography.EntryParagraph, StyleIds.Normal));
        styles.AppendChild(CreateParagraphStyle(StyleIds.TocTitle, "Thesis TOC Title", new FontFormatSpec
        {
            EastAsia = format.DefaultFont.EastAsia,
            Latin = format.DefaultFont.Latin,
            SizePt = 16,
            Bold = true
        }, new ParagraphFormatSpec
        {
            Alignment = TextAlignment.Center,
            FirstLineIndentChars = 0,
            LineSpacingMultiple = 1.5,
            SpaceAfterPt = 12
        }, StyleIds.Normal));
        styles.AppendChild(CreateParagraphStyle(StyleIds.Quote, "Thesis Quote", format.DefaultFont, new ParagraphFormatSpec
        {
            Alignment = TextAlignment.Both,
            FirstLineIndentChars = 0,
            HangingIndentCm = 0,
            LineSpacingMultiple = 1.0,
            SpaceBeforePt = 6,
            SpaceAfterPt = 6
        }, StyleIds.Normal));

        stylesPart.Styles = styles;
        stylesPart.Styles.Save();
    }

    private static W.DocDefaults CreateDocDefaults(FontFormatSpec font)
    {
        return new W.DocDefaults(
            new W.RunPropertiesDefault(
                new W.RunPropertiesBaseStyle(
                    CreateRunFonts(font),
                    new W.FontSize { Val = UnitConverter.PointsToHalfPoints(font.SizePt).ToString() },
                    new W.FontSizeComplexScript { Val = UnitConverter.PointsToHalfPoints(font.SizePt).ToString() })),
            new W.ParagraphPropertiesDefault(
                new W.ParagraphPropertiesBaseStyle()));
    }

    private static W.Style CreateHeadingStyle(string styleId, string name, ThesisFormatSpec format, int level)
    {
        var heading = format.Headings.TryGetValue(level, out var configured)
            ? configured
            : new HeadingFormatSpec { Level = level, Font = format.DefaultFont, OutlineLevel = level - 1 };

        var paragraphFormat = new ParagraphFormatSpec
        {
            Alignment = heading.Alignment,
            FirstLineIndentChars = 0,
            LineSpacingMultiple = 1.5,
            SpaceBeforePt = heading.SpaceBeforePt,
            SpaceAfterPt = heading.SpaceAfterPt
        };

        var style = CreateParagraphStyle(styleId, name, MergeFont(format.DefaultFont, heading.Font), paragraphFormat, StyleIds.Normal);
        var paragraphProperties = style.GetFirstChild<W.StyleParagraphProperties>();
        style.InsertBefore(new W.NextParagraphStyle { Val = StyleIds.ThesisBody }, paragraphProperties);
        style.InsertBefore(new W.UIPriority { Val = 9 }, paragraphProperties);
        style.InsertBefore(new W.PrimaryStyle(), paragraphProperties);
        style.StyleParagraphProperties ??= new W.StyleParagraphProperties();
        if (heading.PageBreakBefore)
        {
            style.StyleParagraphProperties.InsertAt(new W.PageBreakBefore(), 0);
        }

        style.StyleParagraphProperties.AppendChild(new W.OutlineLevel { Val = level - 1 });
        return style;
    }

    private static W.Style CreateParagraphStyle(
        string styleId,
        string name,
        FontFormatSpec font,
        ParagraphFormatSpec paragraph,
        string? basedOn,
        bool isDefault = false)
    {
        var style = new W.Style
        {
            Type = W.StyleValues.Paragraph,
            StyleId = styleId,
            Default = isDefault ? OnOffValue.FromBoolean(true) : null,
            CustomStyle = isDefault ? null : OnOffValue.FromBoolean(true)
        };

        style.AppendChild(new W.StyleName { Val = name });
        if (!string.IsNullOrWhiteSpace(basedOn))
        {
            style.AppendChild(new W.BasedOn { Val = basedOn });
        }

        var styleParagraphProperties = new W.StyleParagraphProperties();
        styleParagraphProperties.AppendChild(new W.WidowControl { Val = paragraph.WidowControl });
        styleParagraphProperties.AppendChild(new W.SpacingBetweenLines
        {
            Before = UnitConverter.PointsToTwips(paragraph.SpaceBeforePt).ToString(),
            After = UnitConverter.PointsToTwips(paragraph.SpaceAfterPt).ToString(),
            Line = ((int)Math.Round(paragraph.LineSpacingMultiple * 240)).ToString(),
            LineRule = W.LineSpacingRuleValues.Auto
        });

        var indentation = new W.Indentation();
        if (paragraph.FirstLineIndentChars > 0)
        {
            indentation.FirstLine = UnitConverter.PointsToTwips(font.SizePt * paragraph.FirstLineIndentChars).ToString();
        }

        if (paragraph.HangingIndentCm > 0)
        {
            indentation.Hanging = UnitConverter.CentimetersToTwips(paragraph.HangingIndentCm).ToString();
        }

        if (indentation.HasAttributes)
        {
            styleParagraphProperties.AppendChild(indentation);
        }

        styleParagraphProperties.AppendChild(new W.Justification { Val = ToJustification(paragraph.Alignment) });
        style.AppendChild(styleParagraphProperties);

        var runProperties = new W.StyleRunProperties();
        runProperties.AppendChild(CreateRunFonts(font));

        if (font.Bold)
        {
            runProperties.AppendChild(new W.Bold());
            runProperties.AppendChild(new W.BoldComplexScript());
        }

        if (font.Italic)
        {
            runProperties.AppendChild(new W.Italic());
            runProperties.AppendChild(new W.ItalicComplexScript());
        }

        runProperties.AppendChild(new W.FontSize { Val = UnitConverter.PointsToHalfPoints(font.SizePt).ToString() });
        runProperties.AppendChild(new W.FontSizeComplexScript { Val = UnitConverter.PointsToHalfPoints(font.SizePt).ToString() });

        style.AppendChild(runProperties);
        return style;
    }

    private static FontFormatSpec MergeFont(FontFormatSpec defaults, FontFormatSpec overrideFont)
    {
        return new FontFormatSpec
        {
            EastAsia = string.IsNullOrWhiteSpace(overrideFont.EastAsia) ? defaults.EastAsia : overrideFont.EastAsia,
            Latin = string.IsNullOrWhiteSpace(overrideFont.Latin) ? defaults.Latin : overrideFont.Latin,
            SizePt = overrideFont.SizePt <= 0 ? defaults.SizePt : overrideFont.SizePt,
            Bold = overrideFont.Bold,
            Italic = overrideFont.Italic
        };
    }

    internal static W.RunFonts CreateRunFonts(FontFormatSpec font)
    {
        return new W.RunFonts
        {
            Ascii = font.Latin,
            HighAnsi = font.Latin,
            EastAsia = font.EastAsia,
            ComplexScript = font.Latin
        };
    }

    internal static W.JustificationValues ToJustification(TextAlignment alignment)
    {
        return alignment switch
        {
            TextAlignment.Center => W.JustificationValues.Center,
            TextAlignment.Right => W.JustificationValues.Right,
            TextAlignment.Both => W.JustificationValues.Both,
            _ => W.JustificationValues.Left
        };
    }
}
