using System.Text.RegularExpressions;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models;

namespace ThesisDocx.Core.Validation;

public sealed partial class DocumentOverridesValidator
{
    public IReadOnlyList<UnifiedDiagnostic> Validate(DocumentOverrides? overrides)
    {
        var diagnostics = new List<UnifiedDiagnostic>();
        if (overrides is null)
        {
            return diagnostics;
        }

        ValidateToc(overrides.Toc, diagnostics);
        ValidateFont(overrides.DefaultFont, "$.defaultFont", diagnostics);
        ValidateParagraph(overrides.BodyParagraph, "$.bodyParagraph", diagnostics);
        ValidateHeadings(overrides.Headings, diagnostics);
        ValidateSectionFormats(overrides.SectionFormats, diagnostics);
        ValidateSectionInstances(overrides.SectionInstances, diagnostics);

        return diagnostics;
    }

    private static void ValidateToc(TocOverrideSpec? toc, List<UnifiedDiagnostic> diagnostics)
    {
        if (toc is null)
        {
            return;
        }

        CheckRange(toc.MinLevel, 1, 6, "$.toc.minLevel", "overrides.toc.minLevel.range", "TOC minLevel must be between 1 and 6.", diagnostics);
        CheckRange(toc.MaxLevel, 1, 6, "$.toc.maxLevel", "overrides.toc.maxLevel.range", "TOC maxLevel must be between 1 and 6.", diagnostics);
        if (toc.MinLevel.HasValue && toc.MaxLevel.HasValue && toc.MinLevel.Value > toc.MaxLevel.Value)
        {
            Add(diagnostics, "overrides.toc.levelRange", "$.toc.maxLevel", "TOC maxLevel must be greater than or equal to minLevel.");
        }

        if (toc.Title is { Length: 0 })
        {
            Add(diagnostics, "overrides.toc.title.empty", "$.toc.title", "TOC title override must not be empty.");
        }

        if (toc.IncludeSectionIds is not null)
        {
            for (var i = 0; i < toc.IncludeSectionIds.Count; i++)
            {
                if (!SafeIdRegex().IsMatch(toc.IncludeSectionIds[i]))
                {
                    Add(diagnostics, "overrides.toc.includeSectionIds.invalid", $"$.toc.includeSectionIds[{i}]", "TOC includeSectionIds values must be safe section ids.");
                }
            }
        }
    }

    private static void ValidateHeadings(Dictionary<int, HeadingOverrideSpec>? headings, List<UnifiedDiagnostic> diagnostics)
    {
        if (headings is null)
        {
            return;
        }

        foreach (var (level, heading) in headings)
        {
            var path = $"$.headings.{level}";
            CheckRange(level, 1, 6, path, "overrides.heading.level.range", "Heading override keys must be between 1 and 6.", diagnostics);
            ValidateFont(heading.Font, $"{path}.font", diagnostics);
            CheckRange(heading.SpaceBeforePt, 0, 72, $"{path}.spaceBeforePt", "overrides.heading.spaceBefore.range", "Heading spaceBeforePt must be between 0 and 72.", diagnostics);
            CheckRange(heading.SpaceAfterPt, 0, 72, $"{path}.spaceAfterPt", "overrides.heading.spaceAfter.range", "Heading spaceAfterPt must be between 0 and 72.", diagnostics);
            CheckRange(heading.OutlineLevel, 0, 8, $"{path}.outlineLevel", "overrides.heading.outlineLevel.range", "Heading outlineLevel must be between 0 and 8.", diagnostics);
        }
    }

    private static void ValidateSectionFormats(Dictionary<string, SectionFormatOverrideSpec>? formats, List<UnifiedDiagnostic> diagnostics)
    {
        if (formats is null)
        {
            return;
        }

        foreach (var (key, format) in formats)
        {
            if (key is not ("cover" or "frontMatter" or "body"))
            {
                Add(diagnostics, "overrides.sectionFormats.key.invalid", $"$.sectionFormats.{key}", "Section format override key must be cover, frontMatter, or body.");
            }

            ValidateSectionFormat(format, $"$.sectionFormats.{key}", diagnostics);
        }
    }

    private static void ValidateSectionInstances(Dictionary<string, SectionInstanceOverrideSpec>? instances, List<UnifiedDiagnostic> diagnostics)
    {
        if (instances is null)
        {
            return;
        }

        foreach (var (sectionId, instance) in instances)
        {
            var path = $"$.sectionInstances.{sectionId}";
            if (!SafeIdRegex().IsMatch(sectionId))
            {
                Add(diagnostics, "overrides.sectionInstances.key.invalid", path, "Section instance override key must be a safe section id.");
            }

            ValidateSectionFormat(instance, path, diagnostics);
            ValidateFont(instance.TitleFont, $"{path}.titleFont", diagnostics);
            ValidateParagraph(instance.TitleParagraph, $"{path}.titleParagraph", diagnostics);
            ValidateFont(instance.DefaultFont, $"{path}.defaultFont", diagnostics);
            ValidateParagraph(instance.Paragraph, $"{path}.paragraph", diagnostics);
            ValidateBlockOverrides(instance.BlockOverrides, $"{path}.blockOverrides", diagnostics);
        }
    }

    private static void ValidateBlockOverrides(Dictionary<int, BlockFormatOverrideSpec>? blocks, string path, List<UnifiedDiagnostic> diagnostics)
    {
        if (blocks is null)
        {
            return;
        }

        foreach (var (index, block) in blocks)
        {
            var blockPath = $"{path}.{index}";
            CheckRange(index, 0, 9999, blockPath, "overrides.block.index.range", "Block override index must be non-negative.", diagnostics);
            ValidateFont(block.Font, $"{blockPath}.font", diagnostics);
            ValidateParagraph(block.Paragraph, $"{blockPath}.paragraph", diagnostics);
        }
    }

    private static void ValidateSectionFormat(SectionFormatOverrideSpec? format, string path, List<UnifiedDiagnostic> diagnostics)
    {
        if (format is null)
        {
            return;
        }

        CheckRange(format.StartPageNumber, 1, 999, $"{path}.startPageNumber", "overrides.section.startPageNumber.range", "Section startPageNumber must be between 1 and 999.", diagnostics);
    }

    private static void ValidateFont(FontOverrideSpec? font, string path, List<UnifiedDiagnostic> diagnostics)
    {
        if (font is null)
        {
            return;
        }

        if (font.EastAsia is { Length: 0 })
        {
            Add(diagnostics, "overrides.font.eastAsia.empty", $"{path}.eastAsia", "East Asian font override must not be empty.");
        }

        if (font.Latin is { Length: 0 })
        {
            Add(diagnostics, "overrides.font.latin.empty", $"{path}.latin", "Latin font override must not be empty.");
        }

        CheckRange(font.SizePt, 1, 72, $"{path}.sizePt", "overrides.font.size.range", "Font sizePt must be between 1 and 72.", diagnostics);
    }

    private static void ValidateParagraph(ParagraphOverrideSpec? paragraph, string path, List<UnifiedDiagnostic> diagnostics)
    {
        if (paragraph is null)
        {
            return;
        }

        CheckRange(paragraph.LineSpacingMultiple, 0.5, 4, $"{path}.lineSpacingMultiple", "overrides.paragraph.lineSpacing.range", "Paragraph lineSpacingMultiple must be between 0.5 and 4.", diagnostics);
        CheckRange(paragraph.LineSpacingExactPt, 1, 120, $"{path}.lineSpacingExactPt", "overrides.paragraph.lineSpacingExact.range", "Paragraph lineSpacingExactPt must be between 1 and 120.", diagnostics);
        CheckRange(paragraph.SpaceBeforePt, 0, 72, $"{path}.spaceBeforePt", "overrides.paragraph.spaceBefore.range", "Paragraph spaceBeforePt must be between 0 and 72.", diagnostics);
        CheckRange(paragraph.SpaceAfterPt, 0, 72, $"{path}.spaceAfterPt", "overrides.paragraph.spaceAfter.range", "Paragraph spaceAfterPt must be between 0 and 72.", diagnostics);
        CheckRange(paragraph.FirstLineIndentChars, 0, 8, $"{path}.firstLineIndentChars", "overrides.paragraph.firstLineIndent.range", "Paragraph firstLineIndentChars must be between 0 and 8.", diagnostics);
        CheckRange(paragraph.HangingIndentCm, 0, 5, $"{path}.hangingIndentCm", "overrides.paragraph.hangingIndent.range", "Paragraph hangingIndentCm must be between 0 and 5.", diagnostics);
    }

    private static void CheckRange(double? value, double min, double max, string path, string code, string message, List<UnifiedDiagnostic> diagnostics)
    {
        if (value.HasValue && (value.Value < min || value.Value > max))
        {
            Add(diagnostics, code, path, message);
        }
    }

    private static void Add(List<UnifiedDiagnostic> diagnostics, string code, string path, string message)
    {
        diagnostics.Add(new UnifiedDiagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Error,
            Path = path,
            Message = message,
            FixHint = "Adjust the DocumentOverrides payload to match the documented range.",
            Category = DiagnosticCategory.Schema,
            Source = "DocumentOverridesValidator"
        });
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,80}$")]
    private static partial Regex SafeIdRegex();
}
