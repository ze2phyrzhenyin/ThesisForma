using System.Text.Json;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Api;

public sealed record FormatPreviewRequest(string? TemplateId, DocumentOverrides? Overrides = null);

public sealed record FormatPreviewResponse(
    string DocumentId,
    string TemplateId,
    string TemplateName,
    string FormatSpecName,
    string BaseSchemaVersion,
    string EffectiveSchemaVersion,
    ThesisFormatSpec BaseFormat,
    ThesisFormatSpec EffectiveFormat,
    IReadOnlyList<FormatPreviewChange> Changes,
    IReadOnlyList<SectionFormatPreview> Sections,
    IReadOnlyList<FormatPreviewEvidence> Evidence);

public sealed record FormatPreviewChange(
    string Path,
    string Label,
    string Source,
    string? Before,
    string? After);

public sealed record SectionFormatPreview(
    string SectionId,
    string SectionKind,
    string Title,
    string Bucket,
    SectionFormatSpec BaseFormat,
    SectionFormatSpec EffectiveFormat,
    IReadOnlyList<FormatPreviewChange> Changes);

public sealed record FormatPreviewEvidence(
    string Kind,
    string Path,
    string Message);

public static class FormatPreviewBuilder
{
    public static FormatPreviewResponse Build(
        string documentId,
        ThesisDocument document,
        TemplateResolutionResult resolution,
        DocumentOverrides? overrides)
    {
        var baseFormat = resolution.FormatSpec ?? new ThesisFormatSpec();
        var merger = new DocumentOverridesFormatMerger();
        var effectiveFormat = merger.Merge(baseFormat, overrides);
        var changes = new List<FormatPreviewChange>();

        AddTocChanges(changes, baseFormat, effectiveFormat);
        AddHeaderFooterChanges(changes, baseFormat, effectiveFormat);
        AddFontChanges(changes, "$.defaultFont", "默认字体", "DocumentOverrides.defaultFont", baseFormat.DefaultFont, effectiveFormat.DefaultFont);
        AddParagraphChanges(changes, "$.bodyParagraph", "正文段落", "DocumentOverrides.bodyParagraph", baseFormat.BodyParagraph, effectiveFormat.BodyParagraph);
        AddHeadingChanges(changes, baseFormat, effectiveFormat);
        AddSectionBucketChanges(changes, baseFormat, effectiveFormat);

        var sections = BuildSectionPreviews(document, effectiveFormat, overrides, merger);
        var evidence = BuildEvidence(changes, overrides);

        return new FormatPreviewResponse(
            documentId,
            resolution.Template?.Id ?? string.Empty,
            resolution.Template?.Name ?? string.Empty,
            effectiveFormat.Name,
            baseFormat.SchemaVersion,
            effectiveFormat.SchemaVersion,
            baseFormat,
            effectiveFormat,
            changes,
            sections,
            evidence);
    }

    private static void AddTocChanges(List<FormatPreviewChange> changes, ThesisFormatSpec before, ThesisFormatSpec after)
    {
        AddIfChanged(changes, "$.toc.title", "目录标题", "DocumentOverrides.toc.title", before.Toc.Title, after.Toc.Title);
        AddIfChanged(changes, "$.toc.minLevel", "目录起始层级", "DocumentOverrides.toc.minLevel", before.Toc.MinLevel, after.Toc.MinLevel);
        AddIfChanged(changes, "$.toc.maxLevel", "目录结束层级", "DocumentOverrides.toc.maxLevel", before.Toc.MaxLevel, after.Toc.MaxLevel);
    }

    private static void AddHeaderFooterChanges(List<FormatPreviewChange> changes, ThesisFormatSpec before, ThesisFormatSpec after)
    {
        AddIfChanged(changes, "$.headerFooter.headerText", "页眉文本", "DocumentOverrides.headerFooter.headerText", before.HeaderFooter.HeaderText, after.HeaderFooter.HeaderText);
        AddIfChanged(changes, "$.headerFooter.drawHeaderLine", "页眉横线", "DocumentOverrides.headerFooter.drawHeaderLine", before.HeaderFooter.DrawHeaderLine, after.HeaderFooter.DrawHeaderLine);
        AddIfChanged(changes, "$.headerFooter.hidePageNumberOnCover", "封面隐藏页码", "DocumentOverrides.headerFooter.hidePageNumberOnCover", before.HeaderFooter.HidePageNumberOnCover, after.HeaderFooter.HidePageNumberOnCover);
        AddIfChanged(changes, "$.headerFooter.differentFirstPage", "首页不同页眉页脚", "DocumentOverrides.headerFooter.differentFirstPage", before.HeaderFooter.DifferentFirstPage, after.HeaderFooter.DifferentFirstPage);
    }

    private static void AddHeadingChanges(List<FormatPreviewChange> changes, ThesisFormatSpec before, ThesisFormatSpec after)
    {
        foreach (var level in Enumerable.Range(1, 6))
        {
            before.Headings.TryGetValue(level, out var baseHeading);
            after.Headings.TryGetValue(level, out var effectiveHeading);
            if (baseHeading is null && effectiveHeading is null)
            {
                continue;
            }

            baseHeading ??= new HeadingFormatSpec { Level = level };
            effectiveHeading ??= new HeadingFormatSpec { Level = level };
            var path = $"$.headings[{level}]";
            var source = $"DocumentOverrides.headings.{level}";
            AddFontChanges(changes, $"{path}.font", $"H{level} 字体", $"{source}.font", baseHeading.Font, effectiveHeading.Font);
            AddIfChanged(changes, $"{path}.spaceBeforePt", $"H{level} 段前", $"{source}.spaceBeforePt", baseHeading.SpaceBeforePt, effectiveHeading.SpaceBeforePt);
            AddIfChanged(changes, $"{path}.spaceAfterPt", $"H{level} 段后", $"{source}.spaceAfterPt", baseHeading.SpaceAfterPt, effectiveHeading.SpaceAfterPt);
            AddIfChanged(changes, $"{path}.numbered", $"H{level} 编号", $"{source}.numbered", baseHeading.Numbered, effectiveHeading.Numbered);
            AddIfChanged(changes, $"{path}.pageBreakBefore", $"H{level} 另起一页", $"{source}.pageBreakBefore", baseHeading.PageBreakBefore, effectiveHeading.PageBreakBefore);
            AddIfChanged(changes, $"{path}.outlineLevel", $"H{level} 大纲层级", $"{source}.outlineLevel", baseHeading.OutlineLevel, effectiveHeading.OutlineLevel);
            AddIfChanged(changes, $"{path}.alignment", $"H{level} 对齐", $"{source}.alignment", baseHeading.Alignment, effectiveHeading.Alignment);
        }
    }

    private static void AddSectionBucketChanges(List<FormatPreviewChange> changes, ThesisFormatSpec before, ThesisFormatSpec after)
    {
        foreach (var key in new[] { "cover", "frontMatter", "body" })
        {
            var baseSection = before.Sections.GetValueOrDefault(key) ?? new SectionFormatSpec();
            var effectiveSection = after.Sections.GetValueOrDefault(key) ?? new SectionFormatSpec();
            AddSectionFormatChanges(changes, $"$.sections.{key}", SectionLabel(key), $"DocumentOverrides.sectionFormats.{key}", baseSection, effectiveSection);
        }
    }

    private static IReadOnlyList<SectionFormatPreview> BuildSectionPreviews(
        ThesisDocument document,
        ThesisFormatSpec effectiveFormat,
        DocumentOverrides? overrides,
        DocumentOverridesFormatMerger merger)
    {
        var previews = new List<SectionFormatPreview>();
        foreach (var section in document.Sections)
        {
            var profile = SectionProfileExtensions.FromSectionKind(section.Kind);
            var bucket = profile.SpecKey();
            var baseSection = effectiveFormat.Sections.GetValueOrDefault(bucket) ?? new SectionFormatSpec();
            var instance = !string.IsNullOrWhiteSpace(section.Id) && overrides?.SectionInstances?.TryGetValue(section.Id, out var configured) == true
                ? configured
                : null;
            var effectiveSection = merger.MergeSectionFormat(baseSection, instance);
            var sectionChanges = new List<FormatPreviewChange>();
            AddSectionFormatChanges(
                sectionChanges,
                $"$.sectionInstances.{section.Id ?? bucket}",
                section.Title ?? section.Kind.ToString(),
                $"DocumentOverrides.sectionInstances.{section.Id}",
                baseSection,
                effectiveSection);
            AddSectionInstanceContentChanges(sectionChanges, section, effectiveFormat, instance);

            previews.Add(new SectionFormatPreview(
                section.Id ?? string.Empty,
                section.Kind.ToString(),
                section.Title ?? string.Empty,
                bucket,
                baseSection,
                effectiveSection,
                sectionChanges));
        }

        return previews;
    }

    private static void AddSectionInstanceContentChanges(
        List<FormatPreviewChange> changes,
        ThesisSection section,
        ThesisFormatSpec effectiveFormat,
        SectionInstanceOverrideSpec? instance)
    {
        if (instance is null || string.IsNullOrWhiteSpace(section.Id))
        {
            return;
        }

        var source = $"DocumentOverrides.sectionInstances.{section.Id}";
        if (instance.HeaderText is not null)
        {
            AddExplicitChange(changes, $"$.sectionInstances.{section.Id}.headerText", "本节页眉文本", $"{source}.headerText", effectiveFormat.HeaderFooter.HeaderText, instance.HeaderText);
        }

        if (instance.FooterText is not null)
        {
            AddExplicitChange(changes, $"$.sectionInstances.{section.Id}.footerText", "本节页脚文本", $"{source}.footerText", null, instance.FooterText);
        }

        if (instance.DefaultFont is not null)
        {
            AddFontChanges(changes, $"$.sectionInstances.{section.Id}.defaultFont", "本节默认字体", $"{source}.defaultFont", effectiveFormat.DefaultFont, DocumentOverridesFormatMerger.MergeFont(effectiveFormat.DefaultFont, instance.DefaultFont));
        }

        if (instance.Paragraph is not null)
        {
            AddParagraphChanges(changes, $"$.sectionInstances.{section.Id}.paragraph", "本节段落", $"{source}.paragraph", effectiveFormat.BodyParagraph, DocumentOverridesFormatMerger.MergeParagraph(effectiveFormat.BodyParagraph, instance.Paragraph));
        }
    }

    private static void AddFontChanges(List<FormatPreviewChange> changes, string path, string label, string source, FontFormatSpec before, FontFormatSpec after)
    {
        AddIfChanged(changes, $"{path}.eastAsia", $"{label}中文字体", $"{source}.eastAsia", before.EastAsia, after.EastAsia);
        AddIfChanged(changes, $"{path}.latin", $"{label}西文字体", $"{source}.latin", before.Latin, after.Latin);
        AddIfChanged(changes, $"{path}.sizePt", $"{label}字号", $"{source}.sizePt", before.SizePt, after.SizePt);
        AddIfChanged(changes, $"{path}.bold", $"{label}加粗", $"{source}.bold", before.Bold, after.Bold);
        AddIfChanged(changes, $"{path}.italic", $"{label}斜体", $"{source}.italic", before.Italic, after.Italic);
    }

    private static void AddParagraphChanges(List<FormatPreviewChange> changes, string path, string label, string source, ParagraphFormatSpec before, ParagraphFormatSpec after)
    {
        AddIfChanged(changes, $"{path}.lineSpacingMultiple", $"{label}行距", $"{source}.lineSpacingMultiple", before.LineSpacingMultiple, after.LineSpacingMultiple);
        AddIfChanged(changes, $"{path}.lineSpacingExactPt", $"{label}固定行距", $"{source}.lineSpacingExactPt", before.LineSpacingExactPt, after.LineSpacingExactPt);
        AddIfChanged(changes, $"{path}.spaceBeforePt", $"{label}段前", $"{source}.spaceBeforePt", before.SpaceBeforePt, after.SpaceBeforePt);
        AddIfChanged(changes, $"{path}.spaceAfterPt", $"{label}段后", $"{source}.spaceAfterPt", before.SpaceAfterPt, after.SpaceAfterPt);
        AddIfChanged(changes, $"{path}.firstLineIndentChars", $"{label}首行缩进", $"{source}.firstLineIndentChars", before.FirstLineIndentChars, after.FirstLineIndentChars);
        AddIfChanged(changes, $"{path}.hangingIndentCm", $"{label}悬挂缩进", $"{source}.hangingIndentCm", before.HangingIndentCm, after.HangingIndentCm);
        AddIfChanged(changes, $"{path}.alignment", $"{label}对齐", $"{source}.alignment", before.Alignment, after.Alignment);
        AddIfChanged(changes, $"{path}.widowControl", $"{label}孤行控制", $"{source}.widowControl", before.WidowControl, after.WidowControl);
    }

    private static void AddSectionFormatChanges(List<FormatPreviewChange> changes, string path, string label, string source, SectionFormatSpec before, SectionFormatSpec after)
    {
        AddIfChanged(changes, $"{path}.pageNumberStyle", $"{label}页码样式", $"{source}.pageNumberStyle", before.PageNumberStyle, after.PageNumberStyle);
        AddIfChanged(changes, $"{path}.startPageNumber", $"{label}起始页码", $"{source}.startPageNumber", before.StartPageNumber, after.StartPageNumber);
        AddIfChanged(changes, $"{path}.restartPageNumbering", $"{label}重置页码", $"{source}.restartPageNumbering", before.RestartPageNumbering, after.RestartPageNumbering);
        AddIfChanged(changes, $"{path}.includeHeader", $"{label}显示页眉", $"{source}.includeHeader", before.IncludeHeader, after.IncludeHeader);
        AddIfChanged(changes, $"{path}.includeFooter", $"{label}显示页脚", $"{source}.includeFooter", before.IncludeFooter, after.IncludeFooter);
    }

    private static IReadOnlyList<FormatPreviewEvidence> BuildEvidence(IReadOnlyList<FormatPreviewChange> changes, DocumentOverrides? overrides)
    {
        var evidence = changes
            .Select(change => new FormatPreviewEvidence("formatChange", change.Path, $"{change.Label}: {change.Before ?? "<none>"} -> {change.After ?? "<none>"}"))
            .ToList();

        if (overrides?.Toc?.IncludeSectionIds is { } ids)
        {
            evidence.Add(new FormatPreviewEvidence("tocScope", "$.overrides.toc.includeSectionIds", $"TOC section scope includes {ids.Count} section id(s)."));
        }

        if (overrides is null || JsonSerializer.Serialize(overrides, ThesisJson.Options) == "{}")
        {
            evidence.Add(new FormatPreviewEvidence("noOverrides", "$.overrides", "No DocumentOverrides were supplied; effective format equals the resolved template format."));
        }
        else if (evidence.Count == 0)
        {
            evidence.Add(new FormatPreviewEvidence("noFormatDelta", "$.overrides", "DocumentOverrides were supplied but did not change the resolved format values."));
        }

        return evidence;
    }

    private static void AddIfChanged<T>(List<FormatPreviewChange> changes, string path, string label, string source, T before, T after)
    {
        if (EqualityComparer<T>.Default.Equals(before, after))
        {
            return;
        }

        AddExplicitChange(changes, path, label, source, before, after);
    }

    private static void AddExplicitChange<T>(List<FormatPreviewChange> changes, string path, string label, string source, T before, T after)
    {
        changes.Add(new FormatPreviewChange(path, label, source, ValueText(before), ValueText(after)));
    }

    private static string? ValueText<T>(T value)
    {
        if (value is null)
        {
            return null;
        }

        return value is string text
            ? text
            : JsonSerializer.Serialize(value, ThesisJson.Options);
    }

    private static string SectionLabel(string key)
    {
        return key switch
        {
            "cover" => "封面",
            "frontMatter" => "前置页",
            _ => "正文"
        };
    }
}
