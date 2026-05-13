using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Extraction;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Validation.ContentPreservation;

public sealed class ContentPreservationAuditor
{
    private const int MinimumSegmentLength = 12;

    public ContentPreservationResult Audit(DocxExtractionResult source, DocxExtractionResult rendered)
    {
        var sourceSearchText = BuildSearchText(source);
        var renderedSearchText = BuildSearchText(rendered);
        var result = new ContentPreservationResult
        {
            SourceParagraphCount = source.Paragraphs.Count,
            RenderedParagraphCount = rendered.Paragraphs.Count,
            SourceTextLength = source.PlainText.Length,
            RenderedTextLength = rendered.PlainText.Length,
            NormalizedSourceTextLength = sourceSearchText.Length,
            NormalizedRenderedTextLength = renderedSearchText.Length,
            SourceContentHash = Sha256(sourceSearchText),
            RenderedContentHash = Sha256(renderedSearchText),
            FootnoteComparison = CompareCount("footnotes", CountNonEmpty(source.Footnotes.Select(note => note.Text)), CountNonEmpty(rendered.Footnotes.Select(note => note.Text))),
            EndnoteComparison = CompareCount("endnotes", CountNonEmpty(source.Endnotes.Select(note => note.Text)), CountNonEmpty(rendered.Endnotes.Select(note => note.Text))),
            BibliographyComparison = CompareCount("bibliography candidates", source.PossibleBibliography.Count, rendered.PossibleBibliography.Count),
            HeadingComparison = CompareCount("heading candidates", source.PossibleHeadings.Count, rendered.PossibleHeadings.Count),
            TableComparison = CompareCount("tables", source.Tables.Count, rendered.Tables.Count),
            FigureComparison = CompareCount("figures", source.Figures.Count, rendered.Figures.Count),
            FieldComparison = CompareCount("fields", source.Fields.Count, rendered.Fields.Count)
        };

        var sourceSegments = ExtractSegments(source)
            .GroupBy(segment => segment.NormalizedText, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(segment => segment.EvidencePath, StringComparer.Ordinal)
            .ToList();

        foreach (var segment in sourceSegments)
        {
            if (renderedSearchText.Contains(segment.NormalizedText, StringComparison.Ordinal))
            {
                result.MatchedSegments++;
            }
            else
            {
                result.MissingSegments.Add(new ContentSegmentIssue
                {
                    Code = segment.NormalizedText.Length >= 80 ? "content.segment.longMissing" : "content.segment.missing",
                    Severity = segment.NormalizedText.Length >= 80 ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                    EvidencePath = segment.EvidencePath,
                    TextPreview = TextNormalizer.Preview(segment.OriginalText),
                    Message = "Source text segment was not found in the rendered extraction."
                });
            }
        }

        foreach (var added in ExtractSegments(rendered)
            .Where(segment => !sourceSearchText.Contains(segment.NormalizedText, StringComparison.Ordinal))
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
            .Where(issue => UnifiedDiagnosticMapper.IsError(issue.Severity))
            .Select(issue => new ContentPreservationIssue
            {
                Code = issue.Code,
                Severity = DiagnosticSeverity.Error,
                EvidencePath = issue.EvidencePath,
                Message = issue.Message,
                TextPreview = issue.TextPreview
            })
            .OrderBy(issue => issue.EvidencePath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ToList();

        result.Warnings = result.Warnings
            .Concat(result.MissingSegments.Where(issue => UnifiedDiagnosticMapper.IsWarning(issue.Severity)).Select(issue => new ContentPreservationIssue
            {
                Code = issue.Code,
                Severity = DiagnosticSeverity.Warning,
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

    public ContentPreservationResult AuditDraft(DocxExtractionResult source, ThesisDocument draft)
    {
        return Audit(source, DraftExtraction(draft));
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
        builder.AppendLine($"Source content hash: `{result.SourceContentHash}`");
        builder.AppendLine($"Rendered content hash: `{result.RenderedContentHash}`");
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

    private static DocxExtractionResult DraftExtraction(ThesisDocument document)
    {
        var extraction = new DocxExtractionResult
        {
            InputFileName = "thesis-document.draft.json"
        };
        var textSegments = new List<string>();
        var paragraphIndex = 0;
        var tableIndex = 0;
        var footnoteIndex = 0;
        var endnoteIndex = 0;

        for (var sectionIndex = 0; sectionIndex < document.Sections.Count; sectionIndex++)
        {
            var section = document.Sections[sectionIndex];
            for (var blockIndex = 0; blockIndex < section.Blocks.Count; blockIndex++)
            {
                var path = $"draft.sections[{sectionIndex}].blocks[{blockIndex}]";
                AddDraftBlock(section.Blocks[blockIndex], path, extraction, textSegments, ref paragraphIndex, ref tableIndex, ref footnoteIndex, ref endnoteIndex);
            }
        }

        extraction.PlainText = string.Join(Environment.NewLine, textSegments.Where(text => !string.IsNullOrWhiteSpace(text)));
        return extraction;
    }

    private static void AddDraftBlock(
        BlockNode block,
        string path,
        DocxExtractionResult extraction,
        List<string> textSegments,
        ref int paragraphIndex,
        ref int tableIndex,
        ref int footnoteIndex,
        ref int endnoteIndex)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                AddDraftParagraph(extraction, textSegments, InlinesText(paragraph.Inlines), path, ref paragraphIndex);
                AddInlineNotes(paragraph.Inlines, path, extraction, ref footnoteIndex, ref endnoteIndex);
                break;
            case HeadingBlock heading:
                AddDraftParagraph(extraction, textSegments, InlinesText(heading.Inlines), path, ref paragraphIndex);
                AddInlineNotes(heading.Inlines, path, extraction, ref footnoteIndex, ref endnoteIndex);
                break;
            case QuoteBlock quote:
                AddDraftParagraph(extraction, textSegments, InlinesText(quote.Inlines), path, ref paragraphIndex);
                AddInlineNotes(quote.Inlines, path, extraction, ref footnoteIndex, ref endnoteIndex);
                break;
            case ListBlock list:
                for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                {
                    for (var childIndex = 0; childIndex < list.Items[itemIndex].Blocks.Count; childIndex++)
                    {
                        AddDraftBlock(list.Items[itemIndex].Blocks[childIndex], $"{path}.items[{itemIndex}].blocks[{childIndex}]", extraction, textSegments, ref paragraphIndex, ref tableIndex, ref footnoteIndex, ref endnoteIndex);
                    }
                }

                break;
            case FigureBlock figure:
                AddDraftParagraph(extraction, textSegments, figure.Caption, path, ref paragraphIndex);
                break;
            case TableBlock table:
                AddDraftTable(extraction, textSegments, table, path, ref tableIndex);
                break;
            case EquationBlock equation:
                AddDraftParagraph(extraction, textSegments, equation.PlainText ?? equation.Placeholder, path, ref paragraphIndex);
                if (!string.IsNullOrWhiteSpace(equation.Caption))
                {
                    AddDraftParagraph(extraction, textSegments, equation.Caption!, $"{path}.caption", ref paragraphIndex);
                }

                break;
            case BibliographyBlock bibliography:
                foreach (var (entry, index) in bibliography.Entries.Select((entry, index) => (entry, index)))
                {
                    AddDraftParagraph(extraction, textSegments, entry.Text, $"{path}.entries[{index}]", ref paragraphIndex);
                }

                break;
            case FootnoteBlock footnote:
                AddDraftFootnote(extraction, InlinesText(footnote.Inlines), footnote.NoteId, path, ref footnoteIndex);
                break;
            case EndnoteBlock endnote:
                AddDraftEndnote(extraction, InlinesText(endnote.Inlines), endnote.NoteId, path, ref endnoteIndex);
                break;
        }
    }

    private static void AddDraftParagraph(DocxExtractionResult extraction, List<string> textSegments, string text, string path, ref int paragraphIndex)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        extraction.Paragraphs.Add(new ExtractedParagraph
        {
            Id = $"draft-paragraph-{paragraphIndex}",
            Index = paragraphIndex,
            Text = text,
            EvidencePath = path
        });
        textSegments.Add(text);
        paragraphIndex++;
    }

    private static void AddDraftTable(DocxExtractionResult extraction, List<string> textSegments, TableBlock table, string path, ref int tableIndex)
    {
        var rows = new List<ExtractedTableRow>();
        var tableText = new List<string>();
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            var cells = new List<ExtractedTableCell>();
            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
            {
                var cell = row.Cells[cellIndex];
                var cellText = !string.IsNullOrWhiteSpace(cell.Text)
                    ? cell.Text
                    : string.Join(" ", cell.Blocks.Select(BlockText).Where(text => !string.IsNullOrWhiteSpace(text)));
                tableText.Add(cellText);
                cells.Add(new ExtractedTableCell
                {
                    RowIndex = rowIndex,
                    CellIndex = cellIndex,
                    Text = cellText,
                    GridSpan = Math.Max(1, cell.GridSpan),
                    EvidencePath = $"{path}.rows[{rowIndex}].cells[{cellIndex}]"
                });
            }

            rows.Add(new ExtractedTableRow { Index = rowIndex, Cells = cells });
        }

        var joined = string.Join(" ", tableText.Where(text => !string.IsNullOrWhiteSpace(text)));
        extraction.Tables.Add(new ExtractedTable
        {
            Id = $"draft-table-{tableIndex}",
            Index = tableIndex,
            Rows = rows,
            Text = joined,
            EvidencePath = path
        });
        if (!string.IsNullOrWhiteSpace(joined))
        {
            textSegments.Add(joined);
        }

        tableIndex++;
    }

    private static void AddInlineNotes(IEnumerable<InlineNode> inlines, string path, DocxExtractionResult extraction, ref int footnoteIndex, ref int endnoteIndex)
    {
        var index = 0;
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case BookmarkInline bookmark:
                    AddInlineNotes(bookmark.Inlines, $"{path}.inlines[{index}]", extraction, ref footnoteIndex, ref endnoteIndex);
                    break;
                case FootnoteInline footnote:
                    AddDraftFootnote(extraction, InlinesText(footnote.Inlines), footnote.NoteId, $"{path}.inlines[{index}]", ref footnoteIndex);
                    break;
                case EndnoteInline endnote:
                    AddDraftEndnote(extraction, InlinesText(endnote.Inlines), endnote.NoteId, $"{path}.inlines[{index}]", ref endnoteIndex);
                    break;
            }

            index++;
        }
    }

    private static void AddDraftFootnote(DocxExtractionResult extraction, string text, string noteId, string path, ref int footnoteIndex)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        extraction.Footnotes.Add(new ExtractedFootnote
        {
            Id = $"draft-footnote-{footnoteIndex}",
            NoteId = noteId,
            Text = text,
            EvidencePath = path
        });
        footnoteIndex++;
    }

    private static void AddDraftEndnote(DocxExtractionResult extraction, string text, string noteId, string path, ref int endnoteIndex)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        extraction.Endnotes.Add(new ExtractedEndnote
        {
            Id = $"draft-endnote-{endnoteIndex}",
            NoteId = noteId,
            Text = text,
            EvidencePath = path
        });
        endnoteIndex++;
    }

    private static string BlockText(BlockNode block)
    {
        return block switch
        {
            ParagraphBlock paragraph => InlinesText(paragraph.Inlines),
            HeadingBlock heading => InlinesText(heading.Inlines),
            QuoteBlock quote => InlinesText(quote.Inlines),
            ListBlock list => string.Join(" ", list.Items.SelectMany(item => item.Blocks).Select(BlockText).Where(text => !string.IsNullOrWhiteSpace(text))),
            FigureBlock figure => figure.Caption,
            TableBlock table => string.Join(" ", table.Rows.SelectMany(row => row.Cells).Select(cell => !string.IsNullOrWhiteSpace(cell.Text) ? cell.Text : string.Join(" ", cell.Blocks.Select(BlockText))).Where(text => !string.IsNullOrWhiteSpace(text))),
            EquationBlock equation => equation.PlainText ?? equation.Placeholder,
            BibliographyBlock bibliography => string.Join(" ", bibliography.Entries.Select(entry => entry.Text).Where(text => !string.IsNullOrWhiteSpace(text))),
            FootnoteBlock footnote => InlinesText(footnote.Inlines),
            EndnoteBlock endnote => InlinesText(endnote.Inlines),
            _ => string.Empty
        };
    }

    private static string InlinesText(IEnumerable<InlineNode> inlines)
    {
        return string.Join(string.Empty, inlines.Select(InlineText));
    }

    private static string InlineText(InlineNode inline)
    {
        return inline switch
        {
            TextInline text => text.Text,
            HyperlinkInline hyperlink => hyperlink.Text,
            CitationInline citation => citation.DisplayText,
            BookmarkInline bookmark => InlinesText(bookmark.Inlines),
            ReferenceInline reference => reference.FallbackText ?? reference.BookmarkName,
            FootnoteInline footnote => InlinesText(footnote.Inlines),
            EndnoteInline endnote => InlinesText(endnote.Inlines),
            _ => string.Empty
        };
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

    private static string BuildSearchText(DocxExtractionResult extraction)
    {
        var values = new List<string> { extraction.PlainText };
        values.AddRange(extraction.Footnotes.Select(note => note.Text));
        values.AddRange(extraction.Endnotes.Select(note => note.Text));
        values.AddRange(extraction.Tables.Select(table => table.Text));
        return TextNormalizer.Normalize(string.Join(Environment.NewLine, values.Where(value => !string.IsNullOrWhiteSpace(value))));
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

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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
    public string SourceContentHash { get; set; } = string.Empty;
    public string RenderedContentHash { get; set; } = string.Empty;
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
