using ThesisDocx.Core.Models;

namespace ThesisDocx.Core.Rendering;

public sealed class DocumentOverridesFormatMerger
{
    public ThesisFormatSpec Merge(ThesisFormatSpec baseSpec, DocumentOverrides? overrides)
    {
        ArgumentNullException.ThrowIfNull(baseSpec);

        var merged = Clone(baseSpec);
        if (overrides is null)
        {
            return merged;
        }

        ApplyToc(merged, overrides.Toc);
        ApplyHeaderFooter(merged, overrides.HeaderFooter);
        ApplyFont(merged.DefaultFont, overrides.DefaultFont);
        ApplyParagraph(merged.BodyParagraph, overrides.BodyParagraph);
        ApplyHeadings(merged, overrides.Headings);
        ApplySectionFormats(merged, overrides.SectionFormats);

        if (overrides.HeaderFooter?.HidePageNumberOnCover == true
            && merged.Sections.TryGetValue(SectionProfile.Cover.SpecKey(), out var cover))
        {
            cover.PageNumberStyle = PageNumberStyle.None;
        }

        return merged;
    }

    public SectionFormatSpec MergeSectionFormat(SectionFormatSpec baseFormat, SectionInstanceOverrideSpec? instance)
    {
        var merged = Clone(baseFormat);
        ApplySectionFormat(merged, instance);
        return merged;
    }

    public static FontFormatSpec MergeFont(FontFormatSpec baseFont, FontOverrideSpec? overrides)
    {
        var merged = Clone(baseFont);
        ApplyFont(merged, overrides);
        return merged;
    }

    public static ParagraphFormatSpec MergeParagraph(ParagraphFormatSpec baseParagraph, ParagraphOverrideSpec? overrides)
    {
        var merged = Clone(baseParagraph);
        ApplyParagraph(merged, overrides);
        return merged;
    }

    private static void ApplyToc(ThesisFormatSpec spec, TocOverrideSpec? toc)
    {
        if (toc is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(toc.Title))
        {
            spec.Toc.Title = toc.Title!;
        }

        if (toc.MinLevel.HasValue)
        {
            spec.Toc.MinLevel = toc.MinLevel.Value;
        }

        if (toc.MaxLevel.HasValue)
        {
            spec.Toc.MaxLevel = toc.MaxLevel.Value;
        }
    }

    private static void ApplyHeaderFooter(ThesisFormatSpec spec, HeaderFooterOverrideSpec? headerFooter)
    {
        if (headerFooter is null)
        {
            return;
        }

        if (headerFooter.HeaderText is not null)
        {
            spec.HeaderFooter.HeaderText = headerFooter.HeaderText;
        }

        if (headerFooter.DrawHeaderLine.HasValue)
        {
            spec.HeaderFooter.DrawHeaderLine = headerFooter.DrawHeaderLine.Value;
        }

        if (headerFooter.HidePageNumberOnCover.HasValue)
        {
            spec.HeaderFooter.HidePageNumberOnCover = headerFooter.HidePageNumberOnCover.Value;
        }

        if (headerFooter.DifferentFirstPage.HasValue)
        {
            spec.HeaderFooter.DifferentFirstPage = headerFooter.DifferentFirstPage.Value;
        }
    }

    private static void ApplyHeadings(ThesisFormatSpec spec, Dictionary<int, HeadingOverrideSpec>? headings)
    {
        if (headings is null)
        {
            return;
        }

        foreach (var (level, overrides) in headings)
        {
            if (level is < 1 or > 6 || overrides is null)
            {
                continue;
            }

            if (!spec.Headings.TryGetValue(level, out var heading))
            {
                heading = new HeadingFormatSpec
                {
                    Level = level,
                    Font = Clone(spec.DefaultFont),
                    OutlineLevel = level - 1
                };
                spec.Headings[level] = heading;
            }

            heading.Level = level;
            heading.Font = MergeFont(heading.Font, overrides.Font);

            if (overrides.SpaceBeforePt.HasValue)
            {
                heading.SpaceBeforePt = overrides.SpaceBeforePt.Value;
            }

            if (overrides.SpaceAfterPt.HasValue)
            {
                heading.SpaceAfterPt = overrides.SpaceAfterPt.Value;
            }

            if (overrides.Numbered.HasValue)
            {
                heading.Numbered = overrides.Numbered.Value;
            }

            if (overrides.PageBreakBefore.HasValue)
            {
                heading.PageBreakBefore = overrides.PageBreakBefore.Value;
            }

            if (overrides.OutlineLevel.HasValue)
            {
                heading.OutlineLevel = overrides.OutlineLevel.Value;
            }

            if (overrides.Alignment.HasValue)
            {
                heading.Alignment = overrides.Alignment.Value;
            }
        }
    }

    private static void ApplySectionFormats(ThesisFormatSpec spec, Dictionary<string, SectionFormatOverrideSpec>? sectionFormats)
    {
        if (sectionFormats is null)
        {
            return;
        }

        foreach (var (key, overrides) in sectionFormats)
        {
            if (!IsSectionBucket(key) || overrides is null)
            {
                continue;
            }

            if (!spec.Sections.TryGetValue(key, out var section))
            {
                section = new SectionFormatSpec();
                spec.Sections[key] = section;
            }

            ApplySectionFormat(section, overrides);
        }
    }

    private static void ApplySectionFormat(SectionFormatSpec section, SectionFormatOverrideSpec? overrides)
    {
        if (overrides is null)
        {
            return;
        }

        if (overrides.PageNumberStyle.HasValue)
        {
            section.PageNumberStyle = overrides.PageNumberStyle.Value;
        }

        if (overrides.StartPageNumber.HasValue)
        {
            section.StartPageNumber = overrides.StartPageNumber.Value;
        }

        if (overrides.RestartPageNumbering.HasValue)
        {
            section.RestartPageNumbering = overrides.RestartPageNumbering.Value;
        }

        if (overrides.IncludeHeader.HasValue)
        {
            section.IncludeHeader = overrides.IncludeHeader.Value;
        }

        if (overrides.IncludeFooter.HasValue)
        {
            section.IncludeFooter = overrides.IncludeFooter.Value;
        }
    }

    private static void ApplyFont(FontFormatSpec font, FontOverrideSpec? overrides)
    {
        if (overrides is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(overrides.EastAsia))
        {
            font.EastAsia = overrides.EastAsia!;
        }

        if (!string.IsNullOrWhiteSpace(overrides.Latin))
        {
            font.Latin = overrides.Latin!;
        }

        if (overrides.SizePt.HasValue)
        {
            font.SizePt = overrides.SizePt.Value;
        }

        if (overrides.Bold.HasValue)
        {
            font.Bold = overrides.Bold.Value;
        }

        if (overrides.Italic.HasValue)
        {
            font.Italic = overrides.Italic.Value;
        }
    }

    private static void ApplyParagraph(ParagraphFormatSpec paragraph, ParagraphOverrideSpec? overrides)
    {
        if (overrides is null)
        {
            return;
        }

        if (overrides.LineSpacingMultiple.HasValue)
        {
            paragraph.LineSpacingMultiple = overrides.LineSpacingMultiple.Value;
            paragraph.LineSpacingExactPt = null;
        }

        if (overrides.LineSpacingExactPt.HasValue)
        {
            paragraph.LineSpacingExactPt = overrides.LineSpacingExactPt.Value;
        }

        if (overrides.SpaceBeforePt.HasValue)
        {
            paragraph.SpaceBeforePt = overrides.SpaceBeforePt.Value;
        }

        if (overrides.SpaceAfterPt.HasValue)
        {
            paragraph.SpaceAfterPt = overrides.SpaceAfterPt.Value;
        }

        if (overrides.FirstLineIndentChars.HasValue)
        {
            paragraph.FirstLineIndentChars = overrides.FirstLineIndentChars.Value;
        }

        if (overrides.HangingIndentCm.HasValue)
        {
            paragraph.HangingIndentCm = overrides.HangingIndentCm.Value;
        }

        if (overrides.Alignment.HasValue)
        {
            paragraph.Alignment = overrides.Alignment.Value;
        }

        if (overrides.WidowControl.HasValue)
        {
            paragraph.WidowControl = overrides.WidowControl.Value;
        }
    }

    private static bool IsSectionBucket(string value)
    {
        return value is "cover" or "frontMatter" or "body";
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, Utilities.ThesisJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, Utilities.ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not clone {typeof(T).Name}.");
    }
}
