using System.Text.Json;
using System.Text.RegularExpressions;
using ThesisDocx.Core.Extraction;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Core.Validation.ContentPreservation;

namespace ThesisDocx.Core.Structuring;

public sealed class ThesisStructureMapper
{
    private static readonly Regex FootnoteMarkerPattern = new(@"^\[\^fn(?<id>[^\]]+)\]$", RegexOptions.CultureInvariant);
    private static readonly Regex EndnoteMarkerPattern = new(@"^\[\^en(?<id>[^\]]+)\]$", RegexOptions.CultureInvariant);

    public ThesisStructuringResult Map(DocxExtractionResult extraction, string sourceExtraction = "")
    {
        var result = new ThesisStructuringResult();
        result.Document = new ThesisDocument
        {
            SchemaVersion = ThesisSchemaVersions.Version110,
            Metadata = BuildMetadata(extraction, result),
            Sections = []
        };

        var current = NewSection(ThesisSectionKind.Body, "正文", "body");
        var bibliographyEntries = new List<BibliographyEntryNode>();
        var mappedFigureIds = new HashSet<string>(StringComparer.Ordinal);
        var noteContext = BuildNoteContext(extraction);
        var figuresByEvidencePath = extraction.Figures
            .GroupBy(figure => figure.EvidencePath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        foreach (var paragraph in extraction.Paragraphs.Where(p => !string.IsNullOrWhiteSpace(p.Text)))
        {
            var text = paragraph.Text.Trim();
            var role = Classify(text, paragraph);
            switch (role)
            {
                case "cover":
                    current = AddSection(result, ThesisSectionKind.Cover, "封面", "cover", paragraph);
                    AddParagraph(current, paragraph, result, noteContext, role, 0.75);
                    break;
                case "declaration":
                    current = AddSection(result, ThesisSectionKind.OriginalityStatement, text, "declaration", paragraph);
                    AddHeading(current, paragraph, result, noteContext, 1, 0.85);
                    break;
                case "abstractZh":
                case "abstractEn":
                    current = AddSection(result, ThesisSectionKind.Abstract, text, $"abstract-{result.Document.Sections.Count}", paragraph);
                    AddHeading(current, paragraph, result, noteContext, 1, 0.9);
                    break;
                case "toc":
                    current = AddSection(result, ThesisSectionKind.Toc, text, "toc", paragraph);
                    AddHeading(current, paragraph, result, noteContext, 1, 0.9);
                    break;
                case "bibliography":
                    if (current.Kind != ThesisSectionKind.Bibliography)
                    {
                        current = AddSection(result, ThesisSectionKind.Bibliography, text, "bibliography", paragraph);
                        AddHeading(current, paragraph, result, noteContext, 1, 0.9);
                    }
                    break;
                case "acknowledgements":
                    current = AddSection(result, ThesisSectionKind.Acknowledgements, text, "acknowledgements", paragraph);
                    AddHeading(current, paragraph, result, noteContext, 1, 0.9);
                    break;
                case "appendix":
                    current = AddSection(result, ThesisSectionKind.Appendix, text, "appendix", paragraph);
                    AddHeading(current, paragraph, result, noteContext, 1, 0.9);
                    break;
                case "heading":
                    if (!result.Document.Sections.Contains(current))
                    {
                        result.Document.Sections.Add(current);
                    }
                    AddHeading(current, paragraph, result, noteContext, GuessHeadingLevel(paragraph, text), 0.8);
                    break;
                case "bibliographyItem":
                    if (current.Kind != ThesisSectionKind.Bibliography)
                    {
                        current = AddSection(result, ThesisSectionKind.Bibliography, "参考文献", "bibliography", paragraph);
                    }
                    bibliographyEntries.Add(new BibliographyEntryNode { Id = $"bib{bibliographyEntries.Count + 1}", Text = text });
                    AddEvidence(result, $"$.sections[{result.Document.Sections.IndexOf(current)}].blocks[bibliography].entries[{bibliographyEntries.Count - 1}]", paragraph.EvidencePath, "bibliography item", 0.8);
                    break;
                default:
                    if (!result.Document.Sections.Contains(current))
                    {
                        result.Document.Sections.Add(current);
                    }
                    AddParagraph(current, paragraph, result, noteContext, "body paragraph", 0.7);
                    if (paragraph.PossibleRole == "headingCandidate")
                    {
                        AddUnresolved(result, "structure.headingLevel.unconfirmed", "Possible heading requires review.", paragraph.EvidencePath, "Confirm heading level and section boundary.");
                    }
                    break;
            }

            AddFiguresForEvidence(result, current, figuresByEvidencePath, mappedFigureIds, paragraph.EvidencePath);
        }

        foreach (var figure in extraction.Figures.Where(figure => !mappedFigureIds.Contains(figure.Id)))
        {
            if (!result.Document.Sections.Contains(current))
            {
                result.Document.Sections.Add(current);
            }

            AddFigure(current, figure, result, mappedFigureIds, "figure extracted from drawing evidence without a nearby non-empty paragraph", 0.65);
        }

        foreach (var table in extraction.Tables)
        {
            if (!result.Document.Sections.Contains(current))
            {
                result.Document.Sections.Add(current);
            }

            current.Blocks.Add(new TableBlock
            {
                Id = SafeId($"table{table.Index + 1}"),
                Caption = $"表 {table.Index + 1}",
                CaptionPosition = CaptionPosition.Before,
                Rows = table.Rows.Select(row => new TableRowNode
                {
                    Cells = row.Cells.Select(cell => new TableCellNode { Text = cell.Text, GridSpan = Math.Max(1, cell.GridSpan) }).ToList()
                }).ToList()
            });
            AddEvidence(result, $"$.sections[{result.Document.Sections.IndexOf(current)}].blocks[{current.Blocks.Count - 1}]", table.EvidencePath, "table structure copied", 0.8);
        }

        if (bibliographyEntries.Count > 0)
        {
            var bibliography = result.Document.Sections.FirstOrDefault(s => s.Kind == ThesisSectionKind.Bibliography) ?? AddSection(result, ThesisSectionKind.Bibliography, "参考文献", "bibliography", null);
            bibliography.Blocks.Add(new BibliographyBlock { Id = "bibliographyEntries", Entries = bibliographyEntries });
        }

        if (result.Document.Sections.Count == 0)
        {
            result.Document.Sections.Add(NewSection(ThesisSectionKind.Body, "正文", "body"));
        }

        EnsureRendererScaffoldSections(result);

        var contentPreservation = new ContentPreservationAuditor().AuditDraft(extraction, result.Document);
        result.Report = new ThesisStructureMappingReport
        {
            SourceExtraction = sourceExtraction,
            RuleBasedMappedCount = result.EvidenceLinks.Count,
            UnresolvedCount = result.UnresolvedItems.Count,
            LowConfidenceCount = result.EvidenceLinks.Count(link => link.Confidence < 0.75),
            EvidenceCoverageRatio = extraction.Paragraphs.Count == 0 ? 0 : Math.Round(result.EvidenceLinks.Select(l => l.EvidencePath).Distinct().Count() / (double)extraction.Paragraphs.Count, 6),
            ContentPreservation = contentPreservation,
            EvidenceLinks = result.EvidenceLinks,
            RecommendedCodexReviewSteps =
            [
                "Review unresolved metadata placeholders before rendering a final document.",
                "Confirm heading levels and section boundaries against extraction evidence.",
                "Verify bibliography item boundaries and citation markers.",
                "Do not rewrite body text; only adjust structure mapping."
            ]
        };
        result.Report.Warnings.AddRange(result.UnresolvedItems.Select(item => $"{item.Code}: {item.Message}").Order(StringComparer.Ordinal));
        result.Report.Warnings.AddRange(contentPreservation.Warnings.Select(issue => $"{issue.Code}: {issue.Message}").Order(StringComparer.Ordinal));
        result.Report.BlockingIssues.AddRange(contentPreservation.BlockingIssues.Select(issue => $"{issue.Code}: {issue.Message}").Order(StringComparer.Ordinal));
        return result;
    }

    private static NoteReferenceContext BuildNoteContext(DocxExtractionResult extraction)
    {
        return new NoteReferenceContext(
            extraction.Footnotes
                .Where(note => !string.IsNullOrWhiteSpace(note.NoteId))
                .GroupBy(note => note.NoteId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal),
            extraction.Endnotes
                .Where(note => !string.IsNullOrWhiteSpace(note.NoteId))
                .GroupBy(note => note.NoteId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal));
    }

    private static void EnsureRendererScaffoldSections(ThesisStructuringResult result)
    {
        if (result.Document.Sections.All(section => section.Kind != ThesisSectionKind.Cover))
        {
            result.Document.Sections.Insert(0, new ThesisSection
            {
                Id = "cover",
                Kind = ThesisSectionKind.Cover,
                Title = "封面",
                StartOnNewPage = true,
                Blocks = [new ParagraphBlock { Id = "coverPlaceholder", Inlines = [new TextInline { Text = result.Document.Metadata.Title }] }]
            });
            AddUnresolved(result, "section.cover.synthesized", "Cover section was synthesized from metadata for template rendering.", "document", "Review cover metadata fields before final rendering.");
        }

        if (result.Document.Sections.All(section => section.Kind != ThesisSectionKind.Toc))
        {
            var insertIndex = Math.Min(result.Document.Sections.Count, result.Document.Sections.FindIndex(section => section.Kind == ThesisSectionKind.Body) is var bodyIndex && bodyIndex >= 0 ? bodyIndex : result.Document.Sections.Count);
            result.Document.Sections.Insert(insertIndex, new ThesisSection
            {
                Id = "toc",
                Kind = ThesisSectionKind.Toc,
                Title = "目录",
                StartOnNewPage = true,
                Blocks = [new HeadingBlock { Id = "tocHeading", Level = 1, Numbered = false, Inlines = [new TextInline { Text = "目录" }] }]
            });
            AddUnresolved(result, "section.toc.synthesized", "TOC section was synthesized so Word can update a TOC field after rendering.", "document", "Review whether the original document contained a table of contents.");
        }
    }

    public void WriteOutputs(ThesisStructuringResult result, string documentPath, string reportPath, string unresolvedPath, string? evidencePath = null)
    {
        WriteJson(documentPath, result.Document);
        WriteJson(reportPath, result.Report);
        WriteJson(unresolvedPath, result.UnresolvedItems);
        if (!string.IsNullOrWhiteSpace(evidencePath))
        {
            WriteJson(evidencePath, result.EvidenceLinks);
        }
    }

    private static ThesisMetadata BuildMetadata(DocxExtractionResult extraction, ThesisStructuringResult result)
    {
        var nonEmpty = extraction.Paragraphs.Select(p => p.Text.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        var title = ParseTitleFromFileName(extraction.InputFileName) ?? nonEmpty.FirstOrDefault() ?? "UNKNOWN_TITLE";
        var author = ParseAuthorFromFileName(extraction.InputFileName) ?? "UNKNOWN_AUTHOR";
        if (author.StartsWith("UNKNOWN", StringComparison.Ordinal))
        {
            AddUnresolved(result, "metadata.author.unresolved", "Author could not be confirmed from extraction evidence.", "document", "Review cover page metadata.");
        }

        foreach (var field in new[] { "college", "major", "studentId", "advisor", "date" })
        {
            AddUnresolved(result, $"metadata.{field}.unresolved", $"Metadata field '{field}' was not confidently identified.", "document", "Review cover page or filename evidence.");
        }

        return new ThesisMetadata
        {
            Title = title,
            Author = author,
            College = "UNKNOWN_COLLEGE",
            Major = "UNKNOWN_MAJOR",
            StudentId = "UNKNOWN_STUDENT_ID",
            Advisor = "UNKNOWN_ADVISOR",
            Date = "UNKNOWN_DATE",
            Language = "zh-CN"
        };
    }

    private static string? ParseTitleFromFileName(string fileName)
    {
        var match = Regex.Match(fileName, @"《(?<title>[^》]+)》", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["title"].Value : null;
    }

    private static string? ParseAuthorFromFileName(string fileName)
    {
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var parts = withoutExtension.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 ? Regex.Replace(parts[^1], @"\([0-9]+\)$", string.Empty) : null;
    }

    private static string Classify(string text, ExtractedParagraph paragraph)
    {
        if (Regex.IsMatch(text, @"^(独创性声明|原创性声明|论文使用授权)", RegexOptions.CultureInvariant)) return "declaration";
        if (Regex.IsMatch(text, @"^(摘要|内容摘要)\s*$", RegexOptions.CultureInvariant)) return "abstractZh";
        if (Regex.IsMatch(text, @"^(ABSTRACT|Abstract)\s*$", RegexOptions.CultureInvariant)) return "abstractEn";
        if (Regex.IsMatch(text, @"^目\s*录$|^目录$", RegexOptions.CultureInvariant)) return "toc";
        if (Regex.IsMatch(text, @"^(参考文献|References)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return "bibliography";
        if (Regex.IsMatch(text, @"^(致谢语|致谢)\s*$", RegexOptions.CultureInvariant)) return "acknowledgements";
        if (Regex.IsMatch(text, @"^(附录|Appendix)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return "appendix";
        if (Regex.IsMatch(text, @"^\[[0-9]+\]")) return "bibliographyItem";
        if (Regex.IsMatch(text, @"^(引言|绪论|Introduction)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return "heading";
        if (paragraph.PossibleRole == "headingCandidate") return "heading";
        return "paragraph";
    }

    private static int GuessHeadingLevel(ExtractedParagraph paragraph, string text)
    {
        var outlineLevel = paragraph.OutlineLevel ?? paragraph.EffectiveFormat.OutlineLevel;
        if (outlineLevel is >= 0 and <= 5) return outlineLevel.Value + 1;
        if (Regex.IsMatch(text, @"^[0-9]+\.[0-9]+\.[0-9]+")) return 3;
        if (Regex.IsMatch(text, @"^[0-9]+\.[0-9]+")) return 2;
        return 1;
    }

    private static ThesisSection AddSection(ThesisStructuringResult result, ThesisSectionKind kind, string? title, string id, ExtractedParagraph? paragraph)
    {
        var existing = result.Document.Sections.FirstOrDefault(s => s.Id == id);
        if (existing is not null)
        {
            return existing;
        }

        var section = NewSection(kind, title, id);
        result.Document.Sections.Add(section);
        if (paragraph is not null)
        {
            AddEvidence(result, $"$.sections[{result.Document.Sections.Count - 1}]", paragraph.EvidencePath, $"section classified as {kind}", 0.85);
        }
        return section;
    }

    private static ThesisSection NewSection(ThesisSectionKind kind, string? title, string id)
    {
        return new ThesisSection { Id = SafeId(id), Kind = kind, Title = title, StartOnNewPage = kind != ThesisSectionKind.Body };
    }

    private static void AddHeading(ThesisSection section, ExtractedParagraph paragraph, ThesisStructuringResult result, NoteReferenceContext noteContext, int level, double confidence)
    {
        section.Blocks.Add(new HeadingBlock
        {
            Id = SafeId($"heading{paragraph.Index}"),
            Level = Math.Clamp(level, 1, 6),
            Inlines = ToInlines(paragraph, noteContext, result),
            Numbered = Regex.IsMatch(paragraph.Text, @"^(第|[0-9])", RegexOptions.CultureInvariant)
        });
        AddEvidence(result, $"$.sections[{result.Document.Sections.IndexOf(section)}].blocks[{section.Blocks.Count - 1}]", paragraph.EvidencePath, "heading mapped", confidence);
    }

    private static void AddParagraph(ThesisSection section, ExtractedParagraph paragraph, ThesisStructuringResult result, NoteReferenceContext noteContext, string reason, double confidence)
    {
        section.Blocks.Add(new ParagraphBlock
        {
            Id = SafeId($"paragraph{paragraph.Index}"),
            Inlines = ToInlines(paragraph, noteContext, result)
        });
        AddEvidence(result, $"$.sections[{result.Document.Sections.IndexOf(section)}].blocks[{section.Blocks.Count - 1}]", paragraph.EvidencePath, reason, confidence);
    }

    private static void AddFiguresForEvidence(
        ThesisStructuringResult result,
        ThesisSection current,
        IReadOnlyDictionary<string, List<ExtractedFigure>> figuresByEvidencePath,
        HashSet<string> mappedFigureIds,
        string evidencePath)
    {
        if (!figuresByEvidencePath.TryGetValue(evidencePath, out var figures))
        {
            return;
        }

        foreach (var figure in figures)
        {
            AddFigure(current, figure, result, mappedFigureIds, "figure mapped from paragraph drawing evidence", 0.8);
        }
    }

    private static void AddFigure(
        ThesisSection section,
        ExtractedFigure figure,
        ThesisStructuringResult result,
        HashSet<string> mappedFigureIds,
        string reason,
        double confidence)
    {
        if (!mappedFigureIds.Add(figure.Id))
        {
            return;
        }

        section.Blocks.Add(new FigureBlock
        {
            Id = SafeId($"figure{figure.Index + 1}"),
            Caption = string.IsNullOrWhiteSpace(figure.SuggestedCaption) ? $"图 {figure.Index + 1}" : figure.SuggestedCaption!,
            ImagePath = figure.ArtifactPath,
            ImageContentType = SupportedImageContentType(figure.ContentType) ? figure.ContentType! : "image/png"
        });
        AddEvidence(result, $"$.sections[{result.Document.Sections.IndexOf(section)}].blocks[{section.Blocks.Count - 1}]", figure.EvidencePath, reason, confidence);
        if (string.IsNullOrWhiteSpace(figure.ArtifactPath))
        {
            AddUnresolved(result, "figure.artifact.missing", "Figure drawing was extracted but no image artifact path was available.", figure.EvidencePath, "Review the source DOCX image relationship and decide whether to attach an image manually.");
        }
    }

    private static List<InlineNode> ToInlines(ExtractedParagraph paragraph, NoteReferenceContext noteContext, ThesisStructuringResult result)
    {
        var runs = paragraph.Runs.Where(r => !string.IsNullOrEmpty(r.Text)).ToList();
        if (runs.Count == 0)
        {
            return [new TextInline { Text = paragraph.Text }];
        }

        return runs.Select(run => ToInline(run, paragraph, noteContext, result)).ToList();
    }

    private static InlineNode ToInline(ExtractedRun run, ExtractedParagraph paragraph, NoteReferenceContext noteContext, ThesisStructuringResult result)
    {
        var footnoteMarker = FootnoteMarkerPattern.Match(run.Text);
        if (footnoteMarker.Success)
        {
            var noteId = footnoteMarker.Groups["id"].Value;
            if (noteContext.FootnotesById.TryGetValue(noteId, out var note) && !string.IsNullOrWhiteSpace(note.Text))
            {
                return new FootnoteInline
                {
                    NoteId = SafeId($"fn{noteId}"),
                    Inlines = [new TextInline { Text = note.Text }]
                };
            }

            AddUnresolved(result, "note.footnote.contentMissing", $"Footnote reference '{noteId}' has no extracted note content.", paragraph.EvidencePath, "Review the source note part and attach the note content manually if needed.");
            return TextFromRun(run);
        }

        var endnoteMarker = EndnoteMarkerPattern.Match(run.Text);
        if (endnoteMarker.Success)
        {
            var noteId = endnoteMarker.Groups["id"].Value;
            if (noteContext.EndnotesById.TryGetValue(noteId, out var note) && !string.IsNullOrWhiteSpace(note.Text))
            {
                return new EndnoteInline
                {
                    NoteId = SafeId($"en{noteId}"),
                    Inlines = [new TextInline { Text = note.Text }]
                };
            }

            AddUnresolved(result, "note.endnote.contentMissing", $"Endnote reference '{noteId}' has no extracted note content.", paragraph.EvidencePath, "Review the source note part and attach the note content manually if needed.");
            return TextFromRun(run);
        }

        return TextFromRun(run);
    }

    private static TextInline TextFromRun(ExtractedRun run)
    {
        return new TextInline
        {
            Text = run.Text,
            Bold = run.Bold,
            Italic = run.Italic,
            Underline = run.Underline,
            VerticalAlignment = run.Superscript ? VerticalAlignment.Superscript : run.Subscript ? VerticalAlignment.Subscript : VerticalAlignment.Baseline
        };
    }

    private static void AddEvidence(ThesisStructuringResult result, string structuredPath, string evidencePath, string reason, double confidence)
    {
        result.EvidenceLinks.Add(new ThesisStructureEvidenceLink
        {
            StructuredPath = structuredPath,
            EvidencePath = evidencePath,
            Reason = reason,
            Confidence = confidence
        });
    }

    private static void AddUnresolved(ThesisStructuringResult result, string code, string message, string evidencePath, string action)
    {
        if (result.UnresolvedItems.Any(item => item.Code == code && item.EvidencePath == evidencePath))
        {
            return;
        }

        result.UnresolvedItems.Add(new ThesisStructureUnresolvedItem
        {
            Id = $"unresolved-{result.UnresolvedItems.Count + 1}",
            Code = code,
            Severity = "warning",
            Message = message,
            EvidencePath = evidencePath,
            RecommendedAction = action
        });
    }

    private static bool SupportedImageContentType(string? contentType)
    {
        return contentType is "image/png" or "image/jpeg" or "image/jpg" or "image/gif" or "image/bmp" or "image/tiff";
    }

    private static string SafeId(string value)
    {
        var id = Regex.Replace(value, @"[^A-Za-z0-9_.-]", "_");
        if (string.IsNullOrWhiteSpace(id) || !char.IsLetter(id[0]))
        {
            id = "id_" + id;
        }

        return id;
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory());
        File.WriteAllText(path, JsonSerializer.Serialize(value, ThesisJson.Options));
    }

    private sealed record NoteReferenceContext(
        IReadOnlyDictionary<string, ExtractedFootnote> FootnotesById,
        IReadOnlyDictionary<string, ExtractedEndnote> EndnotesById);
}

public sealed class ThesisDocumentDraftValidator
{
    public ThesisDocumentDraftValidationResult Validate(string documentPath, string schemaPath)
    {
        var schema = new ThesisSchemaValidator().ValidateDocumentFile(documentPath, schemaPath);
        return new ThesisDocumentDraftValidationResult
        {
            IsValid = schema.IsValid,
            Errors = schema.Errors.Select(e => e.ToString()).Order(StringComparer.Ordinal).ToList(),
            Warnings = schema.Warnings.Select(e => e.ToString()).Order(StringComparer.Ordinal).ToList()
        };
    }
}
