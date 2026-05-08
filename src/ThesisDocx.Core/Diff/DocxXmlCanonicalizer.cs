using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ThesisDocx.Core.Diff;

public sealed class DocxXmlCanonicalizer
{
    private static readonly Regex RelationshipIdRegex = new(@"\brId\d+\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TimestampRegex = new(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyDictionary<string, string> ReadCanonicalParts(string docxPath, DocxStructureDiffOptions? options = null)
    {
        options ??= new DocxStructureDiffOptions();
        using var archive = ZipFile.OpenRead(docxPath);
        return archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .Where(entry => !IsIgnored(entry.FullName, options))
            .OrderBy(entry => entry.FullName, StringComparer.Ordinal)
            .ToDictionary(entry => entry.FullName, entry => CanonicalizeEntry(entry), StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, string> ExtractMarkers(string docxPath, DocxStructureDiffOptions? options = null)
    {
        options ??= new DocxStructureDiffOptions();
        var markers = new Dictionary<string, string>(StringComparer.Ordinal);
        using var archive = ZipFile.OpenRead(docxPath);
        foreach (var entry in archive.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(entry.Name) || IsIgnored(entry.FullName, options))
            {
                continue;
            }

            if (!IsXml(entry.FullName))
            {
                markers[$"{entry.FullName}:binary:length"] = entry.Length.ToString();
                continue;
            }

            XDocument? document;
            try
            {
                using var stream = entry.Open();
                document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            }
            catch
            {
                continue;
            }

            ExtractXmlMarkers(entry.FullName, document, markers, options);
        }

        return markers;
    }

    public string CanonicalizeXml(string xml)
    {
        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        NormalizeDocument(document);
        return WriteCanonical(document.Root);
    }

    private static string CanonicalizeEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        if (!IsXml(entry.FullName))
        {
            return Convert.ToBase64String(bytes);
        }

        try
        {
            var xml = Encoding.UTF8.GetString(bytes);
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            NormalizeDocument(document);
            return WriteCanonical(document.Root);
        }
        catch
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private static bool IsXml(string partName)
    {
        return partName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            || partName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnored(string partName, DocxStructureDiffOptions options)
    {
        return options.IgnoredPartNames.Contains(partName)
            || partName.StartsWith("package/services/metadata/core-properties/", StringComparison.OrdinalIgnoreCase);
    }

    private static void NormalizeDocument(XDocument document)
    {
        if (document.Root is null)
        {
            return;
        }

        foreach (var element in document.Descendants().ToList())
        {
            if (element.Name.LocalName is "created" or "modified" or "lastPrinted")
            {
                element.Value = "TIMESTAMP";
            }

            foreach (var attribute in element.Attributes().ToList())
            {
                if (attribute.Name.LocalName.StartsWith("rsid", StringComparison.OrdinalIgnoreCase))
                {
                    attribute.Remove();
                    continue;
                }

                if (element.Name.LocalName is "docPr" or "cNvPr" && attribute.Name.LocalName == "id")
                {
                    attribute.Value = "docPr#";
                    continue;
                }

                if (attribute.Name.NamespaceName.Contains("officeDocument/2006/relationships", StringComparison.Ordinal)
                    || attribute.Value.StartsWith("rId", StringComparison.Ordinal))
                {
                    attribute.Value = RelationshipIdRegex.Replace(attribute.Value, "rId#");
                }

                attribute.Value = TimestampRegex.Replace(attribute.Value, "TIMESTAMP");
            }
        }
    }

    private static string WriteCanonical(XElement? element)
    {
        if (element is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        WriteElement(element, builder);
        return builder.ToString();
    }

    private static void WriteElement(XElement element, StringBuilder builder)
    {
        builder.Append('<').Append(element.Name);
        foreach (var attribute in element.Attributes().OrderBy(a => a.Name.NamespaceName, StringComparer.Ordinal).ThenBy(a => a.Name.LocalName, StringComparer.Ordinal))
        {
            builder.Append(' ').Append(attribute.Name).Append("=\"").Append(attribute.Value).Append('"');
        }

        builder.Append('>');
        if (!element.HasElements)
        {
            builder.Append(NormalizeText(element.Value));
        }
        else
        {
            foreach (var child in element.Elements())
            {
                WriteElement(child, builder);
            }
        }

        builder.Append("</").Append(element.Name).Append('>');
    }

    private static string NormalizeText(string value)
    {
        return TimestampRegex.Replace(RelationshipIdRegex.Replace(value, "rId#"), "TIMESTAMP");
    }

    private static void ExtractXmlMarkers(string partName, XDocument document, Dictionary<string, string> markers, DocxStructureDiffOptions options)
    {
        var root = document.Root;
        if (root is null)
        {
            return;
        }

        if (partName == "word/document.xml")
        {
            var sections = root.Descendants().Where(e => e.Name.LocalName == "sectPr").ToList();
            for (var i = 0; i < sections.Count; i++)
            {
                var pgMar = sections[i].Elements().FirstOrDefault(e => e.Name.LocalName == "pgMar");
                markers[$"{partName}:section[{i}].margins"] = JoinAttrs(pgMar, "top", "bottom", "left", "right", "header", "footer");
                var pgSz = sections[i].Elements().FirstOrDefault(e => e.Name.LocalName == "pgSz");
                markers[$"{partName}:section[{i}].pageSize"] = JoinAttrs(pgSz, "w", "h", "orient");
                var pgNum = sections[i].Elements().FirstOrDefault(e => e.Name.LocalName == "pgNumType");
                markers[$"{partName}:section[{i}].pageNumber"] = JoinAttrs(pgNum, "fmt", "start");
            }

            markers[$"{partName}:field.toc.count"] = CountFields(root, "TOC").ToString();
            markers[$"{partName}:field.page.count"] = CountFields(root, "PAGE").ToString();
            markers[$"{partName}:field.ref.count"] = CountFields(root, "REF").ToString();
            markers[$"{partName}:footnoteReference.count"] = root.Descendants().Count(e => e.Name.LocalName == "footnoteReference").ToString();
            markers[$"{partName}:endnoteReference.count"] = root.Descendants().Count(e => e.Name.LocalName == "endnoteReference").ToString();

            var drawings = root.Descendants().Where(e => e.Name.LocalName == "extent").ToList();
            for (var i = 0; i < drawings.Count; i++)
            {
                markers[$"{partName}:drawing[{i}].size"] = JoinAttrs(drawings[i], "cx", "cy");
            }

            var tables = root.Descendants().Where(e => e.Name.LocalName == "tbl").ToList();
            for (var i = 0; i < tables.Count; i++)
            {
                var borders = tables[i].Descendants().FirstOrDefault(e => e.Name.LocalName == "tblBorders");
                markers[$"{partName}:table[{i}].borders"] = SummarizeBorderElement(borders);
                var width = tables[i].Descendants().FirstOrDefault(e => e.Name.LocalName == "tblW");
                markers[$"{partName}:table[{i}].width"] = JoinAttrs(width, "type", "w");
                var layout = tables[i].Descendants().FirstOrDefault(e => e.Name.LocalName == "tblLayout");
                markers[$"{partName}:table[{i}].layout"] = JoinAttrs(layout, "type");
                markers[$"{partName}:table[{i}].gridSpan.count"] = tables[i].Descendants().Count(e => e.Name.LocalName == "gridSpan").ToString();
                markers[$"{partName}:table[{i}].gridSpan.values"] = string.Join(",", tables[i].Descendants().Where(e => e.Name.LocalName == "gridSpan").Select(e => Attr(e, "val") ?? "missing"));
                markers[$"{partName}:table[{i}].vMerge.count"] = tables[i].Descendants().Count(e => e.Name.LocalName == "vMerge").ToString();
                markers[$"{partName}:table[{i}].vMerge.values"] = string.Join(",", tables[i].Descendants().Where(e => e.Name.LocalName == "vMerge").Select(e => Attr(e, "val") ?? "continue"));
                markers[$"{partName}:table[{i}].tblHeader.count"] = tables[i].Descendants().Count(e => e.Name.LocalName == "tblHeader").ToString();
                markers[$"{partName}:table[{i}].cantSplit.count"] = tables[i].Descendants().Count(e => e.Name.LocalName == "cantSplit").ToString();
                markers[$"{partName}:table[{i}].cellWidths"] = string.Join("|", tables[i].Descendants().Where(e => e.Name.LocalName == "tcW").Select(e => JoinAttrs(e, "type", "w")));
                markers[$"{partName}:table[{i}].cellVerticalAlignments"] = string.Join(",", tables[i].Descendants().Where(e => e.Name.LocalName == "vAlign").Select(e => Attr(e, "val") ?? "missing"));
                markers[$"{partName}:table[{i}].cellBorders"] = string.Join("|", tables[i].Descendants().Where(e => e.Name.LocalName == "tcBorders").Select(e => SummarizeBorderElement(e, "top", "bottom", "left", "right")));
            }
        }

        if (partName == "word/styles.xml")
        {
            foreach (var style in root.Descendants().Where(e => e.Name.LocalName == "style"))
            {
                var id = Attr(style, "styleId") ?? "unknown";
                if (id is "Heading1" or "Heading2" or "Heading3" or "ThesisBody" or "ThesisBibliography")
                {
                    markers[$"{partName}:style.{id}"] = WriteCanonical(style);
                }
            }
        }

        if (partName.StartsWith("word/footer", StringComparison.Ordinal))
        {
            markers[$"{partName}:field.page.count"] = CountFields(root, "PAGE").ToString();
        }

        if (partName is "word/footnotes.xml" or "word/endnotes.xml")
        {
            markers[$"{partName}:note.ids"] = string.Join(",", root.Descendants().Where(e => e.Name.LocalName is "footnote" or "endnote").Select(e => Attr(e, "id")).Where(v => v is not null).Order(StringComparer.Ordinal));
        }

        if (options.CompareCustomProperties && partName == "docProps/custom.xml")
        {
            foreach (var property in root.Descendants().Where(e => e.Name.LocalName == "property"))
            {
                var name = Attr(property, "name") ?? "unknown";
                markers[$"{partName}:customProperty.{name}"] = string.Concat(property.Descendants().Where(e => !e.HasElements).Select(e => e.Value));
            }
        }
    }

    private static int CountFields(XElement root, string instructionPrefix)
    {
        return root.Descendants()
            .Where(e => e.Name.LocalName == "fldSimple")
            .Select(e => Attr(e, "instr"))
            .Count(value => value?.TrimStart().StartsWith(instructionPrefix, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string JoinAttrs(XElement? element, params string[] names)
    {
        return element is null
            ? "missing"
            : string.Join(";", names.Select(name => $"{name}={Attr(element, name) ?? "missing"}"));
    }

    private static string SummarizeBorderElement(XElement? borders, params string[] names)
    {
        if (borders is null)
        {
            return "missing";
        }

        names = names.Length == 0 ? ["top", "bottom", "left", "right", "insideH", "insideV"] : names;
        return string.Join(";", names
            .Select(name =>
            {
                var edge = borders.Elements().FirstOrDefault(e => e.Name.LocalName == name);
                return $"{name}:{JoinAttrs(edge, "val", "sz", "color")}";
            }));
    }

    private static string? Attr(XElement element, string localName)
    {
        return element.Attributes().FirstOrDefault(a => a.Name.LocalName == localName)?.Value;
    }
}
