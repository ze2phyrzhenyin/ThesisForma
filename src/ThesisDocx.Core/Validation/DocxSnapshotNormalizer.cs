using System.Text;

namespace ThesisDocx.Core.Validation;

public sealed class DocxSnapshotNormalizer
{
    public string NormalizeToStableSnapshot(string docxPath)
    {
        var inspection = new DocxInspector().Inspect(docxPath);
        var builder = new StringBuilder();

        AppendList(builder, "entries", NormalizeEntries(inspection.Entries));
        AppendList(builder, "styles", inspection.Styles);
        AppendList(builder, "numberingLevelTexts", inspection.NumberingLevelTexts);
        AppendList(builder, "fieldCodes", NormalizeRelationshipIds(inspection.FieldCodes));
        AppendList(builder, "sectionPageNumberFormats", inspection.SectionPageNumberFormats);
        AppendList(builder, "bookmarks", inspection.Bookmarks);
        AppendList(builder, "equationBookmarks", inspection.Equations.Bookmarks);
        AppendList(builder, "equationSourceTypes", inspection.Equations.SourceTypes);
        AppendList(builder, "footnoteStyles", inspection.Footnotes.StyleIds);
        AppendList(builder, "endnoteStyles", inspection.Endnotes.StyleIds);
        AppendList(builder, "tableBookmarks", inspection.Tables.Bookmarks);
        AppendList(builder, "tableStyles", inspection.Tables.Styles);
        AppendList(builder, "tableCellParagraphStyles", inspection.Tables.CellParagraphStyleIds);
        AppendList(builder, "tableWidthTypes", inspection.Tables.WidthTypes);
        AppendList(builder, "tableBorders", inspection.Tables.BorderSummary);

        builder.AppendLine($"paragraphCount={inspection.ParagraphCount}");
        builder.AppendLine($"tableCount={inspection.TableCount}");
        builder.AppendLine($"drawingCount={inspection.DrawingCount}");
        builder.AppendLine($"equationCount={inspection.Equations.Count}");
        builder.AppendLine($"equationOmmlElementCount={inspection.Equations.OmmlElementCount}");
        builder.AppendLine($"tableHasGridSpan={inspection.Tables.HasGridSpan}");
        builder.AppendLine($"tableHasVerticalMerge={inspection.Tables.HasVerticalMerge}");
        builder.AppendLine($"tableHasRepeatHeaderRows={inspection.Tables.HasRepeatHeaderRows}");
        builder.AppendLine($"tableHasCantSplitRows={inspection.Tables.HasCantSplitRows}");
        builder.AppendLine($"tableHasNestedCellBlocks={inspection.Tables.HasNestedCellBlocks}");
        builder.AppendLine($"tableHasCellNoteReferences={inspection.Tables.HasCellNoteReferences}");
        builder.AppendLine($"templateRuleParagraphCount={inspection.TemplateRendering.RuleParagraphCount}");
        builder.AppendLine($"headerCount={inspection.HeaderCount}");
        builder.AppendLine($"footerCount={inspection.FooterCount}");

        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void AppendList(StringBuilder builder, string name, IEnumerable<string> values)
    {
        builder.AppendLine($"{name}:");
        foreach (var value in values)
        {
            builder.AppendLine($"- {value}");
        }
    }

    private static IEnumerable<string> NormalizeRelationshipIds(IEnumerable<string> values)
    {
        return values.Select(value => System.Text.RegularExpressions.Regex.Replace(value, @"rId\d+", "rId#"));
    }

    private static IEnumerable<string> NormalizeEntries(IEnumerable<string> entries)
    {
        return entries
            .Select(value => System.Text.RegularExpressions.Regex.Replace(
                value,
                @"package/services/metadata/core-properties/[^/]+\.psmdcp",
                "package/services/metadata/core-properties/core-properties.psmdcp"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
    }
}
