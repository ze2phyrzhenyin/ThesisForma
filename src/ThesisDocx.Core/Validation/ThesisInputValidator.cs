using System.Globalization;
using System.Text.RegularExpressions;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Versioning;

namespace ThesisDocx.Core.Validation;

public sealed class ThesisInputValidator
{
    private const int MaxInlineImageBytes = 8 * 1024 * 1024;
    private const int MaxPreservedObjectPartBytes = 8 * 1024 * 1024;

    private static readonly HashSet<string> SupportedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/gif",
        "image/bmp",
        "image/tiff"
    };

    public ThesisInputValidationResult Validate(ThesisDocument document, ThesisFormatSpec format, string? documentBaseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(format);

        var result = new ThesisInputValidationResult();
        ValidateSchemaVersions(document, format, result);
        ValidateDocumentShape(document, result);
        ValidateFormat(format, result);

        var sectionIds = new HashSet<string>(StringComparer.Ordinal);
        var blockAndBookmarkIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var bookmarks = new HashSet<string>(StringComparer.Ordinal);
        var referenceTargets = new Dictionary<string, string>(StringComparer.Ordinal);
        var bibliographyKeys = new HashSet<string>(StringComparer.Ordinal);
        var citationTargets = new List<(string Path, string TargetId)>();
        var referenceInlines = new List<(string Path, string TargetId)>();
        var figureIds = new HashSet<string>(StringComparer.Ordinal);
        var tableIds = new HashSet<string>(StringComparer.Ordinal);
        var headingIds = new HashSet<string>(StringComparer.Ordinal);
        var footnoteIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var endnoteIds = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var sectionIndex = 0; sectionIndex < document.Sections.Count; sectionIndex++)
        {
            var section = document.Sections[sectionIndex];
            var sectionPath = $"$.sections[{sectionIndex}]";

            if (!string.IsNullOrWhiteSpace(section.Id) && !sectionIds.Add(section.Id))
            {
                result.Add("duplicate.sectionId", $"{sectionPath}.id", $"Section id '{section.Id}' is duplicated.");
            }

            ValidateHeadingSequence(section, format, sectionPath, result);

            for (var blockIndex = 0; blockIndex < section.Blocks.Count; blockIndex++)
            {
                VisitBlock(
                    section.Blocks[blockIndex],
                    $"{sectionPath}.blocks[{blockIndex}]",
                    documentBaseDirectory,
                    format,
                    result,
                    blockAndBookmarkIds,
                    bookmarks,
                    referenceTargets,
                    bibliographyKeys,
                    citationTargets,
                    referenceInlines,
                    figureIds,
                    tableIds,
                    headingIds,
                    footnoteIds,
                    endnoteIds);
            }
        }

        foreach (var (path, targetId) in citationTargets)
        {
            if (!bibliographyKeys.Contains(targetId))
            {
                result.Add("dangling.citation", path, $"Citation target '{targetId}' does not exist in bibliography entries.");
            }
        }

        foreach (var (path, targetId) in referenceInlines)
        {
            if (!referenceTargets.ContainsKey(targetId))
            {
                result.Add("dangling.reference", path, $"Cross reference target '{targetId}' does not match a bookmark, heading, figure, or table.");
            }
        }

        ValidateBodyTextCount(document, format.Validation.BodyTextCount, result);
        return result;
    }

    private static void ValidateSchemaVersions(ThesisDocument document, ThesisFormatSpec format, ThesisInputValidationResult result)
    {
        result.VersionReport = SchemaVersionReport.ForDocumentAndFormat(document.SchemaVersion, format.SchemaVersion);
        if (!ThesisSchemaValidator.IsSupportedVersion(document.SchemaVersion))
        {
            result.Add("unsupported.documentSchemaVersion", "$.schemaVersion", $"Unsupported document schemaVersion '{document.SchemaVersion}'.");
        }

        if (!ThesisSchemaVersions.IsSupportedFormat(format.SchemaVersion))
        {
            result.Add("unsupported.formatSchemaVersion", "$.schemaVersion", $"Unsupported format schemaVersion '{format.SchemaVersion}'.");
        }
    }

    private static void ValidateDocumentShape(ThesisDocument document, ThesisInputValidationResult result)
    {
        if (document.Sections.Count == 0)
        {
            result.Add("document.sections.empty", "$.sections", "ThesisDocument must contain at least one section.");
        }
    }

    private static void ValidateFormat(ThesisFormatSpec format, ThesisInputValidationResult result)
    {
        ValidatePageSetup(format.PageSetup, "$.pageSetup", result);
        ValidateFont(format.DefaultFont, "$.defaultFont", result);
        ValidateParagraph(format.BodyParagraph, "$.bodyParagraph", result);

        foreach (var (level, heading) in format.Headings)
        {
            var path = $"$.headings.{level}";
            if (level is < 1 or > 6 || heading.Level is < 1 or > 6)
            {
                result.Add("format.heading.level.invalid", path, "Heading levels must be between 1 and 6.");
            }

            ValidateFont(heading.Font, $"{path}.font", result);
            ValidateNonNegative(heading.SpaceBeforePt, $"{path}.spaceBeforePt", "format.spacing.negative", "Heading spaceBeforePt must be non-negative.", result);
            ValidateNonNegative(heading.SpaceAfterPt, $"{path}.spaceAfterPt", "format.spacing.negative", "Heading spaceAfterPt must be non-negative.", result);
        }

        ValidateTableWidth(format.Tables.DefaultWidth, "$.tables.defaultWidth", result);
        ValidateMargins(format.Tables.DefaultCellMargins, "$.tables.defaultCellMargins", result);
        ValidateBorders(format.Tables.DefaultBorders, "$.tables.defaultBorders", result);
        ValidateBorders(format.Tables.ThreeLineTableBorders, "$.tables.threeLineTableBorders", result);
        ValidateNoteFormat(format.Notes.Footnote, "$.notes.footnote", result);
        ValidateNoteFormat(format.Notes.Endnote, "$.notes.endnote", result);
        ValidateParagraph(format.Bibliography.EntryParagraph, "$.bibliography.entryParagraph", result);
        if (format.Bibliography.EntryFont is not null)
        {
            ValidateFont(format.Bibliography.EntryFont, "$.bibliography.entryFont", result);
        }

        ValidateTextCountSpec(format.Validation.BodyTextCount, "$.validation.bodyTextCount", result);

        if (format.Equations.FontSizePt <= 0)
        {
            result.Add("format.fontSize.invalid", "$.equations.fontSizePt", "Equation fontSizePt must be greater than 0.");
        }

        ValidateNonNegative(format.Equations.SpacingBeforePt, "$.equations.spacingBeforePt", "format.spacing.negative", "Equation spacingBeforePt must be non-negative.", result);
        ValidateNonNegative(format.Equations.SpacingAfterPt, "$.equations.spacingAfterPt", "format.spacing.negative", "Equation spacingAfterPt must be non-negative.", result);
    }

    private static void ValidateNoteFormat(NoteFormatSpec note, string path, ThesisInputValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(note.StyleId))
        {
            result.Add("format.note.styleId.required", $"{path}.styleId", "Note styleId is required.");
        }

        ValidateFont(note.Font, $"{path}.font", result);
        ValidateParagraph(note.Paragraph, $"{path}.paragraph", result);
        if (note.StartNumber < 1)
        {
            result.Add("format.note.startNumber.invalid", $"{path}.startNumber", "Note startNumber must be at least 1.");
        }
    }

    private static void ValidateTextCountSpec(TextCountValidationSpec? spec, string path, ThesisInputValidationResult result)
    {
        if (spec is null)
        {
            return;
        }

        if (spec.Min is < 0)
        {
            result.Add("format.textCount.min.negative", $"{path}.min", "Text count minimum must be non-negative.");
        }

        if (spec.Max is < 0)
        {
            result.Add("format.textCount.max.negative", $"{path}.max", "Text count maximum must be non-negative.");
        }

        if (spec.Min.HasValue && spec.Max.HasValue && spec.Min.Value > spec.Max.Value)
        {
            result.Add("format.textCount.range.invalid", path, "Text count minimum must not exceed maximum.");
        }

        if (spec.SectionKinds.Count == 0)
        {
            result.Add("format.textCount.sectionKinds.empty", $"{path}.sectionKinds", "Text count validation requires at least one section kind.");
        }
    }

    private static void ValidatePageSetup(PageSetupSpec pageSetup, string path, ThesisInputValidationResult result)
    {
        ValidateNonNegative(pageSetup.TopMarginCm, $"{path}.topMarginCm", "format.margin.negative", "Page top margin must be non-negative.", result);
        ValidateNonNegative(pageSetup.BottomMarginCm, $"{path}.bottomMarginCm", "format.margin.negative", "Page bottom margin must be non-negative.", result);
        ValidateNonNegative(pageSetup.LeftMarginCm, $"{path}.leftMarginCm", "format.margin.negative", "Page left margin must be non-negative.", result);
        ValidateNonNegative(pageSetup.RightMarginCm, $"{path}.rightMarginCm", "format.margin.negative", "Page right margin must be non-negative.", result);
        ValidateNonNegative(pageSetup.GutterCm, $"{path}.gutterCm", "format.margin.negative", "Page gutter must be non-negative.", result);
        ValidateNonNegative(pageSetup.HeaderDistanceCm, $"{path}.headerDistanceCm", "format.headerFooterDistance.negative", "Header distance must be non-negative.", result);
        ValidateNonNegative(pageSetup.FooterDistanceCm, $"{path}.footerDistanceCm", "format.headerFooterDistance.negative", "Footer distance must be non-negative.", result);
        if (pageSetup.Columns < 1)
        {
            result.Add("format.pageSetup.columns.invalid", $"{path}.columns", "Page setup columns must be at least 1.");
        }
    }

    private static void ValidateFont(FontFormatSpec font, string path, ThesisInputValidationResult result)
    {
        if (font.SizePt <= 0)
        {
            result.Add("format.fontSize.invalid", $"{path}.sizePt", "Font size must be greater than 0.");
        }
    }

    private static void ValidateParagraph(ParagraphFormatSpec paragraph, string path, ThesisInputValidationResult result)
    {
        if (paragraph.LineSpacingMultiple <= 0)
        {
            result.Add("format.lineSpacing.invalid", $"{path}.lineSpacingMultiple", "Line spacing multiple must be greater than 0.");
        }

        if (paragraph.LineSpacingExactPt.HasValue && paragraph.LineSpacingExactPt <= 0)
        {
            result.Add("format.lineSpacingExact.invalid", $"{path}.lineSpacingExactPt", "Exact line spacing must be greater than 0 points.");
        }

        ValidateNonNegative(paragraph.SpaceBeforePt, $"{path}.spaceBeforePt", "format.spacing.negative", "Paragraph spaceBeforePt must be non-negative.", result);
        ValidateNonNegative(paragraph.SpaceAfterPt, $"{path}.spaceAfterPt", "format.spacing.negative", "Paragraph spaceAfterPt must be non-negative.", result);
        ValidateNonNegative(paragraph.HangingIndentCm, $"{path}.hangingIndentCm", "format.indent.negative", "Hanging indent must be non-negative.", result);
    }

    private static void ValidateHeadingSequence(ThesisSection section, ThesisFormatSpec format, string sectionPath, ThesisInputValidationResult result)
    {
        if (format.Validation.AllowHeadingLevelSkips)
        {
            return;
        }

        var previousLevel = 0;
        for (var i = 0; i < section.Blocks.Count; i++)
        {
            if (section.Blocks[i] is not HeadingBlock heading)
            {
                continue;
            }

            if (heading.Level > previousLevel + 1)
            {
                result.Add(
                    "heading.levelJump",
                    $"{sectionPath}.blocks[{i}].level",
                    $"Heading level jumps from {previousLevel} to {heading.Level}.");
            }

            previousLevel = heading.Level;
        }
    }

    private static void VisitBlock(
        BlockNode block,
        string path,
        string? documentBaseDirectory,
        ThesisFormatSpec format,
        ThesisInputValidationResult result,
        Dictionary<string, string> blockAndBookmarkIds,
        HashSet<string> bookmarks,
        Dictionary<string, string> referenceTargets,
        HashSet<string> bibliographyKeys,
        List<(string Path, string TargetId)> citationTargets,
        List<(string Path, string TargetId)> referenceInlines,
        HashSet<string> figureIds,
        HashSet<string> tableIds,
        HashSet<string> headingIds,
        Dictionary<string, string> footnoteIds,
        Dictionary<string, string> endnoteIds)
    {
        if (!string.IsNullOrWhiteSpace(block.Id))
        {
            AddUnique(blockAndBookmarkIds, block.Id, $"{path}.id", "duplicate.blockOrBookmarkId", result);
            referenceTargets[block.Id] = path;
        }

        switch (block)
        {
            case ParagraphBlock paragraph:
                if (string.IsNullOrWhiteSpace(PlainText(paragraph.Inlines)))
                {
                    result.AddWarning("paragraph.empty", $"{path}.inlines", "Paragraph has no visible text.");
                }

                VisitInlines(paragraph.Inlines, $"{path}.inlines", result, blockAndBookmarkIds, bookmarks, referenceTargets, citationTargets, referenceInlines, footnoteIds, endnoteIds);
                break;
            case HeadingBlock heading:
                if (heading.Level is < 1 or > 6)
                {
                    result.Add("heading.level.invalid", $"{path}.level", "Heading level must be between 1 and 6.");
                }

                if (!string.IsNullOrWhiteSpace(block.Id))
                {
                    headingIds.Add(block.Id);
                }

                if (!string.IsNullOrWhiteSpace(heading.BookmarkName))
                {
                    AddUnique(blockAndBookmarkIds, heading.BookmarkName, $"{path}.bookmarkName", "duplicate.blockOrBookmarkId", result);
                    bookmarks.Add(heading.BookmarkName);
                    referenceTargets[heading.BookmarkName] = path;
                    headingIds.Add(heading.BookmarkName);
                }

                VisitInlines(heading.Inlines, $"{path}.inlines", result, blockAndBookmarkIds, bookmarks, referenceTargets, citationTargets, referenceInlines, footnoteIds, endnoteIds);
                break;
            case ListBlock list:
                for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                {
                    for (var blockIndex = 0; blockIndex < list.Items[itemIndex].Blocks.Count; blockIndex++)
                    {
                        VisitBlock(list.Items[itemIndex].Blocks[blockIndex], $"{path}.items[{itemIndex}].blocks[{blockIndex}]", documentBaseDirectory, format, result, blockAndBookmarkIds, bookmarks, referenceTargets, bibliographyKeys, citationTargets, referenceInlines, figureIds, tableIds, headingIds, footnoteIds, endnoteIds);
                    }
                }

                break;
            case FigureBlock figure:
                if (!string.IsNullOrWhiteSpace(block.Id))
                {
                    figureIds.Add(block.Id);
                }

                if (string.IsNullOrWhiteSpace(figure.Caption))
                {
                    result.Add("missing.figureCaption", $"{path}.caption", "Figure caption is required.");
                }

                ValidateImage(figure, path, documentBaseDirectory, result);
                break;
            case TableBlock table:
                if (!string.IsNullOrWhiteSpace(block.Id))
                {
                    tableIds.Add(block.Id);
                }

                if (!string.IsNullOrWhiteSpace(table.BookmarkId))
                {
                    AddUnique(blockAndBookmarkIds, table.BookmarkId, $"{path}.bookmarkId", "duplicate.blockOrBookmarkId", result);
                    bookmarks.Add(table.BookmarkId);
                    referenceTargets[table.BookmarkId] = path;
                }

                if (string.IsNullOrWhiteSpace(table.Caption))
                {
                    result.Add("missing.tableCaption", $"{path}.caption", "Table caption is required.");
                }

                ValidateTable(table, path, result);
                VisitTableCellBlocks(table, path, documentBaseDirectory, format, result, blockAndBookmarkIds, bookmarks, referenceTargets, bibliographyKeys, citationTargets, referenceInlines, figureIds, tableIds, headingIds, footnoteIds, endnoteIds);
                break;
            case QuoteBlock quote:
                VisitInlines(quote.Inlines, $"{path}.inlines", result, blockAndBookmarkIds, bookmarks, referenceTargets, citationTargets, referenceInlines, footnoteIds, endnoteIds);
                break;
            case EquationBlock equation:
                if (!string.IsNullOrWhiteSpace(block.Id))
                {
                    referenceTargets[block.Id] = path;
                }

                if (!string.IsNullOrWhiteSpace(equation.BookmarkName))
                {
                    AddUnique(blockAndBookmarkIds, equation.BookmarkName, $"{path}.bookmarkName", "duplicate.blockOrBookmarkId", result);
                    bookmarks.Add(equation.BookmarkName);
                    referenceTargets[equation.BookmarkName] = path;
                }

                if (!string.IsNullOrWhiteSpace(equation.BookmarkId))
                {
                    AddUnique(blockAndBookmarkIds, equation.BookmarkId, $"{path}.bookmarkId", "duplicate.blockOrBookmarkId", result);
                    bookmarks.Add(equation.BookmarkId);
                    referenceTargets[equation.BookmarkId] = path;
                }

                ValidateEquation(equation, path, format, result);
                break;
            case PreservedObjectBlock preserved:
                ValidatePreservedObject(preserved, path, result);
                break;
            case BibliographyBlock bibliography:
                for (var i = 0; i < bibliography.Entries.Count; i++)
                {
                    var entry = bibliography.Entries[i];
                    if (string.IsNullOrWhiteSpace(entry.Text))
                    {
                        result.AddWarning("bibliography.entry.empty", $"{path}.entries[{i}].text", "Bibliography entry text is empty.");
                    }

                    if (!bibliographyKeys.Add(entry.Id))
                    {
                        result.Add("duplicate.bibliographyKey", $"{path}.entries[{i}].id", $"Bibliography key '{entry.Id}' is duplicated.");
                    }
                }

                ValidateBibliographySort(bibliography, path, format.Bibliography.SortOrder, result);
                break;
            case FootnoteBlock footnote:
                AddUnique(footnoteIds, footnote.NoteId, $"{path}.noteId", "duplicate.footnoteId", result);
                ValidateNoteContent(footnote.Inlines, $"{path}.inlines", "footnote", result);
                VisitInlines(footnote.Inlines, $"{path}.inlines", result, blockAndBookmarkIds, bookmarks, referenceTargets, citationTargets, referenceInlines, footnoteIds, endnoteIds);
                break;
            case EndnoteBlock endnote:
                AddUnique(endnoteIds, endnote.NoteId, $"{path}.noteId", "duplicate.endnoteId", result);
                ValidateNoteContent(endnote.Inlines, $"{path}.inlines", "endnote", result);
                VisitInlines(endnote.Inlines, $"{path}.inlines", result, blockAndBookmarkIds, bookmarks, referenceTargets, citationTargets, referenceInlines, footnoteIds, endnoteIds);
                break;
        }
    }

    private static void VisitInlines(
        IReadOnlyList<InlineNode> inlines,
        string path,
        ThesisInputValidationResult result,
        Dictionary<string, string> blockAndBookmarkIds,
        HashSet<string> bookmarks,
        Dictionary<string, string> referenceTargets,
        List<(string Path, string TargetId)> citationTargets,
        List<(string Path, string TargetId)> referenceInlines,
        Dictionary<string, string> footnoteIds,
        Dictionary<string, string> endnoteIds)
    {
        for (var i = 0; i < inlines.Count; i++)
        {
            var inlinePath = $"{path}[{i}]";
            switch (inlines[i])
            {
                case CitationInline citation:
                    citationTargets.Add(($"{inlinePath}.targetId", citation.TargetId));
                    break;
                case ReferenceInline reference:
                    referenceInlines.Add(($"{inlinePath}.bookmarkName", reference.BookmarkName));
                    break;
                case BookmarkInline bookmark:
                    AddUnique(blockAndBookmarkIds, bookmark.Name, $"{inlinePath}.name", "duplicate.blockOrBookmarkId", result);
                    bookmarks.Add(bookmark.Name);
                    referenceTargets[bookmark.Name] = inlinePath;
                    VisitInlines(bookmark.Inlines, $"{inlinePath}.inlines", result, blockAndBookmarkIds, bookmarks, referenceTargets, citationTargets, referenceInlines, footnoteIds, endnoteIds);
                    break;
                case FootnoteInline footnote:
                    AddUnique(footnoteIds, footnote.NoteId, $"{inlinePath}.noteId", "duplicate.footnoteId", result);
                    ValidateNoteContent(footnote.Inlines, $"{inlinePath}.inlines", "footnote", result);
                    VisitInlines(footnote.Inlines, $"{inlinePath}.inlines", result, blockAndBookmarkIds, bookmarks, referenceTargets, citationTargets, referenceInlines, footnoteIds, endnoteIds);
                    break;
                case EndnoteInline endnote:
                    AddUnique(endnoteIds, endnote.NoteId, $"{inlinePath}.noteId", "duplicate.endnoteId", result);
                    ValidateNoteContent(endnote.Inlines, $"{inlinePath}.inlines", "endnote", result);
                    VisitInlines(endnote.Inlines, $"{inlinePath}.inlines", result, blockAndBookmarkIds, bookmarks, referenceTargets, citationTargets, referenceInlines, footnoteIds, endnoteIds);
                    break;
            }
        }
    }

    private static void ValidateImage(FigureBlock figure, string path, string? documentBaseDirectory, ThesisInputValidationResult result)
    {
        if (!SupportedImageContentTypes.Contains(figure.ImageContentType))
        {
            result.Add("invalid.imageContentType", $"{path}.imageContentType", $"Unsupported image content type '{figure.ImageContentType}'.");
        }

        if (!string.IsNullOrWhiteSpace(figure.ImagePath))
        {
            var imagePath = Path.IsPathRooted(figure.ImagePath) || string.IsNullOrWhiteSpace(documentBaseDirectory)
                ? figure.ImagePath
                : Path.Combine(documentBaseDirectory, figure.ImagePath);
            if (!File.Exists(imagePath))
            {
                result.Add("missing.imagePath", $"{path}.imagePath", $"Image file '{figure.ImagePath}' does not exist.");
            }
        }
        else if (string.IsNullOrWhiteSpace(figure.ImageDataBase64))
        {
            result.Add("missing.imageSource", path, "Figure requires imagePath or imageDataBase64.");
        }
        else
        {
            ValidateImageDataBase64(figure.ImageDataBase64, $"{path}.imageDataBase64", result);
        }

        ValidateFigureCrop(figure.Crop, $"{path}.crop", result);
    }

    private static void ValidateFigureCrop(FigureCropSpec? crop, string path, ThesisInputValidationResult result)
    {
        if (crop is null)
        {
            return;
        }

        ValidateCropPercent(crop.LeftPercent, $"{path}.leftPercent", result);
        ValidateCropPercent(crop.TopPercent, $"{path}.topPercent", result);
        ValidateCropPercent(crop.RightPercent, $"{path}.rightPercent", result);
        ValidateCropPercent(crop.BottomPercent, $"{path}.bottomPercent", result);
        if ((crop.LeftPercent ?? 0) + (crop.RightPercent ?? 0) >= 100)
        {
            result.Add("figure.crop.horizontal.invalid", path, "Figure crop leftPercent + rightPercent must be less than 100.");
        }

        if ((crop.TopPercent ?? 0) + (crop.BottomPercent ?? 0) >= 100)
        {
            result.Add("figure.crop.vertical.invalid", path, "Figure crop topPercent + bottomPercent must be less than 100.");
        }
    }

    private static void ValidateCropPercent(double? value, string path, ThesisInputValidationResult result)
    {
        if (value is < 0 or > 100)
        {
            result.Add("figure.crop.percent.invalid", path, "Figure crop percent values must be between 0 and 100.");
        }
    }

    private static void ValidateImageDataBase64(string value, string path, ThesisInputValidationResult result)
    {
        var estimatedBytes = value.Length * 3 / 4;
        if (estimatedBytes > MaxInlineImageBytes)
        {
            result.Add("image.base64.tooLarge", path, $"Inline image data exceeds {MaxInlineImageBytes} bytes.");
            return;
        }

        var buffer = new byte[Math.Max(estimatedBytes, 1)];
        if (!Convert.TryFromBase64String(value, buffer, out _))
        {
            result.Add("image.base64.invalid", path, "imageDataBase64 must be valid base64.");
        }
    }

    private static void VisitTableCellBlocks(
        TableBlock table,
        string path,
        string? documentBaseDirectory,
        ThesisFormatSpec format,
        ThesisInputValidationResult result,
        Dictionary<string, string> blockAndBookmarkIds,
        HashSet<string> bookmarks,
        Dictionary<string, string> referenceTargets,
        HashSet<string> bibliographyKeys,
        List<(string Path, string TargetId)> citationTargets,
        List<(string Path, string TargetId)> referenceInlines,
        HashSet<string> figureIds,
        HashSet<string> tableIds,
        HashSet<string> headingIds,
        Dictionary<string, string> footnoteIds,
        Dictionary<string, string> endnoteIds)
    {
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
            {
                var cell = row.Cells[cellIndex];
                var cellPath = $"{path}.rows[{rowIndex}].cells[{cellIndex}]";
                for (var blockIndex = 0; blockIndex < cell.Blocks.Count; blockIndex++)
                {
                    var child = cell.Blocks[blockIndex];
                    var childPath = $"{cellPath}.blocks[{blockIndex}]";
                    ValidateSupportedTableCellBlock(child, childPath, result);
                    VisitBlock(child, childPath, documentBaseDirectory, format, result, blockAndBookmarkIds, bookmarks, referenceTargets, bibliographyKeys, citationTargets, referenceInlines, figureIds, tableIds, headingIds, footnoteIds, endnoteIds);
                }
            }
        }
    }

    private static void ValidateSupportedTableCellBlock(BlockNode block, string path, ThesisInputValidationResult result)
    {
        switch (block)
        {
            case ParagraphBlock:
            case HeadingBlock:
            case QuoteBlock:
            case FigureBlock:
            case TableBlock:
            case PreservedObjectBlock:
            case FootnoteBlock:
            case EndnoteBlock:
                return;
            case ListBlock list:
                for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                {
                    for (var blockIndex = 0; blockIndex < list.Items[itemIndex].Blocks.Count; blockIndex++)
                    {
                        ValidateSupportedTableCellListItemBlock(list.Items[itemIndex].Blocks[blockIndex], $"{path}.items[{itemIndex}].blocks[{blockIndex}]", result);
                    }
                }

                return;
            default:
                result.Add("table.cellBlock.unsupported", path, "Table cell blocks support paragraph, heading, quote, figure, table, preservedObject, list, footnote, and endnote blocks only.");
                return;
        }
    }

    private static void ValidateSupportedTableCellListItemBlock(BlockNode block, string path, ThesisInputValidationResult result)
    {
        if (block is not ParagraphBlock and not QuoteBlock)
        {
            result.Add("table.cellListBlock.unsupported", path, "Table cell list items support paragraph and quote blocks only.");
        }
    }

    private static void ValidateEquation(EquationBlock equation, string path, ThesisFormatSpec format, ThesisInputValidationResult result)
    {
        switch (equation.SourceType)
        {
            case EquationSourceType.Omml:
                if (string.IsNullOrWhiteSpace(equation.Omml))
                {
                    result.Add("equation.sourceMismatch", $"{path}.omml", "sourceType omml requires omml.");
                }
                else
                {
                    Merge(result, OmmlSafetyValidator.Validate(equation.Omml, $"{path}.omml"));
                }

                break;
            case EquationSourceType.Latex:
                if (string.IsNullOrWhiteSpace(equation.Latex))
                {
                    result.Add("equation.sourceMismatch", $"{path}.latex", "sourceType latex requires latex.");
                }
                else if (!EquationRenderer.IsLatexSubsetSupported(equation.Latex))
                {
                    if (format.Equations.AllowLatexFallbackToPlain)
                    {
                        result.AddWarning("equation.latex.unsupportedFallback", $"{path}.latex", "Latex expression is outside the supported subset and will be rendered as plain OMML text.");
                    }
                    else
                    {
                        result.Add("equation.latex.unsupported", $"{path}.latex", "Latex expression is outside the supported subset and fallback is disabled.");
                    }
                }

                break;
            case EquationSourceType.Plain:
                if (string.IsNullOrWhiteSpace(equation.PlainText) && string.IsNullOrWhiteSpace(equation.Placeholder))
                {
                    result.Add("equation.sourceMismatch", $"{path}.plainText", "sourceType plain requires plainText or legacy placeholder.");
                }

                break;
        }

        var numbering = equation.Numbering;
        if (numbering is not null)
        {
            if (numbering.Enabled && (!numbering.Format.Contains("{index}", StringComparison.Ordinal) || !numbering.Format.StartsWith("(", StringComparison.Ordinal) || !numbering.Format.EndsWith(")", StringComparison.Ordinal)))
            {
                result.Add("equation.numbering.invalidFormat", $"{path}.numbering.format", "Equation numbering format must be parenthesized and contain {index}.");
            }

            if (numbering.RestartByHeadingLevel is < 1 or > 6)
            {
                result.Add("equation.numbering.invalidRestartLevel", $"{path}.numbering.restartByHeadingLevel", "restartByHeadingLevel must be between 1 and 6.");
            }
        }
    }

    private static void ValidatePreservedObject(PreservedObjectBlock preserved, string path, ThesisInputValidationResult result)
    {
        if (!Enum.IsDefined(preserved.ObjectType))
        {
            result.Add("preservedObject.objectType.invalid", $"{path}.objectType", "Preserved object type is not supported.");
        }

        if (!Enum.IsDefined(preserved.PreservationMode))
        {
            result.Add("preservedObject.preservationMode.invalid", $"{path}.preservationMode", "Preserved object mode is not supported.");
        }

        if (preserved.WidthCm is < 0)
        {
            result.Add("preservedObject.width.negative", $"{path}.widthCm", "Preserved object widthCm must be non-negative.");
        }

        if (preserved.HeightCm is < 0)
        {
            result.Add("preservedObject.height.negative", $"{path}.heightCm", "Preserved object heightCm must be non-negative.");
        }

        if (preserved.PreservationMode == PreservedObjectMode.ExtractText && string.IsNullOrWhiteSpace(preserved.ExtractedText))
        {
            result.Add("preservedObject.extractedText.missing", $"{path}.extractedText", "ExtractText preserved objects require extractedText.");
        }

        if (preserved.PreservationMode == PreservedObjectMode.Passthrough)
        {
            var rootPartRelationshipIds = preserved.Parts.Select(part => part.RelationshipId).ToHashSet(StringComparer.Ordinal);
            var missingRootParts = preserved.RelationshipIds.Where(id => !rootPartRelationshipIds.Contains(id)).ToList();
            if (missingRootParts.Count > 0)
            {
                result.Add("preservedObject.passthrough.partGraphMissing", $"{path}.parts", $"Passthrough object is missing preserved part payloads for relationship ids: {string.Join(", ", missingRootParts)}.");
            }

            Merge(result, PreservedObjectSafetyValidator.Validate(preserved.RawXml, $"{path}.rawXml", allowRelationshipReferences: preserved.Parts.Count > 0));
        }
        else if (!string.IsNullOrWhiteSpace(preserved.RawXml))
        {
            var safety = PreservedObjectSafetyValidator.Validate(preserved.RawXml, $"{path}.rawXml", allowRelationshipReferences: true);
            result.Warnings.AddRange(safety.Errors);
            result.Warnings.AddRange(safety.Warnings);
        }

        for (var i = 0; i < preserved.Parts.Count; i++)
        {
            ValidatePreservedObjectPart(preserved.Parts[i], $"{path}.parts[{i}]", result);
        }
    }

    private static void ValidatePreservedObjectPart(PreservedObjectPart part, string path, ThesisInputValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(part.RelationshipId))
        {
            result.Add("preservedObject.part.relationshipId.missing", $"{path}.relationshipId", "Preserved object part relationshipId is required.");
        }

        if (!IsAllowedPreservedPart(part.RelationshipType, part.ContentType))
        {
            result.Add("preservedObject.part.relationshipType.unsupported", $"{path}.relationshipType", $"Preserved object part relationship type '{part.RelationshipType}' is not supported.");
        }

        var estimatedBytes = part.DataBase64.Length * 3 / 4;
        if (estimatedBytes > MaxPreservedObjectPartBytes)
        {
            result.Add("preservedObject.part.tooLarge", $"{path}.dataBase64", $"Preserved object part exceeds {MaxPreservedObjectPartBytes} bytes.");
        }
        else
        {
            var buffer = new byte[Math.Max(estimatedBytes, 1)];
            if (!Convert.TryFromBase64String(part.DataBase64, buffer, out _))
            {
                result.Add("preservedObject.part.base64.invalid", $"{path}.dataBase64", "Preserved object part dataBase64 must be valid base64.");
            }
        }

        for (var i = 0; i < part.Children.Count; i++)
        {
            ValidatePreservedObjectPart(part.Children[i], $"{path}.children[{i}]", result);
        }
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

    private static void ValidateTable(TableBlock table, string path, ThesisInputValidationResult result)
    {
        ValidateTableWidth(table.Width, $"{path}.width", result);
        ValidateMargins(table.CellMargins, $"{path}.cellMargins", result);
        ValidateBorders(table.Borders, $"{path}.borders", result);

        var expectedColumns = -1;
        var hasBodyRow = false;
        var activeVerticalMerges = new Dictionary<int, int>();
        var fixedLayoutMissingWidths = table.Layout == TableLayoutKind.Fixed;

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            if (row.HeightPt is < 0)
            {
                result.Add("table.rowHeight.negative", $"{path}.rows[{rowIndex}].heightPt", "Table row height must be non-negative.");
            }

            if (!row.IsHeader)
            {
                hasBodyRow = true;
            }
            else if (hasBodyRow)
            {
                result.Add("table.header.afterBody", $"{path}.rows[{rowIndex}].isHeader", "Header rows must appear before body rows.");
            }

            var logicalColumns = 0;
            var nextActiveVerticalMerges = new Dictionary<int, int>();
            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
            {
                var cell = row.Cells[cellIndex];
                var cellPath = $"{path}.rows[{rowIndex}].cells[{cellIndex}]";
                if (cell.Width is not null || cell.WidthCm.HasValue)
                {
                    fixedLayoutMissingWidths = false;
                }

                ValidateTableWidth(cell.Width, $"{cellPath}.width", result);
                if (cell.WidthCm is < 0)
                {
                    result.Add("table.cellWidth.negative", $"{cellPath}.widthCm", "Table cell widthCm must be non-negative.");
                }

                ValidateMargins(cell.CellMargins, $"{cellPath}.cellMargins", result);
                ValidateBorders(cell.Borders, $"{cellPath}.borders", result);
                if (cell.VerticalAlignment.HasValue && !Enum.IsDefined(cell.VerticalAlignment.Value))
                {
                    result.Add("table.cellVerticalAlignment.invalid", $"{cellPath}.verticalAlignment", "Table cell verticalAlignment is not supported.");
                }

                if (cell.GridSpan < 1)
                {
                    result.Add("table.gridSpan.invalid", $"{cellPath}.gridSpan", "gridSpan must be greater than or equal to 1.");
                }
                else if (cell.GridSpan > 32)
                {
                    result.Add("table.gridSpan.tooWide", $"{cellPath}.gridSpan", "gridSpan must not exceed 32 logical columns.");
                }

                var span = Math.Max(1, cell.GridSpan);
                for (var col = logicalColumns; col < logicalColumns + span; col++)
                {
                    if (cell.VerticalMerge == VerticalMergeKind.Continue && !activeVerticalMerges.ContainsKey(col))
                    {
                        result.Add("table.verticalMerge.invalidChain", $"{cellPath}.verticalMerge", "vMerge continue requires a restart or continue above it.");
                    }
                    else if (cell.VerticalMerge == VerticalMergeKind.Continue && activeVerticalMerges.TryGetValue(col, out var activeSpan) && activeSpan != span)
                    {
                        result.Add("table.verticalMerge.spanMismatch", $"{cellPath}.gridSpan", "vMerge continue must keep the same gridSpan as the active merge above it.");
                    }

                    if (cell.VerticalMerge is VerticalMergeKind.Restart or VerticalMergeKind.Continue)
                    {
                        nextActiveVerticalMerges[col] = span;
                    }
                }

                logicalColumns += span;
            }

            if (expectedColumns < 0)
            {
                expectedColumns = logicalColumns;
            }
            else if (logicalColumns != expectedColumns)
            {
                result.Add("table.grid.inconsistent", $"{path}.rows[{rowIndex}]", $"Logical column count {logicalColumns} does not match expected {expectedColumns}.");
            }

            activeVerticalMerges = nextActiveVerticalMerges;
        }

        if (table.RepeatHeaderRows.HasValue && table.RepeatHeaderRows.Value > table.Rows.Count(row => row.IsHeader))
        {
            result.Add("table.repeatHeaderRows.invalid", $"{path}.repeatHeaderRows", "repeatHeaderRows exceeds available header rows.");
        }

        if (fixedLayoutMissingWidths)
        {
            result.AddWarning("table.fixedLayout.widthsMissing", $"{path}.rows", "Fixed layout table should specify table or cell widths.");
        }
    }

    private static void ValidateBibliographySort(BibliographyBlock bibliography, string path, BibliographySortOrder sortOrder, ThesisInputValidationResult result)
    {
        if (sortOrder == BibliographySortOrder.DocumentOrder || bibliography.Entries.Count < 2)
        {
            return;
        }

        var years = bibliography.Entries
            .Select((entry, index) => (Index: index, Year: ExtractPublicationYear(entry.Text)))
            .ToList();
        foreach (var item in years.Where(item => item.Year is null))
        {
            result.AddWarning("bibliography.sort.yearMissing", $"{path}.entries[{item.Index}].text", "Bibliography sort order is configured by year, but no publication year was found in this entry.");
        }

        var comparableYears = years.Where(item => item.Year.HasValue).Select(item => (item.Index, Year: item.Year!.Value)).ToList();
        if (sortOrder == BibliographySortOrder.Chronological)
        {
            var ascending = IsYearOrdered(comparableYears, descending: false);
            var descending = IsYearOrdered(comparableYears, descending: true);
            if (!ascending && !descending)
            {
                result.Add(
                    "bibliography.sort.yearOrder",
                    path,
                    "Bibliography entries violate configured chronological year order.");
            }

            return;
        }

        for (var i = 1; i < comparableYears.Count; i++)
        {
            var previous = comparableYears[i - 1];
            var current = comparableYears[i];
            var outOfOrder = sortOrder == BibliographySortOrder.YearAscending
                ? current.Year < previous.Year
                : current.Year > previous.Year;
            if (outOfOrder)
            {
                result.Add(
                    "bibliography.sort.yearOrder",
                    $"{path}.entries[{current.Index}].text",
                    $"Bibliography entry year {current.Year} violates configured {sortOrder} order after {previous.Year}.");
            }
        }
    }

    private static bool IsYearOrdered(IReadOnlyList<(int Index, int Year)> years, bool descending)
    {
        for (var i = 1; i < years.Count; i++)
        {
            if (descending ? years[i].Year > years[i - 1].Year : years[i].Year < years[i - 1].Year)
            {
                return false;
            }
        }

        return true;
    }

    private static int? ExtractPublicationYear(string text)
    {
        var match = Regex.Match(text, @"(?<!\d)(1[5-9]\d{2}|20\d{2}|21\d{2})(?!\d)", RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Value, out var year) ? year : null;
    }

    private static void ValidateTableWidth(TableWidthSpec? width, string path, ThesisInputValidationResult result)
    {
        if (width is null)
        {
            return;
        }

        if (width.Type == TableWidthKind.Percent && width.Value is < 0 or > 100)
        {
            result.Add("table.width.percent.invalid", $"{path}.value", "Percent table width must be between 0 and 100.");
        }

        if (width.Type == TableWidthKind.Dxa && width.Value is < 0)
        {
            result.Add("table.width.dxa.negative", $"{path}.value", "Dxa table width must be non-negative.");
        }
    }

    private static void ValidateMargins(TableCellMarginsSpec? margins, string path, ThesisInputValidationResult result)
    {
        if (margins is null)
        {
            return;
        }

        ValidateNonNegative(margins.TopCm, $"{path}.topCm", "table.cellMargin.negative", "Table cell top margin must be non-negative.", result);
        ValidateNonNegative(margins.BottomCm, $"{path}.bottomCm", "table.cellMargin.negative", "Table cell bottom margin must be non-negative.", result);
        ValidateNonNegative(margins.LeftCm, $"{path}.leftCm", "table.cellMargin.negative", "Table cell left margin must be non-negative.", result);
        ValidateNonNegative(margins.RightCm, $"{path}.rightCm", "table.cellMargin.negative", "Table cell right margin must be non-negative.", result);
    }

    private static void ValidateBorders(TableBordersSpec? borders, string path, ThesisInputValidationResult result)
    {
        if (borders is null)
        {
            return;
        }

        ValidateBorder(borders.Top, $"{path}.top", result);
        ValidateBorder(borders.Bottom, $"{path}.bottom", result);
        ValidateBorder(borders.Left, $"{path}.left", result);
        ValidateBorder(borders.Right, $"{path}.right", result);
        ValidateBorder(borders.InsideH, $"{path}.insideH", result);
        ValidateBorder(borders.InsideV, $"{path}.insideV", result);
    }

    private static void ValidateBorder(BorderSpec? border, string path, ThesisInputValidationResult result)
    {
        if (border is null)
        {
            return;
        }

        if (!Enum.IsDefined(border.Style))
        {
            result.Add("table.border.style.invalid", $"{path}.style", "Table border style is not supported.");
        }

        if (border.Size < 0)
        {
            result.Add("table.border.size.negative", $"{path}.size", "Table border size must be non-negative.");
        }

        if (border.Space < 0)
        {
            result.Add("table.border.space.negative", $"{path}.space", "Table border space must be non-negative.");
        }

        if (!IsHexColor(border.Color))
        {
            result.Add("table.border.color.invalid", $"{path}.color", "Table border color must be a six-digit RGB hex value.");
        }
    }

    private static bool IsHexColor(string? value)
    {
        return value is { Length: 6 } && value.All(Uri.IsHexDigit);
    }

    private static void ValidateNonNegative(double? value, string path, string code, string message, ThesisInputValidationResult result)
    {
        if (value is < 0)
        {
            result.Add(code, path, message);
        }
    }

    private static void Merge(ThesisInputValidationResult target, ThesisInputValidationResult source)
    {
        target.Errors.AddRange(source.Errors);
        target.Warnings.AddRange(source.Warnings);
    }

    private static void ValidateBodyTextCount(ThesisDocument document, TextCountValidationSpec? spec, ThesisInputValidationResult result)
    {
        if (spec is null)
        {
            return;
        }

        var sectionKinds = spec.SectionKinds.ToHashSet();
        var text = string.Concat(document.Sections
            .Where(section => sectionKinds.Contains(section.Kind))
            .SelectMany(section => section.Blocks)
            .Select(BlockPlainText));
        var count = CountText(text, spec.Unit);

        if (spec.Min.HasValue && count < spec.Min.Value)
        {
            result.Add("textCount.body.tooShort", "$.sections", $"Body text count {count} is below configured minimum {spec.Min.Value}.");
        }

        if (spec.Max.HasValue && count > spec.Max.Value)
        {
            result.Add("textCount.body.tooLong", "$.sections", $"Body text count {count} exceeds configured maximum {spec.Max.Value}.");
        }
    }

    private static int CountText(string text, TextCountUnit unit)
    {
        return unit switch
        {
            TextCountUnit.Words => Regex.Matches(text, @"[\p{L}\p{N}]+", RegexOptions.CultureInvariant).Count,
            TextCountUnit.UnicodeTextElements => new StringInfo(text).LengthInTextElements,
            _ => text.Count(ch => !char.IsWhiteSpace(ch))
        };
    }

    private static void ValidateNoteContent(IReadOnlyList<InlineNode> inlines, string path, string kind, ThesisInputValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(PlainText(inlines)))
        {
            result.Add("note.empty", path, $"{kind} content must not be empty.");
        }
    }

    private static string PlainText(IEnumerable<InlineNode> inlines)
    {
        return string.Concat(inlines.Select(inline => inline switch
        {
            TextInline text => text.Text,
            HyperlinkInline hyperlink => hyperlink.Text,
            CitationInline citation => citation.DisplayText,
            ReferenceInline reference => reference.FallbackText ?? reference.BookmarkName,
            BookmarkInline bookmark => PlainText(bookmark.Inlines),
            FootnoteInline footnote => PlainText(footnote.Inlines),
            EndnoteInline endnote => PlainText(endnote.Inlines),
            _ => string.Empty
        }));
    }

    private static string BlockPlainText(BlockNode block)
    {
        return block switch
        {
            ParagraphBlock paragraph => PlainText(paragraph.Inlines),
            HeadingBlock heading => PlainText(heading.Inlines),
            ListBlock list => string.Concat(list.Items.SelectMany(item => item.Blocks).Select(BlockPlainText)),
            TableBlock table => string.Concat(table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks).Select(BlockPlainText)),
            QuoteBlock quote => PlainText(quote.Inlines),
            BibliographyBlock bibliography => string.Concat(bibliography.Entries.Select(entry => entry.Text)),
            FootnoteBlock footnote => PlainText(footnote.Inlines),
            EndnoteBlock endnote => PlainText(endnote.Inlines),
            EquationBlock equation => equation.PlainText ?? equation.Placeholder ?? string.Empty,
            PreservedObjectBlock preserved => preserved.ExtractedText ?? string.Empty,
            _ => string.Empty
        };
    }

    private static void AddUnique(Dictionary<string, string> values, string id, string path, string code, ThesisInputValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            result.Add("missing.id", path, "Identifier is required.");
            return;
        }

        if (values.TryGetValue(id, out var existingPath))
        {
            result.Add(code, path, $"Identifier '{id}' duplicates '{existingPath}'.");
        }
        else
        {
            values[id] = path;
        }
    }
}
