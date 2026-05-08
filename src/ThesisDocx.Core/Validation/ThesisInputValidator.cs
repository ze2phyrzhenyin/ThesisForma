using ThesisDocx.Core.Models;
using ThesisDocx.Core.Rendering;

namespace ThesisDocx.Core.Validation;

public sealed class ThesisInputValidator
{
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

        return result;
    }

    private static void ValidateSchemaVersions(ThesisDocument document, ThesisFormatSpec format, ThesisInputValidationResult result)
    {
        if (!ThesisSchemaValidator.IsSupportedVersion(document.SchemaVersion))
        {
            result.Add("unsupported.documentSchemaVersion", "$.schemaVersion", $"Unsupported document schemaVersion '{document.SchemaVersion}'.");
        }

        if (!ThesisSchemaVersions.IsSupportedFormat(format.SchemaVersion))
        {
            result.Add("unsupported.formatSchemaVersion", "$.schemaVersion", $"Unsupported format schemaVersion '{format.SchemaVersion}'.");
        }
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
                VisitInlines(paragraph.Inlines, $"{path}.inlines", result, blockAndBookmarkIds, bookmarks, referenceTargets, citationTargets, referenceInlines, footnoteIds, endnoteIds);
                break;
            case HeadingBlock heading:
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
            case BibliographyBlock bibliography:
                for (var i = 0; i < bibliography.Entries.Count; i++)
                {
                    var entry = bibliography.Entries[i];
                    if (!bibliographyKeys.Add(entry.Id))
                    {
                        result.Add("duplicate.bibliographyKey", $"{path}.entries[{i}].id", $"Bibliography key '{entry.Id}' is duplicated.");
                    }
                }

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

    private static void ValidateTable(TableBlock table, string path, ThesisInputValidationResult result)
    {
        var expectedColumns = -1;
        var hasBodyRow = false;
        var activeVerticalMerges = new HashSet<int>();

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            if (!row.IsHeader)
            {
                hasBodyRow = true;
            }
            else if (hasBodyRow)
            {
                result.Add("table.header.afterBody", $"{path}.rows[{rowIndex}].isHeader", "Header rows must appear before body rows.");
            }

            var logicalColumns = 0;
            var nextActiveVerticalMerges = new HashSet<int>();
            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
            {
                var cell = row.Cells[cellIndex];
                var cellPath = $"{path}.rows[{rowIndex}].cells[{cellIndex}]";
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
                    if (cell.VerticalMerge == VerticalMergeKind.Continue && !activeVerticalMerges.Contains(col))
                    {
                        result.Add("table.verticalMerge.invalidChain", $"{cellPath}.verticalMerge", "vMerge continue requires a restart or continue above it.");
                    }

                    if (cell.VerticalMerge is VerticalMergeKind.Restart or VerticalMergeKind.Continue)
                    {
                        nextActiveVerticalMerges.Add(col);
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
    }

    private static void Merge(ThesisInputValidationResult target, ThesisInputValidationResult source)
    {
        target.Errors.AddRange(source.Errors);
        target.Warnings.AddRange(source.Warnings);
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
