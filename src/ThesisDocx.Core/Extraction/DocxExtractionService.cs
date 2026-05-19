using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ThesisDocx.Core.Utilities;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace ThesisDocx.Core.Extraction;

public sealed class DocxExtractionService
{
    private const int MaxPreservedObjectParts = 32;
    private const long MaxPreservedObjectPartBytes = 8 * 1024 * 1024;

    public DocxExtractionResult Extract(DocxExtractionOptions options)
    {
        ValidateOptions(options);
        using var document = OpenDocument(options.InputPath);
        var main = document.MainDocumentPart ?? throw Error("intake.docx.missingMainDocumentPart", "$.input", "DOCX package has no main document part.", "Use a valid WordprocessingML .docx file.");
        var body = main.Document.Body ?? throw Error("intake.docx.missingBody", "$.input", "DOCX main document has no body.", "Use a valid WordprocessingML .docx file with document body content.");
        var styleNames = LoadStyleNames(main);
        var formatResolver = new DocxEffectiveFormatResolver(main, styleNames);
        var artifactImageDirectory = ResolveImageDirectory(options);
        var result = new DocxExtractionResult
        {
            InputFileName = Path.GetFileName(options.InputPath)
        };

        var paragraphIndex = 0;
        var tableIndex = 0;
        var figureIndex = 0;
        var drawingObjectIndex = 0;
        var blockIndex = 0;
        var previousTexts = new Queue<string>();
        foreach (var element in body.Elements())
        {
            switch (element)
            {
                case Paragraph paragraph:
                    var extracted = ExtractParagraph(paragraph, paragraphIndex, styleNames, formatResolver, main);
                    result.Paragraphs.Add(extracted);
                    result.Blocks.Add(new ExtractedBlock
                    {
                        Id = $"block-{blockIndex}",
                        Type = "paragraph",
                        Index = blockIndex,
                        EvidencePath = extracted.EvidencePath
                    });
                    ExtractParagraphChildren(paragraph, extracted, result, main, previousTexts, ref figureIndex, ref drawingObjectIndex, artifactImageDirectory);
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
                    var extractedTable = ExtractTable(table, tableIndex, result, main, previousTexts, ref figureIndex, ref drawingObjectIndex, artifactImageDirectory);
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

        LinkNearbyCaptions(result);
        foreach (var paragraph in result.Paragraphs.Where(p => p.Text.StartsWith("图", StringComparison.Ordinal) || p.Text.StartsWith("表", StringComparison.Ordinal)))
        {
            result.PossibleCaptions.Add(Evidence($"caption-{result.PossibleCaptions.Count}", paragraph.EvidencePath, paragraph.Text, "caption pattern", 0.85));
        }

        ExtractFootnotes(main, result);
        ExtractEndnotes(main, result);
        SummarizeStylesAndNumbering(result);
        SummarizeFormatSignatures(result);
        var formatAnalysis = new DocxFormatChaosAnalyzer().Analyze(result);
        result.FormatChaos = formatAnalysis.Report;
        result.FormatClusters = formatAnalysis.Clusters;
        result.ExtractionIssues.AddRange(formatAnalysis.Report.Diagnostics);
        result.PlainText = string.Join(Environment.NewLine, result.Paragraphs.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
        result.Document = new ExtractedDocument
        {
            ParagraphCount = result.Paragraphs.Count,
            TableCount = result.Tables.Count,
            FigureCount = result.Figures.Count,
            DrawingObjectCount = result.DrawingObjects.Count,
            FootnoteCount = result.Footnotes.Count,
            EndnoteCount = result.Endnotes.Count
        };
        result.Sections.AddRange(body.Descendants<SectionProperties>().Select((sectPr, index) => ExtractSection(sectPr, result.Sections.Count + index)));
        result.Sections = result.Sections.GroupBy(s => s.EvidencePath, StringComparer.Ordinal).Select(g => g.First()).ToList();

        WriteOutputs(options, result);
        return result;
    }

    private static WordprocessingDocument OpenDocument(string inputPath)
    {
        try
        {
            return WordprocessingDocument.Open(inputPath, false);
        }
        catch (OpenXmlPackageException ex)
        {
            throw Error("intake.docx.invalidPackage", "$.input", "Input is not a valid OpenXML Wordprocessing package.", "Export or save the source as a valid .docx file.", ex);
        }
        catch (InvalidDataException ex)
        {
            throw Error("intake.docx.invalidPackage", "$.input", "Input is not a valid .docx ZIP package.", "Use a valid .docx file instead of a renamed or corrupted file.", ex);
        }
    }

    private static void ValidateOptions(DocxExtractionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            throw Error("intake.input.missing", "$.input", "Input DOCX path is required.", "Pass --input or --docx with a valid .docx file.");
        }

        if (!File.Exists(options.InputPath))
        {
            throw Error("intake.input.notFound", "$.input", "Input DOCX file does not exist.", "Check the input path and rerun extraction.");
        }

        if (!Path.GetExtension(options.InputPath).Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            throw Error("intake.input.notDocx", "$.input", "Input file must have a .docx extension.", "Use a .docx file exported by Word or a compatible editor.");
        }

        var fileInfo = new FileInfo(options.InputPath);
        if (fileInfo.Length == 0)
        {
            throw Error("intake.input.empty", "$.input", "Input DOCX file is empty.", "Provide a non-empty .docx file.");
        }

        if (fileInfo.Length > options.MaxInputBytes)
        {
            throw Error("intake.input.tooLarge", "$.input", "Input DOCX file exceeds the configured extraction size limit.", "Reduce embedded media or raise the extraction limit in a controlled workspace.");
        }

        ValidateOutputPath(options.WorkspaceRoot, options.OutputJsonPath, "$.out");
        ValidateOutputPath(options.WorkspaceRoot, options.PlainTextPath, "$.text");
        ValidateOutputPath(options.WorkspaceRoot, options.MarkdownPath, "$.markdown");
        ValidateOutputPath(options.WorkspaceRoot, options.ArtifactsDirectory, "$.artifacts");
        ValidateZipPackage(options);
    }

    private static void ValidateZipPackage(DocxExtractionOptions options)
    {
        ZipArchive archive;
        try
        {
            archive = ZipFile.OpenRead(options.InputPath);
        }
        catch (InvalidDataException ex)
        {
            throw Error("intake.docx.invalidZip", "$.input", "Input is not a valid ZIP-based .docx package.", "Use a valid .docx file instead of a renamed or corrupted file.", ex);
        }

        using (archive)
        {
            if (archive.Entries.Count > options.MaxZipEntryCount)
            {
                throw Error("intake.docx.tooManyEntries", "$.input", "DOCX package contains too many ZIP entries.", "Remove excessive embedded content or split the document.");
            }

            long uncompressedBytes = 0;
            var hasMainDocument = false;
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (Path.IsPathRooted(name) || name.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
                {
                    throw Error("intake.docx.pathTraversal", "$.input", "DOCX package contains a ZIP entry that escapes the package root.", "Reject the package and regenerate it from a trusted .docx source.");
                }

                if (name.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                {
                    hasMainDocument = true;
                }

                uncompressedBytes += entry.Length;
                if (uncompressedBytes > options.MaxUncompressedBytes)
                {
                    throw Error("intake.docx.uncompressedTooLarge", "$.input", "DOCX package expands beyond the configured extraction limit.", "Reduce embedded content or inspect the file in a private workspace.");
                }

                if (entry.CompressedLength > 0 && entry.Length / (double)entry.CompressedLength > options.MaxCompressionRatio)
                {
                    throw Error("intake.docx.compressionRatioTooHigh", "$.input", "DOCX package has an unsafe compression ratio.", "Reject the file or inspect it manually in an isolated workspace.");
                }

                if (IsRelationshipPart(name))
                {
                    ValidateRelationshipPart(entry, name);
                }
            }

            if (!hasMainDocument)
            {
                throw Error("intake.docx.missingDocumentEntry", "$.input", "DOCX package is missing word/document.xml.", "Use a valid WordprocessingML .docx file.");
            }
        }
    }

    private static bool IsRelationshipPart(string name)
    {
        return name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)
            && name.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("_rels", StringComparer.Ordinal);
    }

    private static void ValidateRelationshipPart(ZipArchiveEntry entry, string relationshipEntryName)
    {
        XDocument relationships;
        try
        {
            using var stream = entry.Open();
            relationships = XDocument.Load(stream, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw Error("intake.docx.invalidRelationships", "$.input", "DOCX package contains an invalid relationship part.", "Regenerate the DOCX from a trusted editor before intake.", ex);
        }

        foreach (var relationship in relationships.Descendants().Where(element => element.Name.LocalName == "Relationship"))
        {
            var target = relationship.Attribute("Target")?.Value;
            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            var targetMode = relationship.Attribute("TargetMode")?.Value;
            if (string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsAllowedExternalRelationshipTarget(target))
                {
                    throw Error("intake.docx.externalRelationshipUnsafe", "$.input", "DOCX package contains an unsafe external relationship target.", "Remove file, UNC, or local external relationship targets before intake.");
                }

                continue;
            }

            if (!TryResolvePackageRelationshipTarget(relationshipEntryName, target, out _))
            {
                throw Error("intake.docx.relationshipTargetInvalid", "$.input", "DOCX package contains a relationship target that escapes the package root.", "Regenerate the DOCX from a trusted editor and remove path traversal relationship targets.");
            }
        }
    }

    private static bool IsAllowedExternalRelationshipTarget(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https" or "mailto";
    }

    private static bool TryResolvePackageRelationshipTarget(string relationshipEntryName, string target, out string packagePath)
    {
        packagePath = string.Empty;
        var normalizedTarget = target.Replace('\\', '/').Trim();
        if (normalizedTarget.Length == 0
            || normalizedTarget.StartsWith("//", StringComparison.Ordinal)
            || normalizedTarget.Contains(':', StringComparison.Ordinal)
            || (!normalizedTarget.StartsWith("/", StringComparison.Ordinal) && Uri.TryCreate(normalizedTarget, UriKind.Absolute, out _)))
        {
            return false;
        }

        var combined = normalizedTarget.StartsWith("/", StringComparison.Ordinal)
            ? normalizedTarget.TrimStart('/')
            : CombinePackagePath(SourceDirectoryForRelationshipPart(relationshipEntryName), normalizedTarget);

        var segments = new List<string>();
        foreach (var segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return false;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        packagePath = string.Join('/', segments);
        return packagePath.Length > 0;
    }

    private static string SourceDirectoryForRelationshipPart(string relationshipEntryName)
    {
        var name = relationshipEntryName.Replace('\\', '/');
        if (name.Equals("_rels/.rels", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var marker = "/_rels/";
        var markerIndex = name.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return string.Empty;
        }

        var sourcePart = name[..markerIndex] + "/" + name[(markerIndex + marker.Length)..^".rels".Length];
        var slash = sourcePart.LastIndexOf('/');
        return slash < 0 ? string.Empty : sourcePart[..slash];
    }

    private static string CombinePackagePath(string baseDirectory, string target)
    {
        return string.IsNullOrWhiteSpace(baseDirectory) ? target : $"{baseDirectory}/{target}";
    }

    private static void ValidateOutputPath(string? workspaceRoot, string? path, string optionPath)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var root = Path.GetFullPath(workspaceRoot);
        var fullPath = Path.GetFullPath(path);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!fullPath.Equals(root, StringComparison.Ordinal) && !fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw Error("intake.output.pathTraversal", optionPath, "Extraction output path escapes the configured workspace.", "Keep extraction outputs inside the private intake workspace.");
        }
    }

    private static DocxExtractionException Error(string code, string path, string message, string fixHint, Exception? innerException = null)
    {
        return new DocxExtractionException(code, path, message, fixHint, innerException);
    }

    private static ExtractedParagraph ExtractParagraph(Paragraph paragraph, int index, Dictionary<string, string> styleNames, DocxEffectiveFormatResolver formatResolver, MainDocumentPart main)
    {
        var pPr = paragraph.ParagraphProperties;
        var styleId = pPr?.ParagraphStyleId?.Val?.Value;
        var runs = new List<ExtractedRun>();
        var footnoteReferenceIds = new List<string>();
        var endnoteReferenceIds = new List<string>();
        var runIndex = 0;
        foreach (var child in paragraph.ChildElements)
        {
            switch (child)
            {
                case Run run:
                    AddRunWithReferences(run, $"p{index}-r{runIndex}", runs, footnoteReferenceIds, endnoteReferenceIds);
                    runIndex++;
                    break;
                case Hyperlink hyperlink:
                    var relId = hyperlink.Id?.Value;
                    var uri = ResolveHyperlinkUri(main, relId);
                    foreach (var linkedRun in hyperlink.Elements<Run>())
                    {
                        AddRunWithReferences(linkedRun, $"p{index}-h{runIndex}", runs, footnoteReferenceIds, endnoteReferenceIds, relId, uri);
                        runIndex++;
                    }
                    break;
            }
        }

        var text = string.Concat(runs.Select(r => r.Text));
        var outlineRaw = pPr?.OutlineLevel?.Val?.Value;
        var numId = pPr?.NumberingProperties?.NumberingId?.Val?.Value.ToString();
        var ilvlRaw = pPr?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
        var extracted = new ExtractedParagraph
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
            FootnoteReferenceIds = footnoteReferenceIds,
            EndnoteReferenceIds = endnoteReferenceIds,
            EvidencePath = $"paragraphs[{index}]"
        };
        extracted.EffectiveFormat = formatResolver.Resolve(paragraph, extracted);
        return extracted;
    }

    private static void AddRunWithReferences(
        Run run,
        string id,
        List<ExtractedRun> runs,
        List<string> footnoteReferenceIds,
        List<string> endnoteReferenceIds,
        string? hyperlinkRelationshipId = null,
        string? hyperlinkUri = null)
    {
        var extractedRun = ExtractRun(run, id);
        extractedRun.HyperlinkRelationshipId = hyperlinkRelationshipId;
        extractedRun.HyperlinkUri = hyperlinkUri;
        if (!string.IsNullOrEmpty(extractedRun.Text))
        {
            runs.Add(extractedRun);
        }

        foreach (var reference in run.Descendants<FootnoteReference>())
        {
            var noteId = reference.Id?.Value.ToString();
            if (string.IsNullOrWhiteSpace(noteId))
            {
                continue;
            }

            footnoteReferenceIds.Add(noteId);
            runs.Add(new ExtractedRun { Id = $"{id}-fn{noteId}", Text = $"[^fn{noteId}]", Superscript = true });
        }

        foreach (var reference in run.Descendants<EndnoteReference>())
        {
            var noteId = reference.Id?.Value.ToString();
            if (string.IsNullOrWhiteSpace(noteId))
            {
                continue;
            }

            endnoteReferenceIds.Add(noteId);
            runs.Add(new ExtractedRun { Id = $"{id}-en{noteId}", Text = $"[^en{noteId}]", Superscript = true });
        }
    }

    private static string? ResolveHyperlinkUri(MainDocumentPart main, string? relId)
    {
        return relId is null ? null : main.HyperlinkRelationships.FirstOrDefault(r => r.Id == relId)?.Uri.ToString();
    }

    private static ExtractedRun ExtractRun(Run run, string id)
    {
        var rPr = run.RunProperties;
        var vertical = rPr?.VerticalTextAlignment?.Val?.Value;
        var fontSize = rPr?.FontSize?.Val?.Value;
        return new ExtractedRun
        {
            Id = id,
            Text = NormalizeText(string.Concat(run.Descendants<Text>().Where(text => !text.Ancestors<TextBoxContent>().Any()).Select(t => t.Text))),
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

    private static List<string> ExtractParagraphChildren(
        Paragraph paragraph,
        ExtractedParagraph extracted,
        DocxExtractionResult result,
        MainDocumentPart main,
        Queue<string> previousTexts,
        ref int figureIndex,
        ref int drawingObjectIndex,
        string imageDirectory)
    {
        var figureIds = new List<string>();
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
                Uri = ResolveHyperlinkUri(main, relId),
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
            if (relId is not null && FindImagePart(main, relId) is ImagePart imagePart)
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

            var figureId = $"figure-{figureIndex}";
            var size = DrawingSizeCm(blip);
            result.Figures.Add(new ExtractedFigure
            {
                Id = figureId,
                Index = figureIndex,
                RelationshipId = relId,
                ContentType = contentType,
                ArtifactPath = artifactPath,
                WidthCm = size.WidthCm,
                HeightCm = size.HeightCm,
                AnchorType = size.AnchorType,
                Crop = ExtractCrop(blip),
                SuggestedCaption = GuessNearbyCaption(extracted.Text),
                NearbyText = string.Join(" / ", previousTexts.Append(extracted.Text).Where(t => !string.IsNullOrWhiteSpace(t))),
                EvidencePath = extracted.EvidencePath
            });
            figureIds.Add(figureId);
            figureIndex++;
        }

        ExtractDrawingObjectEvidence(paragraph, extracted.EvidencePath, result, main, ref drawingObjectIndex);
        return figureIds;
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

    private static ImagePart? FindImagePart(MainDocumentPart main, string relationshipId)
    {
        return main.Parts.FirstOrDefault(part => string.Equals(part.RelationshipId, relationshipId, StringComparison.Ordinal)).OpenXmlPart as ImagePart;
    }

    private static (double? WidthCm, double? HeightCm, string? AnchorType) DrawingSizeCm(OpenXmlElement element)
    {
        var inline = element.Descendants<WP.Inline>().FirstOrDefault() ?? element.Ancestors<WP.Inline>().FirstOrDefault();
        var inlineExtent = inline?.Extent;
        if (inlineExtent?.Cx is not null && inlineExtent.Cy is not null)
        {
            return (
                Math.Round(UnitConverter.EmuToCentimeters(inlineExtent.Cx.Value), 3),
                Math.Round(UnitConverter.EmuToCentimeters(inlineExtent.Cy.Value), 3),
                "inline");
        }

        var anchor = element.Descendants<WP.Anchor>().FirstOrDefault() ?? element.Ancestors<WP.Anchor>().FirstOrDefault();
        var anchorExtent = anchor?.Extent;
        if (anchorExtent?.Cx is not null && anchorExtent.Cy is not null)
        {
            return (
                Math.Round(UnitConverter.EmuToCentimeters(anchorExtent.Cx.Value), 3),
                Math.Round(UnitConverter.EmuToCentimeters(anchorExtent.Cy.Value), 3),
                "anchor");
        }

        return (null, null, null);
    }

    private static ExtractedFigureCrop? ExtractCrop(A.Blip blip)
    {
        var source = blip.Ancestors<PIC.BlipFill>().FirstOrDefault()?.GetFirstChild<A.SourceRectangle>();
        if (source is null)
        {
            return null;
        }

        var crop = new ExtractedFigureCrop
        {
            LeftPercent = DrawingPercent(source.Left?.Value),
            TopPercent = DrawingPercent(source.Top?.Value),
            RightPercent = DrawingPercent(source.Right?.Value),
            BottomPercent = DrawingPercent(source.Bottom?.Value)
        };
        return crop.LeftPercent.HasValue || crop.TopPercent.HasValue || crop.RightPercent.HasValue || crop.BottomPercent.HasValue
            ? crop
            : null;
    }

    private static double? DrawingPercent(int? raw)
    {
        return raw.HasValue ? Math.Round(raw.Value / 1000.0, 3) : null;
    }

    private static void ExtractDrawingObjectEvidence(Paragraph paragraph, string evidencePath, DocxExtractionResult result, MainDocumentPart main, ref int drawingObjectIndex)
    {
        foreach (var drawing in paragraph.Descendants<Drawing>())
        {
            if (drawing.Descendants<A.Blip>().Any())
            {
                continue;
            }

            var graphicData = drawing.Descendants<A.GraphicData>().FirstOrDefault();
            var uri = graphicData?.Uri?.Value;
            var relationshipIds = RelationshipIds(drawing);
            var parts = ExtractPreservedParts(main, relationshipIds);
            var size = DrawingSizeCm(drawing);
            result.DrawingObjects.Add(new ExtractedDrawingObject
            {
                Id = $"drawing-object-{drawingObjectIndex}",
                Index = drawingObjectIndex,
                ObjectType = DrawingObjectType(uri, drawing),
                RelationshipId = relationshipIds.FirstOrDefault(),
                RelationshipIds = relationshipIds,
                Parts = parts,
                GraphicDataUri = uri,
                WidthCm = size.WidthCm,
                HeightCm = size.HeightCm,
                AnchorType = size.AnchorType,
                Text = NormalizeText(string.Concat(drawing.Descendants<Text>().Select(text => text.Text))),
                RawXml = drawing.OuterXml,
                EvidencePath = evidencePath
            });
            drawingObjectIndex++;
        }

        foreach (var picture in paragraph.Descendants<Picture>())
        {
            var relationshipIds = RelationshipIds(picture);
            var parts = ExtractPreservedParts(main, relationshipIds);
            result.DrawingObjects.Add(new ExtractedDrawingObject
            {
                Id = $"drawing-object-{drawingObjectIndex}",
                Index = drawingObjectIndex,
                ObjectType = picture.OuterXml.Contains("textbox", StringComparison.OrdinalIgnoreCase) ? "textBox" : "shape",
                RelationshipId = relationshipIds.FirstOrDefault(),
                RelationshipIds = relationshipIds,
                Parts = parts,
                Text = NormalizeText(string.Concat(picture.Descendants<Text>().Select(text => text.Text))),
                RawXml = picture.OuterXml,
                EvidencePath = evidencePath
            });
            drawingObjectIndex++;
        }
    }

    private static List<string> RelationshipIds(OpenXmlElement element)
    {
        return element.Descendants().Prepend(element)
            .SelectMany(child => child.GetAttributes())
            .Where(attribute => attribute.LocalName == "id" && attribute.NamespaceUri == "http://schemas.openxmlformats.org/officeDocument/2006/relationships")
            .Select(attribute => attribute.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<ExtractedPreservedObjectPart> ExtractPreservedParts(OpenXmlPartContainer container, IReadOnlyList<string> relationshipIds)
    {
        var parts = new List<ExtractedPreservedObjectPart>();
        var visited = new HashSet<Uri>();
        var count = 0;
        long bytes = 0;
        foreach (var relationshipId in relationshipIds)
        {
            if (ExtractPreservedPart(container, relationshipId, visited, ref count, ref bytes) is ExtractedPreservedObjectPart part)
            {
                parts.Add(part);
            }
        }

        return parts;
    }

    private static ExtractedPreservedObjectPart? ExtractPreservedPart(OpenXmlPartContainer container, string relationshipId, HashSet<Uri> visited, ref int count, ref long totalBytes)
    {
        if (count >= MaxPreservedObjectParts)
        {
            return null;
        }

        var partPair = container.Parts.FirstOrDefault(part => string.Equals(part.RelationshipId, relationshipId, StringComparison.Ordinal));
        var part = partPair.OpenXmlPart;
        if (part is null || !IsAllowedPreservedPart(part.RelationshipType, part.ContentType) || !visited.Add(part.Uri))
        {
            return null;
        }

        using var input = part.GetStream(FileMode.Open, FileAccess.Read);
        using var memory = new MemoryStream();
        input.CopyTo(memory);
        totalBytes += memory.Length;
        if (totalBytes > MaxPreservedObjectPartBytes)
        {
            return null;
        }

        count++;
        var extracted = new ExtractedPreservedObjectPart
        {
            RelationshipId = relationshipId,
            RelationshipType = part.RelationshipType,
            ContentType = part.ContentType,
            DataBase64 = Convert.ToBase64String(memory.ToArray())
        };
        foreach (var child in part.Parts.OrderBy(child => child.RelationshipId, StringComparer.Ordinal))
        {
            if (ExtractPreservedPart(part, child.RelationshipId, visited, ref count, ref totalBytes) is ExtractedPreservedObjectPart childPart)
            {
                extracted.Children.Add(childPart);
            }
        }

        return extracted;
    }

    private static bool IsAllowedPreservedPart(string relationshipType, string contentType)
    {
        if (relationshipType.EndsWith("/image", StringComparison.OrdinalIgnoreCase)
            && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return relationshipType is
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart" or
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chartUserShapes" or
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/themeOverride" or
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/diagramColors" or
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/diagramData" or
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/diagramLayout" or
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/diagramQuickStyle" or
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/diagramDrawing" or
            "http://schemas.microsoft.com/office/2011/relationships/chartStyle" or
            "http://schemas.microsoft.com/office/2011/relationships/chartColorStyle";
    }

    private static string DrawingObjectType(string? graphicDataUri, OpenXmlElement drawing)
    {
        if (!string.IsNullOrWhiteSpace(graphicDataUri))
        {
            if (graphicDataUri.Contains("/chart", StringComparison.OrdinalIgnoreCase)) return "chart";
            if (graphicDataUri.Contains("/diagram", StringComparison.OrdinalIgnoreCase)) return "smartArt";
            if (graphicDataUri.Contains("wordprocessingShape", StringComparison.OrdinalIgnoreCase))
            {
                return drawing.Descendants<Text>().Any() ? "textBox" : "shape";
            }

            if (graphicDataUri.Contains("/picture", StringComparison.OrdinalIgnoreCase)) return "picture";
        }

        return drawing.Descendants<Text>().Any() ? "textBox" : "drawing";
    }

    private static ExtractedTable ExtractTable(
        Table table,
        int index,
        DocxExtractionResult result,
        MainDocumentPart main,
        Queue<string> previousTexts,
        ref int figureIndex,
        ref int drawingObjectIndex,
        string imageDirectory,
        string? evidencePath = null,
        string? tableId = null)
    {
        var tableEvidencePath = evidencePath ?? $"tables[{index}]";
        tableId ??= $"table-{index}";
        var tableProperties = table.GetFirstChild<TableProperties>();
        var tableWidth = tableProperties?.TableWidth;
        var rows = new List<ExtractedTableRow>();
        var rowIndex = 0;
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = new List<ExtractedTableCell>();
            var cellIndex = 0;
            foreach (var cell in row.Elements<TableCell>())
            {
                var cellEvidencePath = $"{tableEvidencePath}.rows[{rowIndex}].cells[{cellIndex}]";
                var gridSpan = cell.TableCellProperties?.GridSpan?.Val?.Value;
                var figureIds = new List<string>();
                var nestedTables = new List<ExtractedTable>();
                var cellParagraph = new ExtractedParagraph
                {
                    Id = $"{tableId}-cell-{rowIndex}-{cellIndex}",
                    Index = cellIndex,
                    Text = NormalizeText(string.Join(Environment.NewLine, cell.Elements<Paragraph>().Select(p => p.InnerText).Where(t => !string.IsNullOrWhiteSpace(t)))),
                    EvidencePath = cellEvidencePath
                };
                foreach (var paragraph in cell.Elements<Paragraph>())
                {
                    figureIds.AddRange(ExtractParagraphChildren(paragraph, cellParagraph, result, main, previousTexts, ref figureIndex, ref drawingObjectIndex, imageDirectory));
                }

                var nestedTableIndex = 0;
                foreach (var nestedTable in cell.Elements<Table>())
                {
                    nestedTables.Add(ExtractTable(
                        nestedTable,
                        nestedTableIndex,
                        result,
                        main,
                        previousTexts,
                        ref figureIndex,
                        ref drawingObjectIndex,
                        imageDirectory,
                        $"{cellEvidencePath}.nestedTables[{nestedTableIndex}]",
                        $"{tableId}-nested-{rowIndex}-{cellIndex}-{nestedTableIndex}"));
                    nestedTableIndex++;
                }

                cells.Add(new ExtractedTableCell
                {
                    RowIndex = rowIndex,
                    CellIndex = cellIndex,
                    Text = cellParagraph.Text,
                    GridSpan = gridSpan.HasValue ? Math.Max(1, gridSpan.Value) : 1,
                    VerticalMerge = VerticalMergeValue(cell),
                    WidthTwips = TableCellWidthTwips(cell.TableCellProperties?.TableCellWidth),
                    VerticalAlignment = TableCellVerticalAlignmentValue(cell.TableCellProperties?.TableCellVerticalAlignment),
                    Shading = cell.TableCellProperties?.Shading?.Fill?.Value,
                    Borders = cell.TableCellProperties?.TableCellBorders?.OuterXml,
                    FigureIds = figureIds,
                    NestedTables = nestedTables,
                    EvidencePath = cellEvidencePath
                });
                cellIndex++;
            }

            var rowProperties = row.GetFirstChild<TableRowProperties>();
            rows.Add(new ExtractedTableRow
            {
                Index = rowIndex,
                IsHeader = rowProperties?.GetFirstChild<TableHeader>() is not null,
                CantSplit = rowProperties?.GetFirstChild<CantSplit>() is not null ? true : null,
                HeightTwips = ToNullableInt(rowProperties?.GetFirstChild<TableRowHeight>()?.Val?.Value),
                Cells = cells
            });
            rowIndex++;
        }

        return new ExtractedTable
        {
            Id = tableId,
            Index = index,
            Rows = rows,
            Text = NormalizeText(string.Join(Environment.NewLine, rows.SelectMany(r => r.Cells).SelectMany(CellTextSegments))),
            WidthTwips = TableWidthTwips(tableWidth),
            WidthPercent = TableWidthPercent(tableWidth),
            Alignment = TableAlignmentValue(tableProperties?.TableJustification),
            Borders = tableProperties?.TableBorders?.OuterXml,
            EvidencePath = tableEvidencePath
        };
    }

    private static IEnumerable<string> CellTextSegments(ExtractedTableCell cell)
    {
        if (!string.IsNullOrWhiteSpace(cell.Text))
        {
            yield return cell.Text;
        }

        foreach (var nested in cell.NestedTables)
        {
            if (!string.IsNullOrWhiteSpace(nested.Text))
            {
                yield return nested.Text;
            }
        }
    }

    private static int? TableWidthTwips(TableWidth? width)
    {
        if (width?.Type?.Value != TableWidthUnitValues.Dxa)
        {
            return null;
        }

        return ToNullableInt(width.Width?.Value);
    }

    private static string? TableAlignmentValue(TableJustification? justification)
    {
        var value = justification?.Val?.Value;
        if (value == TableRowAlignmentValues.Center) return "center";
        if (value == TableRowAlignmentValues.Right) return "right";
        if (value == TableRowAlignmentValues.Left) return "left";
        return null;
    }

    private static string? TableCellVerticalAlignmentValue(TableCellVerticalAlignment? alignment)
    {
        var value = alignment?.Val?.Value;
        if (value == TableVerticalAlignmentValues.Center) return "center";
        if (value == TableVerticalAlignmentValues.Bottom) return "bottom";
        if (value == TableVerticalAlignmentValues.Top) return "top";
        return null;
    }

    private static int? TableCellWidthTwips(TableCellWidth? width)
    {
        if (width?.Type?.Value != TableWidthUnitValues.Dxa)
        {
            return null;
        }

        return ToNullableInt(width.Width?.Value);
    }

    private static int? TableWidthPercent(TableWidth? width)
    {
        if (width?.Type?.Value != TableWidthUnitValues.Pct)
        {
            return null;
        }

        var raw = ToNullableInt(width.Width?.Value);
        return raw.HasValue ? (int)Math.Round(raw.Value / 50.0, MidpointRounding.AwayFromZero) : null;
    }

    private static string? VerticalMergeValue(TableCell cell)
    {
        var merge = cell.TableCellProperties?.VerticalMerge;
        if (merge is null)
        {
            return null;
        }

        return merge.Val?.Value == MergedCellValues.Restart ? "restart" : "continue";
    }

    private static void LinkNearbyCaptions(DocxExtractionResult result)
    {
        var paragraphsByEvidencePath = result.Paragraphs
            .GroupBy(paragraph => paragraph.EvidencePath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var tablesByEvidencePath = result.Tables
            .GroupBy(table => table.EvidencePath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        for (var index = 0; index < result.Blocks.Count; index++)
        {
            var block = result.Blocks[index];
            if (block.Type != "table" || !tablesByEvidencePath.TryGetValue(block.EvidencePath, out var table))
            {
                continue;
            }

            var before = NearbyCaption(result.Blocks, paragraphsByEvidencePath, index, -1, CaptionKind.Table);
            var after = NearbyCaption(result.Blocks, paragraphsByEvidencePath, index, 1, CaptionKind.Table);
            var caption = before ?? after;
            if (caption is not null)
            {
                table.SuggestedCaption = caption.Text;
                table.CaptionEvidencePath = caption.EvidencePath;
                table.CaptionPosition = caption.Position;
            }
        }

        foreach (var figure in result.Figures)
        {
            var paragraph = paragraphsByEvidencePath.GetValueOrDefault(figure.EvidencePath);
            if (paragraph is not null && IsCaption(paragraph.Text, CaptionKind.Figure))
            {
                figure.SuggestedCaption = paragraph.Text;
                figure.CaptionEvidencePath = paragraph.EvidencePath;
                continue;
            }

            var blockIndex = result.Blocks.FindIndex(block => string.Equals(block.EvidencePath, figure.EvidencePath, StringComparison.Ordinal));
            if (blockIndex < 0)
            {
                continue;
            }

            var after = NearbyCaption(result.Blocks, paragraphsByEvidencePath, blockIndex, 1, CaptionKind.Figure);
            var before = NearbyCaption(result.Blocks, paragraphsByEvidencePath, blockIndex, -1, CaptionKind.Figure);
            var caption = after ?? before;
            if (caption is not null)
            {
                figure.SuggestedCaption = caption.Text;
                figure.CaptionEvidencePath = caption.EvidencePath;
            }
        }
    }

    private static CaptionCandidate? NearbyCaption(
        IReadOnlyList<ExtractedBlock> blocks,
        IReadOnlyDictionary<string, ExtractedParagraph> paragraphsByEvidencePath,
        int originIndex,
        int direction,
        CaptionKind kind)
    {
        var index = originIndex + direction;
        while (index >= 0 && index < blocks.Count)
        {
            var block = blocks[index];
            if (block.Type == "paragraph" && paragraphsByEvidencePath.TryGetValue(block.EvidencePath, out var paragraph))
            {
                if (string.IsNullOrWhiteSpace(paragraph.Text))
                {
                    index += direction;
                    continue;
                }

                if (IsCaption(paragraph.Text, kind))
                {
                    return new CaptionCandidate(paragraph.Text, paragraph.EvidencePath, direction < 0 ? "before" : "after");
                }
            }

            return null;
        }

        return null;
    }

    private static bool IsCaption(string text, CaptionKind kind)
    {
        var trimmed = text.Trim();
        var pattern = kind == CaptionKind.Figure
            ? @"^(图|Figure)\s*[0-9一二三四五六七八九十]+([：:.\s]|$)"
            : @"^(表|Table)\s*[0-9一二三四五六七八九十]+([：:.\s]|$)";
        return Regex.IsMatch(trimmed, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private sealed record CaptionCandidate(string Text, string EvidencePath, string Position);

    private enum CaptionKind
    {
        Figure,
        Table
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
        else if ((paragraph.OutlineLevel ?? paragraph.EffectiveFormat.OutlineLevel) is <= 5 || IsHeadingLike(text, paragraph))
        {
            paragraph.PossibleRole = "headingCandidate";
            result.PossibleHeadings.Add(Evidence($"heading-{paragraph.Index}", paragraph.EvidencePath, text, "effective style/numbering/shape heading candidate", (paragraph.OutlineLevel ?? paragraph.EffectiveFormat.OutlineLevel) is null ? 0.65 : 0.9));
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

    private static void SummarizeFormatSignatures(DocxExtractionResult result)
    {
        result.FormatSignatures = result.Paragraphs
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph.EffectiveFormat.Signature))
            .GroupBy(paragraph => paragraph.EffectiveFormat.Signature, StringComparer.Ordinal)
            .Select((group, index) => new ExtractedFormatSignature
            {
                Id = $"format-{index}",
                Signature = group.Key,
                UsageCount = group.Count(),
                ParagraphIds = group.Select(paragraph => paragraph.Id).ToList(),
                EvidencePaths = group.Select(paragraph => paragraph.EvidencePath).ToList(),
                RepresentativeFormat = CloneFormat(group.First().EffectiveFormat)
            })
            .OrderByDescending(signature => signature.UsageCount)
            .ThenBy(signature => signature.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static ExtractedEffectiveFormat CloneFormat(ExtractedEffectiveFormat source)
    {
        return new ExtractedEffectiveFormat
        {
            Signature = source.Signature,
            StyleId = source.StyleId,
            StyleName = source.StyleName,
            StyleChain = source.StyleChain.ToList(),
            Font = source.Font,
            EastAsiaFont = source.EastAsiaFont,
            FontSizePt = source.FontSizePt,
            Bold = source.Bold,
            Italic = source.Italic,
            Alignment = source.Alignment,
            LeftIndentTwips = source.LeftIndentTwips,
            RightIndentTwips = source.RightIndentTwips,
            FirstLineIndentTwips = source.FirstLineIndentTwips,
            HangingIndentTwips = source.HangingIndentTwips,
            SpaceBeforeTwips = source.SpaceBeforeTwips,
            SpaceAfterTwips = source.SpaceAfterTwips,
            LineSpacing = source.LineSpacing,
            LineSpacingRule = source.LineSpacingRule,
            OutlineLevel = source.OutlineLevel,
            NumberingId = source.NumberingId,
            NumberingLevel = source.NumberingLevel,
            NumberingFormat = source.NumberingFormat,
            NumberingText = source.NumberingText,
            HasDirectParagraphFormatting = source.HasDirectParagraphFormatting,
            HasDirectRunFormatting = source.HasDirectRunFormatting,
            Sources = source.Sources.ToList()
        };
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
        builder.AppendLine($"- Format signatures: {result.FormatSignatures.Count}");
        builder.AppendLine($"- Format chaos: {result.FormatChaos.ChaosLevel} ({result.FormatChaos.ChaosScore.ToString("0.###", CultureInfo.InvariantCulture)})");
        builder.AppendLine($"- Format clusters: {result.FormatClusters.Count}");
        builder.AppendLine();

        if (result.FormatChaos.Diagnostics.Count > 0)
        {
            builder.AppendLine("## Format Diagnostics");
            foreach (var issue in result.FormatChaos.Diagnostics)
            {
                builder.AppendLine($"- `{issue.Severity}` `{issue.Code}` {issue.Message}");
            }

            builder.AppendLine();
        }

        if (result.FormatClusters.Count > 0)
        {
            builder.AppendLine("## Format Clusters");
            foreach (var cluster in result.FormatClusters.Take(12))
            {
                builder.AppendLine($"- `{cluster.Id}` `{cluster.RoleHint}` confidence={cluster.Confidence.ToString("0.##", CultureInfo.InvariantCulture)} usage={cluster.UsageCount} signatures={cluster.SignatureIds.Count} variance={string.Join(",", cluster.Variance)}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Paragraphs");
        foreach (var paragraph in result.Paragraphs)
        {
            builder.AppendLine($"- `{paragraph.EvidencePath}` `{paragraph.PossibleRole}` `{paragraph.EffectiveFormat.Signature}` {paragraph.Text}");
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

        if ((paragraph.EffectiveFormat.StyleName?.Contains("heading", StringComparison.OrdinalIgnoreCase) == true
                || paragraph.EffectiveFormat.StyleName?.Contains("标题", StringComparison.OrdinalIgnoreCase) == true)
            && text.Length <= 80)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(paragraph.EffectiveFormat.NumberingText)
            && paragraph.EffectiveFormat.NumberingLevel is <= 2
            && text.Length <= 80)
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
