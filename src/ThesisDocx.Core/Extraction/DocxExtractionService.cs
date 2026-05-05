using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ThesisDocx.Core.Utilities;
using A = DocumentFormat.OpenXml.Drawing;

namespace ThesisDocx.Core.Extraction;

public sealed class DocxExtractionService
{
    public DocxExtractionResult Extract(DocxExtractionOptions options)
    {
        using var document = WordprocessingDocument.Open(options.InputPath, false);
        var main = document.MainDocumentPart ?? throw new InvalidDataException("DOCX has no main document part.");
        var body = main.Document.Body ?? throw new InvalidDataException("DOCX has no document body.");
        var styleNames = LoadStyleNames(main);
        var artifactImageDirectory = ResolveImageDirectory(options);
        var result = new DocxExtractionResult
        {
            InputFileName = Path.GetFileName(options.InputPath)
        };

        var paragraphIndex = 0;
        var tableIndex = 0;
        var figureIndex = 0;
        var blockIndex = 0;
        var previousTexts = new Queue<string>();
        foreach (var element in body.Elements())
        {
            switch (element)
            {
                case Paragraph paragraph:
                    var extracted = ExtractParagraph(paragraph, paragraphIndex, styleNames);
                    result.Paragraphs.Add(extracted);
                    result.Blocks.Add(new ExtractedBlock
                    {
                        Id = $"block-{blockIndex}",
                        Type = "paragraph",
                        Index = blockIndex,
                        EvidencePath = extracted.EvidencePath
                    });
                    ExtractParagraphChildren(paragraph, extracted, result, main, previousTexts, ref figureIndex, artifactImageDirectory);
                    ClassifyParagraph(extracted, result);
                    paragraphIndex++;
                    blockIndex++;
                    if (!string.IsNullOrWhiteSpace(extracted.Text))
                    {
                        previousTexts.Enqueue(extracted.Text);
                        while (previousTexts.Count > 3)
                        {
                            previousTexts.Dequeue();
                        }
                    }
                    break;
                case Table table:
                    var extractedTable = ExtractTable(table, tableIndex);
                    result.Tables.Add(extractedTable);
                    result.Blocks.Add(new ExtractedBlock
                    {
                        Id = $"block-{blockIndex}",
                        Type = "table",
                        Index = blockIndex,
                        EvidencePath = extractedTable.EvidencePath
                    });
                    tableIndex++;
                    blockIndex++;
                    break;
                case SectionProperties sectionProperties:
                    result.Sections.Add(ExtractSection(sectionProperties, result.Sections.Count));
                    break;
            }
        }

        foreach (var paragraph in result.Paragraphs.Where(p => p.Text.StartsWith("图", StringComparison.Ordinal) || p.Text.StartsWith("表", StringComparison.Ordinal)))
        {
            result.PossibleCaptions.Add(Evidence($"caption-{result.PossibleCaptions.Count}", paragraph.EvidencePath, paragraph.Text, "caption pattern", 0.85));
        }

        ExtractFootnotes(main, result);
        ExtractEndnotes(main, result);
        SummarizeStylesAndNumbering(result);
        result.PlainText = string.Join(Environment.NewLine, result.Paragraphs.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
        result.Document = new ExtractedDocument
        {
            ParagraphCount = result.Paragraphs.Count,
            TableCount = result.Tables.Count,
            FigureCount = result.Figures.Count,
            FootnoteCount = result.Footnotes.Count,
            EndnoteCount = result.Endnotes.Count
        };
        result.Sections.AddRange(body.Descendants<SectionProperties>().Select((sectPr, index) => ExtractSection(sectPr, result.Sections.Count + index)));
        result.Sections = result.Sections.GroupBy(s => s.EvidencePath, StringComparer.Ordinal).Select(g => g.First()).ToList();

        WriteOutputs(options, result);
        return result;
    }

    private static ExtractedParagraph ExtractParagraph(Paragraph paragraph, int index, Dictionary<string, string> styleNames)
    {
        var pPr = paragraph.ParagraphProperties;
        var styleId = pPr?.ParagraphStyleId?.Val?.Value;
        var runs = paragraph.Elements<Run>().Select((run, runIndex) => ExtractRun(run, $"p{index}-r{runIndex}")).ToList();
        foreach (var reference in paragraph.Descendants<FootnoteReference>())
        {
            runs.Add(new ExtractedRun { Id = $"p{index}-fn{reference.Id?.Value}", Text = $"[^fn{reference.Id?.Value}]" });
        }

        foreach (var reference in paragraph.Descendants<EndnoteReference>())
        {
            runs.Add(new ExtractedRun { Id = $"p{index}-en{reference.Id?.Value}", Text = $"[^en{reference.Id?.Value}]" });
        }

        var text = string.Concat(runs.Select(r => r.Text));
        var outlineRaw = pPr?.OutlineLevel?.Val?.Value;
        var numId = pPr?.NumberingProperties?.NumberingId?.Val?.Value.ToString();
        var ilvlRaw = pPr?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
        return new ExtractedParagraph
        {
            Id = $"paragraph-{index}",
            Index = index,
            Text = NormalizeText(text),
            StyleId = styleId,
            StyleName = styleId is not null && styleNames.TryGetValue(styleId, out var name) ? name : null,
            OutlineLevel = ToNullableInt(outlineRaw),
            NumberingId = numId,
            NumberingLevel = ToNullableInt(ilvlRaw),
            Alignment = pPr?.Justification?.Val?.Value.ToString(),
            Indent = pPr?.Indentation?.OuterXml,
            Spacing = pPr?.SpacingBetweenLines?.OuterXml,
            Runs = runs,
            RunSummary = new ExtractedRunSummary
            {
                Count = runs.Count,
                HasBold = runs.Any(r => r.Bold),
                HasItalic = runs.Any(r => r.Italic),
                HasUnderline = runs.Any(r => r.Underline),
                HasSuperscript = runs.Any(r => r.Superscript),
                HasSubscript = runs.Any(r => r.Subscript)
            },
            EvidencePath = $"paragraphs[{index}]"
        };
    }

    private static ExtractedRun ExtractRun(Run run, string id)
    {
        var rPr = run.RunProperties;
        var vertical = rPr?.VerticalTextAlignment?.Val?.Value;
        var fontSize = rPr?.FontSize?.Val?.Value;
        return new ExtractedRun
        {
            Id = id,
            Text = NormalizeText(string.Concat(run.Descendants<Text>().Select(t => t.Text))),
            Bold = rPr?.Bold is not null,
            Italic = rPr?.Italic is not null,
            Underline = rPr?.Underline is not null,
            Font = rPr?.RunFonts?.Ascii?.Value ?? rPr?.RunFonts?.HighAnsi?.Value,
            EastAsiaFont = rPr?.RunFonts?.EastAsia?.Value,
            FontSizePt = fontSize is null ? null : double.Parse(fontSize) / 2.0,
            Color = rPr?.Color?.Val?.Value,
            Superscript = vertical == VerticalPositionValues.Superscript,
            Subscript = vertical == VerticalPositionValues.Subscript
        };
    }

    private static void ExtractParagraphChildren(
        Paragraph paragraph,
        ExtractedParagraph extracted,
        DocxExtractionResult result,
        MainDocumentPart main,
        Queue<string> previousTexts,
        ref int figureIndex,
        string imageDirectory)
    {
        foreach (var bookmark in paragraph.Descendants<BookmarkStart>())
        {
            result.Bookmarks.Add(new ExtractedBookmark
            {
                Id = $"bookmark-{result.Bookmarks.Count}",
                Name = bookmark.Name?.Value ?? string.Empty,
                EvidencePath = extracted.EvidencePath
            });
        }

        foreach (var hyperlink in paragraph.Descendants<Hyperlink>())
        {
            var relId = hyperlink.Id?.Value;
            result.Hyperlinks.Add(new ExtractedHyperlink
            {
                Id = $"hyperlink-{result.Hyperlinks.Count}",
                Text = NormalizeText(hyperlink.InnerText),
                Uri = relId is not null ? main.HyperlinkRelationships.FirstOrDefault(r => r.Id == relId)?.Uri.ToString() : null,
                EvidencePath = extracted.EvidencePath
            });
        }

        foreach (var field in ExtractFields(paragraph, extracted.EvidencePath))
        {
            result.Fields.Add(field);
        }

        foreach (var blip in paragraph.Descendants<A.Blip>())
        {
            var relId = blip.Embed?.Value ?? blip.Link?.Value;
            string? contentType = null;
            string? artifactPath = null;
            if (relId is not null && main.GetPartById(relId) is ImagePart imagePart)
            {
                contentType = imagePart.ContentType;
                Directory.CreateDirectory(imageDirectory);
                var extension = ContentTypeExtension(contentType);
                var fileName = $"image-{figureIndex}{extension}";
                var output = Path.Combine(imageDirectory, fileName);
                using var input = imagePart.GetStream();
                using var file = File.Create(output);
                input.CopyTo(file);
                artifactPath = Path.GetRelativePath(Path.GetDirectoryName(imageDirectory) ?? imageDirectory, output).Replace('\\', '/');
            }

            result.Figures.Add(new ExtractedFigure
            {
                Id = $"figure-{figureIndex}",
                Index = figureIndex,
                RelationshipId = relId,
                ContentType = contentType,
                ArtifactPath = artifactPath,
                SuggestedCaption = GuessNearbyCaption(extracted.Text),
                NearbyText = string.Join(" / ", previousTexts.Append(extracted.Text).Where(t => !string.IsNullOrWhiteSpace(t))),
                EvidencePath = extracted.EvidencePath
            });
            figureIndex++;
        }
    }

    private static List<ExtractedField> ExtractFields(Paragraph paragraph, string evidencePath)
    {
        var fields = new List<ExtractedField>();
        foreach (var simple in paragraph.Descendants<SimpleField>())
        {
            var instruction = simple.Instruction?.Value ?? string.Empty;
            fields.Add(new ExtractedField
            {
                Id = $"field-{fields.Count}",
                Instruction = instruction,
                FieldType = FieldType(instruction),
                EvidencePath = evidencePath
            });
        }

        foreach (var instruction in paragraph.Descendants<FieldCode>().Select(code => code.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text)))
        {
            fields.Add(new ExtractedField
            {
                Id = $"field-{fields.Count}",
                Instruction = instruction,
                FieldType = FieldType(instruction),
                EvidencePath = evidencePath
            });
        }

        return fields;
    }

    private static ExtractedTable ExtractTable(Table table, int index)
    {
        var rows = table.Elements<TableRow>().Select((row, rowIndex) => new ExtractedTableRow
        {
            Index = rowIndex,
            Cells = row.Elements<TableCell>().Select((cell, cellIndex) =>
            {
                var gridSpan = cell.TableCellProperties?.GridSpan?.Val?.Value;
                return new ExtractedTableCell
                {
                    RowIndex = rowIndex,
                    CellIndex = cellIndex,
                    Text = NormalizeText(string.Join(Environment.NewLine, cell.Descendants<Paragraph>().Select(p => p.InnerText).Where(t => !string.IsNullOrWhiteSpace(t)))),
                    GridSpan = gridSpan.HasValue ? Math.Max(1, gridSpan.Value) : 1,
                    VerticalMerge = cell.TableCellProperties?.VerticalMerge?.Val?.Value.ToString(),
                    Borders = cell.TableCellProperties?.TableCellBorders?.OuterXml,
                    EvidencePath = $"tables[{index}].rows[{rowIndex}].cells[{cellIndex}]"
                };
            }).ToList()
        }).ToList();
        return new ExtractedTable
        {
            Id = $"table-{index}",
            Index = index,
            Rows = rows,
            Text = NormalizeText(string.Join(Environment.NewLine, rows.SelectMany(r => r.Cells).Select(c => c.Text))),
            Borders = table.GetFirstChild<TableProperties>()?.TableBorders?.OuterXml,
            EvidencePath = $"tables[{index}]"
        };
    }

    private static ExtractedSection ExtractSection(SectionProperties section, int index)
    {
        var size = section.GetFirstChild<PageSize>();
        var margins = section.GetFirstChild<PageMargin>();
        var numbering = section.GetFirstChild<PageNumberType>();
        return new ExtractedSection
        {
            Id = $"section-{index}",
            Index = index,
            EvidencePath = $"sections[{index}]",
            PageSize = size is null ? string.Empty : $"w={size.Width} h={size.Height} orient={size.Orient?.Value}",
            Margins = margins is null ? string.Empty : $"top={margins.Top} bottom={margins.Bottom} left={margins.Left} right={margins.Right}",
            PageNumbering = numbering is null ? string.Empty : $"fmt={numbering.Format?.Value} start={numbering.Start?.Value}"
        };
    }

    private static void ExtractFootnotes(MainDocumentPart main, DocxExtractionResult result)
    {
        var notes = main.FootnotesPart?.Footnotes?.Elements<Footnote>() ?? [];
        foreach (var note in notes.Where(n => n.Id?.Value is not null && n.Id!.Value >= 0))
        {
            result.Footnotes.Add(new ExtractedFootnote
            {
                Id = $"footnote-{result.Footnotes.Count}",
                NoteId = note.Id!.Value.ToString(),
                Text = NormalizeText(note.InnerText),
                EvidencePath = $"footnotes[{result.Footnotes.Count}]"
            });
        }
    }

    private static void ExtractEndnotes(MainDocumentPart main, DocxExtractionResult result)
    {
        var notes = main.EndnotesPart?.Endnotes?.Elements<Endnote>() ?? [];
        foreach (var note in notes.Where(n => n.Id?.Value is not null && n.Id!.Value >= 0))
        {
            result.Endnotes.Add(new ExtractedEndnote
            {
                Id = $"endnote-{result.Endnotes.Count}",
                NoteId = note.Id!.Value.ToString(),
                Text = NormalizeText(note.InnerText),
                EvidencePath = $"endnotes[{result.Endnotes.Count}]"
            });
        }
    }

    private static void ClassifyParagraph(ExtractedParagraph paragraph, DocxExtractionResult result)
    {
        var text = paragraph.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            paragraph.PossibleRole = "empty";
            return;
        }

        if (Regex.IsMatch(text, @"^(摘要|内容摘要)\s*[:：]?", RegexOptions.CultureInvariant))
        {
            paragraph.PossibleRole = "abstractCandidate";
            result.PossibleAbstract.Add(Evidence($"abstract-{paragraph.Index}", paragraph.EvidencePath, text, "Chinese abstract label", 0.9));
        }
        else if (Regex.IsMatch(text, @"^(关键词|key\s*words?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            paragraph.PossibleRole = "keywordsCandidate";
            result.PossibleKeywords.Add(Evidence($"keywords-{paragraph.Index}", paragraph.EvidencePath, text, "keywords label", 0.9));
        }
        else if (Regex.IsMatch(text, @"^(参考文献|References)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || Regex.IsMatch(text, @"^\[[0-9]+\]"))
        {
            paragraph.PossibleRole = "bibliographyCandidate";
            result.PossibleBibliography.Add(Evidence($"bibliography-{paragraph.Index}", paragraph.EvidencePath, text, "bibliography label or numbered item", 0.85));
        }
        else if (paragraph.OutlineLevel is <= 5 || IsHeadingLike(text, paragraph))
        {
            paragraph.PossibleRole = "headingCandidate";
            result.PossibleHeadings.Add(Evidence($"heading-{paragraph.Index}", paragraph.EvidencePath, text, "style/numbering/shape heading candidate", paragraph.OutlineLevel is null ? 0.65 : 0.9));
        }
        else if (Regex.IsMatch(text, @"^(附录|Appendix)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            paragraph.PossibleRole = "appendixCandidate";
            result.PossibleAppendix.Add(Evidence($"appendix-{paragraph.Index}", paragraph.EvidencePath, text, "appendix label", 0.85));
        }
    }

    private static void SummarizeStylesAndNumbering(DocxExtractionResult result)
    {
        result.Styles = result.Paragraphs
            .Where(p => !string.IsNullOrWhiteSpace(p.StyleId))
            .GroupBy(p => p.StyleId!, StringComparer.Ordinal)
            .Select(group => new ExtractedStyleUsage
            {
                Id = $"style-{group.Key}",
                StyleId = group.Key,
                Name = group.First().StyleName,
                Type = "paragraph",
                UsageCount = group.Count()
            })
            .OrderBy(s => s.StyleId, StringComparer.Ordinal)
            .ToList();
        result.Numbering = result.Paragraphs
            .Where(p => !string.IsNullOrWhiteSpace(p.NumberingId))
            .GroupBy(p => p.NumberingId!, StringComparer.Ordinal)
            .Select(group => new ExtractedNumberingUsage
            {
                Id = $"numbering-{group.Key}",
                NumberingId = group.Key,
                Levels = group.Select(p => p.NumberingLevel ?? 0).Distinct().Order().ToList(),
                UsageCount = group.Count()
            })
            .OrderBy(n => n.NumberingId, StringComparer.Ordinal)
            .ToList();
    }

    private static void WriteOutputs(DocxExtractionOptions options, DocxExtractionResult result)
    {
        if (!string.IsNullOrWhiteSpace(options.OutputJsonPath))
        {
            WriteText(options.OutputJsonPath, JsonSerializer.Serialize(result, ThesisJson.Options));
        }

        if (!string.IsNullOrWhiteSpace(options.PlainTextPath))
        {
            WriteText(options.PlainTextPath, result.PlainText);
        }

        if (!string.IsNullOrWhiteSpace(options.MarkdownPath))
        {
            WriteText(options.MarkdownPath, ToMarkdown(result));
        }
    }

    public static string ToMarkdown(DocxExtractionResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# DOCX Extraction: {result.InputFileName}");
        builder.AppendLine();
        builder.AppendLine($"- Paragraphs: {result.Paragraphs.Count}");
        builder.AppendLine($"- Tables: {result.Tables.Count}");
        builder.AppendLine($"- Figures: {result.Figures.Count}");
        builder.AppendLine($"- Footnotes: {result.Footnotes.Count}");
        builder.AppendLine();
        builder.AppendLine("## Paragraphs");
        foreach (var paragraph in result.Paragraphs)
        {
            builder.AppendLine($"- `{paragraph.EvidencePath}` `{paragraph.PossibleRole}` {paragraph.Text}");
        }

        if (result.Tables.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Tables");
            foreach (var table in result.Tables)
            {
                builder.AppendLine($"- `{table.EvidencePath}` rows={table.Rows.Count}: {table.Text}");
            }
        }

        return builder.ToString();
    }

    private static Dictionary<string, string> LoadStyleNames(MainDocumentPart main)
    {
        return main.StyleDefinitionsPart?.Styles?.Elements<Style>()
            .Where(style => style.StyleId?.Value is not null)
            .ToDictionary(style => style.StyleId!.Value!, style => style.StyleName?.Val?.Value ?? style.StyleId!.Value!, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static bool IsHeadingLike(string text, ExtractedParagraph paragraph)
    {
        if (paragraph.StyleName?.Contains("heading", StringComparison.OrdinalIgnoreCase) == true || paragraph.StyleName?.Contains("标题", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return text.Length <= 40 && Regex.IsMatch(text, @"^(第[一二三四五六七八九十0-9]+[章节]|[0-9]+(\.[0-9]+){0,2}\s+|[一二三四五六七八九十]+[、.])", RegexOptions.CultureInvariant);
    }

    private static ExtractionEvidence Evidence(string id, string path, string text, string reason, double confidence)
    {
        return new ExtractionEvidence { Id = id, EvidencePath = path, Text = text, Reason = reason, Confidence = confidence };
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static string FieldType(string instruction)
    {
        var normalized = instruction.Trim().ToUpperInvariant();
        if (normalized.StartsWith("TOC", StringComparison.Ordinal)) return "TOC";
        if (normalized.StartsWith("PAGE", StringComparison.Ordinal)) return "PAGE";
        if (normalized.StartsWith("REF", StringComparison.Ordinal) || normalized.Contains(" PAGEREF ", StringComparison.Ordinal)) return "REF";
        return "other";
    }

    private static int? ToNullableInt(object? value)
    {
        return value is null ? null : int.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static string? GuessNearbyCaption(string text)
    {
        return Regex.IsMatch(text, @"^(图|Figure)\s*[0-9]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ? text : null;
    }

    private static string ResolveImageDirectory(DocxExtractionOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ArtifactsDirectory))
        {
            return Path.Combine(options.ArtifactsDirectory, "images");
        }

        var outputDirectory = string.IsNullOrWhiteSpace(options.OutputJsonPath)
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(Path.GetFullPath(options.OutputJsonPath)) ?? Directory.GetCurrentDirectory();
        return Path.Combine(outputDirectory, "..", "artifacts", "images");
    }

    private static string ContentTypeExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tiff",
            _ => ".png"
        };
    }

    private static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory());
        File.WriteAllText(path, text);
    }
}
