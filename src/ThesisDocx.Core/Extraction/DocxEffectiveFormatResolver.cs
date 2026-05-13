using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Extraction;

internal sealed class DocxEffectiveFormatResolver
{
    private readonly W.Styles? _styles;
    private readonly W.Numbering? _numbering;
    private readonly Dictionary<string, W.Style> _stylesById;
    private readonly Dictionary<string, string> _styleNames;
    private readonly Dictionary<string, string> _numberingToAbstract;
    private readonly Dictionary<string, Dictionary<int, W.Level>> _levelsByAbstract;

    public DocxEffectiveFormatResolver(MainDocumentPart main, Dictionary<string, string> styleNames)
    {
        _styles = main.StyleDefinitionsPart?.Styles;
        _numbering = main.NumberingDefinitionsPart?.Numbering;
        _styleNames = styleNames;
        _stylesById = _styles?.Elements<W.Style>()
            .Where(style => style.StyleId?.Value is not null)
            .ToDictionary(style => style.StyleId!.Value!, StringComparer.Ordinal)
            ?? new Dictionary<string, W.Style>(StringComparer.Ordinal);
        _numberingToAbstract = _numbering?.Elements<W.NumberingInstance>()
            .Where(instance => instance.NumberID?.Value is not null && instance.AbstractNumId?.Val?.Value is not null)
            .ToDictionary(
                instance => instance.NumberID!.Value!.ToString(CultureInfo.InvariantCulture),
                instance => instance.AbstractNumId!.Val!.Value!.ToString(CultureInfo.InvariantCulture),
                StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        _levelsByAbstract = _numbering?.Elements<W.AbstractNum>()
            .Where(abstractNum => abstractNum.AbstractNumberId?.Value is not null)
            .ToDictionary(
                abstractNum => abstractNum.AbstractNumberId!.Value!.ToString(CultureInfo.InvariantCulture),
                abstractNum => abstractNum.Elements<W.Level>()
                    .Where(level => level.LevelIndex?.Value is not null)
                    .GroupBy(level => (int)level.LevelIndex!.Value!)
                    .ToDictionary(group => group.Key, group => group.First()),
                StringComparer.Ordinal)
            ?? new Dictionary<string, Dictionary<int, W.Level>>(StringComparer.Ordinal);
    }

    public ExtractedEffectiveFormat Resolve(W.Paragraph paragraph, ExtractedParagraph extracted)
    {
        var format = new ExtractedEffectiveFormat
        {
            StyleId = extracted.StyleId,
            StyleName = extracted.StyleName,
            OutlineLevel = extracted.OutlineLevel,
            NumberingId = extracted.NumberingId,
            NumberingLevel = extracted.NumberingLevel
        };

        ApplyParagraphProperties(_styles?.DocDefaults?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle, format, "docDefaults.paragraph");
        ApplyRunProperties(_styles?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle, format, "docDefaults.run");

        foreach (var style in ResolveStyleChain(extracted.StyleId))
        {
            var styleId = style.StyleId?.Value;
            if (!string.IsNullOrWhiteSpace(styleId))
            {
                format.StyleChain.Add(styleId!);
            }

            ApplyParagraphProperties(style.GetFirstChild<W.StyleParagraphProperties>(), format, $"style:{styleId}.paragraph");
            ApplyRunProperties(style.GetFirstChild<W.StyleRunProperties>(), format, $"style:{styleId}.run");
        }

        ApplyNumberingLevel(format);

        var paragraphProperties = paragraph.ParagraphProperties;
        if (paragraphProperties is not null)
        {
            format.HasDirectParagraphFormatting = HasDirectParagraphFormatting(paragraphProperties);
            ApplyParagraphProperties(paragraphProperties, format, "paragraph.direct");
        }

        format.HasDirectRunFormatting = paragraph.Elements<W.Run>().Any(run => run.RunProperties?.HasChildren == true);
        ApplySingleRunDirectFormatting(paragraph, format);
        format.Signature = BuildSignature(format);
        return format;
    }

    private IEnumerable<W.Style> ResolveStyleChain(string? styleId)
    {
        var chain = new Stack<W.Style>();
        var currentStyleId = styleId;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrWhiteSpace(currentStyleId)
            && visited.Add(currentStyleId!)
            && _stylesById.TryGetValue(currentStyleId!, out var style))
        {
            chain.Push(style);
            currentStyleId = style.BasedOn?.Val?.Value;
        }

        while (chain.Count > 0)
        {
            yield return chain.Pop();
        }
    }

    private void ApplyNumberingLevel(ExtractedEffectiveFormat format)
    {
        if (string.IsNullOrWhiteSpace(format.NumberingId))
        {
            return;
        }

        if (!_numberingToAbstract.TryGetValue(format.NumberingId!, out var abstractId)
            || !_levelsByAbstract.TryGetValue(abstractId, out var levels))
        {
            return;
        }

        var levelIndex = format.NumberingLevel ?? 0;
        if (!levels.TryGetValue(levelIndex, out var level))
        {
            return;
        }

        format.NumberingFormat = level.NumberingFormat?.Val?.InnerText;
        format.NumberingText = level.LevelText?.Val?.Value;
        AddSource(format, $"numbering:{format.NumberingId}/{levelIndex}");
        ApplyParagraphProperties(level.GetFirstChild<W.PreviousParagraphProperties>(), format, $"numbering:{format.NumberingId}/{levelIndex}.paragraph");
        ApplyRunProperties(level.GetFirstChild<W.RunProperties>(), format, $"numbering:{format.NumberingId}/{levelIndex}.run");
    }

    private static void ApplyParagraphProperties(OpenXmlElement? properties, ExtractedEffectiveFormat format, string source)
    {
        if (properties is null)
        {
            return;
        }

        var changed = false;
        var justification = properties.GetFirstChild<W.Justification>();
        if (justification?.Val?.Value is not null)
        {
            format.Alignment = justification.Val.InnerText;
            changed = true;
        }

        var indentation = properties.GetFirstChild<W.Indentation>();
        if (indentation is not null)
        {
            AssignIfPresent(indentation.Left?.Value, value => format.LeftIndentTwips = value, ref changed);
            AssignIfPresent(indentation.Right?.Value, value => format.RightIndentTwips = value, ref changed);
            AssignIfPresent(indentation.FirstLine?.Value, value => format.FirstLineIndentTwips = value, ref changed);
            AssignIfPresent(indentation.Hanging?.Value, value => format.HangingIndentTwips = value, ref changed);
        }

        var spacing = properties.GetFirstChild<W.SpacingBetweenLines>();
        if (spacing is not null)
        {
            AssignIfPresent(spacing.Before?.Value, value => format.SpaceBeforeTwips = value, ref changed);
            AssignIfPresent(spacing.After?.Value, value => format.SpaceAfterTwips = value, ref changed);
            if (!string.IsNullOrWhiteSpace(spacing.Line?.Value))
            {
                format.LineSpacing = spacing.Line!.Value;
                changed = true;
            }

            if (spacing.LineRule?.Value is not null)
            {
                format.LineSpacingRule = spacing.LineRule.InnerText;
                changed = true;
            }
        }

        var outline = properties.GetFirstChild<W.OutlineLevel>();
        if (outline?.Val?.Value is not null)
        {
            format.OutlineLevel = (int)outline.Val.Value;
            changed = true;
        }

        if (changed)
        {
            AddSource(format, source);
        }
    }

    private static void ApplyRunProperties(OpenXmlElement? properties, ExtractedEffectiveFormat format, string source)
    {
        if (properties is null)
        {
            return;
        }

        var changed = false;
        var runFonts = properties.GetFirstChild<W.RunFonts>();
        if (runFonts is not null)
        {
            if (!string.IsNullOrWhiteSpace(runFonts.Ascii?.Value) || !string.IsNullOrWhiteSpace(runFonts.HighAnsi?.Value))
            {
                format.Font = runFonts.Ascii?.Value ?? runFonts.HighAnsi?.Value;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(runFonts.EastAsia?.Value))
            {
                format.EastAsiaFont = runFonts.EastAsia!.Value;
                changed = true;
            }
        }

        var fontSize = properties.GetFirstChild<W.FontSize>()?.Val?.Value;
        if (TryParseDouble(fontSize, out var halfPoints))
        {
            format.FontSizePt = halfPoints / 2.0;
            changed = true;
        }

        var bold = properties.GetFirstChild<W.Bold>();
        if (bold is not null)
        {
            format.Bold = bold.Val?.Value ?? true;
            changed = true;
        }

        var italic = properties.GetFirstChild<W.Italic>();
        if (italic is not null)
        {
            format.Italic = italic.Val?.Value ?? true;
            changed = true;
        }

        if (changed)
        {
            AddSource(format, source);
        }
    }

    private static void ApplySingleRunDirectFormatting(W.Paragraph paragraph, ExtractedEffectiveFormat format)
    {
        var textRuns = paragraph.Elements<W.Run>()
            .Where(run => !string.IsNullOrWhiteSpace(string.Concat(run.Descendants<W.Text>().Select(text => text.Text))))
            .ToList();
        if (textRuns.Count == 1)
        {
            ApplyRunProperties(textRuns[0].RunProperties, format, "run.direct");
        }
    }

    private static bool HasDirectParagraphFormatting(W.ParagraphProperties properties)
    {
        return properties.ChildElements.Any(child => child is not W.ParagraphStyleId and not W.NumberingProperties);
    }

    private static void AssignIfPresent(string? value, Action<int> assign, ref bool changed)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            assign(parsed);
            changed = true;
        }
    }

    private static bool TryParseDouble(string? value, out double parsed)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
    }

    private static void AddSource(ExtractedEffectiveFormat format, string source)
    {
        if (!string.IsNullOrWhiteSpace(source) && !format.Sources.Contains(source, StringComparer.Ordinal))
        {
            format.Sources.Add(source);
        }
    }

    private static string BuildSignature(ExtractedEffectiveFormat format)
    {
        var parts = new[]
        {
            ("style", format.StyleId),
            ("font", format.Font),
            ("eastAsia", format.EastAsiaFont),
            ("size", format.FontSizePt?.ToString("0.###", CultureInfo.InvariantCulture)),
            ("bold", format.Bold?.ToString(CultureInfo.InvariantCulture)),
            ("italic", format.Italic?.ToString(CultureInfo.InvariantCulture)),
            ("align", format.Alignment),
            ("left", format.LeftIndentTwips?.ToString(CultureInfo.InvariantCulture)),
            ("right", format.RightIndentTwips?.ToString(CultureInfo.InvariantCulture)),
            ("firstLine", format.FirstLineIndentTwips?.ToString(CultureInfo.InvariantCulture)),
            ("hanging", format.HangingIndentTwips?.ToString(CultureInfo.InvariantCulture)),
            ("before", format.SpaceBeforeTwips?.ToString(CultureInfo.InvariantCulture)),
            ("after", format.SpaceAfterTwips?.ToString(CultureInfo.InvariantCulture)),
            ("line", format.LineSpacing),
            ("lineRule", format.LineSpacingRule),
            ("outline", format.OutlineLevel?.ToString(CultureInfo.InvariantCulture)),
            ("numFmt", format.NumberingFormat),
            ("numText", format.NumberingText)
        };

        return string.Join("|", parts.Select(part => $"{part.Item1}={part.Item2 ?? string.Empty}"));
    }

}
