using System.Text;
using System.Text.Json;
using ThesisDocx.Core.Extraction;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Validation.ContentPreservation;

public sealed class ContentPreservationAuditor
{
    private const int MinimumSegmentLength = 12;

    public ContentPreservationResult Audit(DocxExtractionResult source, DocxExtractionResult rendered)
    {
        var result = new ContentPreservationResult
        {
            SourceParagraphCount = source.Paragraphs.Count,
            RenderedParagraphCount = rendered.Paragraphs.Count,
            SourceTextLength = source.PlainText.Length,
            RenderedTextLength = rendered.PlainText.Length,
            NormalizedSourceTextLength = TextNormalizer.Normalize(source.PlainText).Length,
            NormalizedRenderedTextLength = TextNormalizer.Normalize(rendered.PlainText).Length,
            FootnoteComparison = CompareCount("footnotes", CountNonEmpty(source.Footnotes.Select(note => note.Text)), CountNonEmpty(rendered.Footnotes.Select(note => note.Text))),
            EndnoteComparison = CompareCount("endnotes", CountNonEmpty(source.Endnotes.Select(note => note.Text)), CountNonEmpty(rendered.Endnotes.Select(note => note.Text))),
            BibliographyComparison = CompareCount("bibliography candidates", source.PossibleBibliography.Count, rendered.PossibleBibliography.Count),
            HeadingComparison = CompareCount("heading candidates", source.PossibleHeadings.Count, rendered.PossibleHeadings.Count),
            TableComparison = CompareCount("tables", source.Tables.Count, rendered.Tables.Count),
            FigureComparison = CompareCount("figures", source.Figures.Count, rendered.Figures.Count),
            FieldComparison = CompareCount("fields", source.Fields.Count, rendered.Fields.Count)
        };

        var renderedText = TextNormalizer.Normalize(rendered.PlainText);
        var sourceSegments = ExtractSegments(source)
            .GroupBy(segment => segment.NormalizedText, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(segment => segment.EvidencePath, StringComparer.Ordinal)
            .ToList();

        foreach (var segment in sourceSegments)
        {
            if (renderedText.Contains(segment.NormalizedText, StringComparison.Ordinal))
            {
                result.MatchedSegments++;
            }
            else
            {
                result.MissingSegments.Add(new ContentSegmentIssue
                {
                    Code = segment.NormalizedText.Length >= 80 ? "content.segment.longMissing" : "content.segment.missing",
                    Severity = segment.NormalizedText.Length >= 80 ? "breaking" : "warning",
                    EvidencePath = segment.EvidencePath,
                    TextPreview = TextNormalizer.Preview(segment.OriginalText),
                    Message = "Source text segment was not found in the rendered extraction."
                });
            }
        }

        var sourceText = TextNormalizer.Normalize(source.PlainText);
        foreach (var added in ExtractSegments(rendered)
            .Where(segment => !sourceText.Contains(segment.NormalizedText, StringComparison.Ordinal))
            .Take(50))
        {
            result.AddedSegments.Add(new ContentSegmentIssue
            {
                Code = IsLikelyTemplateAdded(added.OriginalText) ? "content.segment.templateAdded" : "content.segment.added",
                Severity = "info",
                EvidencePath = added.EvidencePath,
                TextPreview = TextNormalizer.Preview(added.OriginalText),
                Message = IsLikelyTemplateAdded(added.OriginalText)
                    ? "Rendered text appears to be template-added content."
                    : "Rendered text was not present in the source extraction."
            });
        }

        AddCountWarnings(result, result.FootnoteComparison);
        AddCountWarnings(result, result.EndnoteComparison);
        AddCountWarnings(result, result.TableComparison);
        AddCountWarnings(result, result.FigureComparison);
        AddCountWarnings(result, result.FieldComparison);

        result.BlockingIssues = result.MissingSegments
            .Where(issue => issue.Severity == "breaking")
            .Select(issue => new ContentPreservationIssue
            {
                Code = issue.Code,
                Severity = "breaking",
                EvidencePath = issue.EvidencePath,
                Message = issue.Message,
                TextPreview = issue.TextPreview
            })
            .OrderBy(issue => issue.EvidencePath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ToList();

        result.Warnings = result.Warnings
            .Concat(result.MissingSegments.Where(issue => issue.Severity == "warning").Select(issue => new ContentPreservationIssue
            {
                Code = issue.Code,
                Severity = "warning",
                EvidencePath = issue.EvidencePath,
                Message = issue.Message,
                TextPreview = issue.TextPreview
            }))
            .OrderBy(issue => issue.EvidencePath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ToList();

        result.MissingSegments = result.MissingSegments
            .OrderBy(issue => issue.EvidencePath, StringComparer.Ordinal)
            .ThenBy(issue => issue.TextPreview, StringComparer.Ordinal)
            .ToList();
        result.AddedSegments = result.AddedSegments
            .OrderBy(issue => issue.EvidencePath, StringComparer.Ordinal)
            .ThenBy(issue => issue.TextPreview, StringComparer.Ordinal)
            .ToList();
        result.Status = result.BlockingIssues.Count > 0
            ? "fail"
            : result.Warnings.Count > 0
                ? "passWithWarnings"
                : "pass";
        return result;
    }

    public ContentPreservationResult Audit(string sourceExtractionPath, string renderedExtractionPath)
    {
        var source = JsonSerializer.Deserialize<DocxExtractionResult>(File.ReadAllText(sourceExtractionPath), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not read source extraction '{sourceExtractionPath}'.");
        var rendered = JsonSerializer.Deserialize<DocxExtractionResult>(File.ReadAllText(renderedExtractionPath), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not read rendered extraction '{renderedExtractionPath}'.");
        return Audit(source, rendered);
    }

    public static string ToMarkdown(ContentPreservationResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Content Preservation Audit");
        builder.AppendLine();
        builder.AppendLine($"Status: `{result.Status}`");
        builder.AppendLine($"Source paragraphs: `{result.SourceParagraphCount}`");
        builder.AppendLine($"Rendered paragraphs: `{result.RenderedParagraphCount}`");
        builder.AppendLine($"Matched segments: `{result.MatchedSegments}`");
        builder.AppendLine($"Missing segments: `{result.MissingSegments.Count}`");
        builder.AppendLine($"Warnings: `{result.Warnings.Count}`");
        builder.AppendLine($"Blocking issues: `{result.BlockingIssues.Count}`");
        builder.AppendLine();
        builder.AppendLine("## Comparisons");
        builder.AppendLine();
        foreach (var comparison in new[]
        {
            result.FootnoteComparison,
            result.EndnoteComparison,
            result.BibliographyComparison,
            result.HeadingComparison,
            result.TableComparison,
            result.FigureComparison,
            result.FieldComparison
        })
        {
            builder.AppendLine($"- {comparison.Name}: source `{comparison.SourceCount}`, rendered `{comparison.RenderedCount}`, status `{comparison.Status}`");
        }

        if (result.BlockingIssues.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Blocking Missing Segments");
            builder.AppendLine();
            foreach (var issue in result.BlockingIssues.Take(20))
            {
                builder.AppendLine($"- `{issue.EvidencePath}` {issue.TextPreview}");
            }
        }

        if (result.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Warnings");
            builder.AppendLine();
            foreach (var issue in result.Warnings.Take(20))
            {
                builder.AppendLine($"- `{issue.Code}` at `{issue.EvidencePath}`: {issue.TextPreview}");
            }
        }

        return builder.ToString();
    }

    private static List<SourceSegment> ExtractSegments(DocxExtractionResult extraction)
    {
        var segments = new List<SourceSegment>();
        foreach (var paragraph in extraction.Paragraphs)
        {
            AddSegment(segments, paragraph.Text, paragraph.EvidencePath);
        }

        foreach (var footnote in extraction.Footnotes)
        {
            AddSegment(segments, footnote.Text, footnote.EvidencePath);
        }

        foreach (var endnote in extraction.Endnotes)
        {
            AddSegment(segments, endnote.Text, endnote.EvidencePath);
        }

        foreach (var table in extraction.Tables)
        {
            AddSegment(segments, table.Text, table.EvidencePath);
        }

        return segments;
    }

    private static void AddSegment(List<SourceSegment> segments, string text, string evidencePath)
    {
        var normalized = TextNormalizer.Normalize(text);
        if (normalized.Length < MinimumSegmentLength)
        {
            return;
        }

        segments.Add(new SourceSegment(text, normalized, evidencePath));
    }

    private static ContentCountComparison CompareCount(string name, int sourceCount, int renderedCount)
    {
        return new ContentCountComparison
        {
            Name = name,
            SourceCount = sourceCount,
            RenderedCount = renderedCount,
            Status = sourceCount == renderedCount ? "match" : "mismatch"
        };
    }

    private static void AddCountWarnings(ContentPreservationResult result, ContentCountComparison comparison)
    {
        if (comparison.Status != "mismatch" || comparison.SourceCount == 0)
        {
            return;
        }

        result.Warnings.Add(new ContentPreservationIssue
        {
            Code = $"content.{comparison.Name.Replace(" ", "", StringComparison.Ordinal)}.countMismatch",
            Severity = "warning",
            EvidencePath = comparison.Name,
            Message = $"Source has {comparison.SourceCount}; rendered has {comparison.RenderedCount}.",
            TextPreview = $"{comparison.SourceCount} -> {comparison.RenderedCount}"
        });
    }

    private static bool IsLikelyTemplateAdded(string text)
    {
        return text.Contains("毕业论文", StringComparison.Ordinal)
            || text.Contains("原创性声明", StringComparison.Ordinal)
            || text.Contains("独创性声明", StringComparison.Ordinal)
            || text.Contains("论文使用授权", StringComparison.Ordinal)
            || text.Contains("待补充", StringComparison.Ordinal)
            || text.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
            || text.Contains("目录", StringComparison.Ordinal);
    }

    private static int CountNonEmpty(IEnumerable<string> values)
    {
        return values.Count(value => !string.IsNullOrWhiteSpace(TextNormalizer.Normalize(value)));
    }

    private sealed record SourceSegment(string OriginalText, string NormalizedText, string EvidencePath);
}

public static class TextNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormKC)
            .Replace('\u00A0', ' ')
            .Replace("\r", "\n", StringComparison.Ordinal);
        return string.Join(" ", normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    public static string Preview(string value, int length = 120)
    {
        var normalized = Normalize(value);
        return normalized.Length <= length ? normalized : normalized[..length] + "...";
    }
}

public sealed class ContentPreservationResult
{
    public string Status { get; set; } = "pass";
    public int SourceParagraphCount { get; set; }
    public int RenderedParagraphCount { get; set; }
    public int SourceTextLength { get; set; }
    public int RenderedTextLength { get; set; }
    public int NormalizedSourceTextLength { get; set; }
    public int NormalizedRenderedTextLength { get; set; }
    public int MatchedSegments { get; set; }
    public List<ContentSegmentIssue> MissingSegments { get; set; } = [];
    public List<ContentSegmentIssue> AddedSegments { get; set; } = [];
    public List<ContentSegmentIssue> ChangedSegments { get; set; } = [];
    public ContentCountComparison FootnoteComparison { get; set; } = new();
    public ContentCountComparison EndnoteComparison { get; set; } = new();
    public ContentCountComparison BibliographyComparison { get; set; } = new();
    public ContentCountComparison HeadingComparison { get; set; } = new();
    public ContentCountComparison TableComparison { get; set; } = new();
    public ContentCountComparison FigureComparison { get; set; } = new();
    public ContentCountComparison FieldComparison { get; set; } = new();
    public List<ContentPreservationIssue> Warnings { get; set; } = [];
    public List<ContentPreservationIssue> BlockingIssues { get; set; } = [];
}

public sealed class ContentSegmentIssue
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string EvidencePath { get; set; } = string.Empty;
    public string TextPreview { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ContentPreservationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string EvidencePath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TextPreview { get; set; } = string.Empty;
}

public sealed class ContentCountComparison
{
    public string Name { get; set; } = string.Empty;
    public int SourceCount { get; set; }
    public int RenderedCount { get; set; }
    public string Status { get; set; } = "match";
}
