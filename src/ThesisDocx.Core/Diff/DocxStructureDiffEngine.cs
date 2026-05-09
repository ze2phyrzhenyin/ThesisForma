namespace ThesisDocx.Core.Diff;

public sealed class DocxStructureDiffEngine
{
    private readonly DocxXmlCanonicalizer _canonicalizer = new();
    private readonly DocxDiffSeverityClassifier _severityClassifier = new();

    public DocxStructureDiffResult Compare(string baseDocxPath, string targetDocxPath, DocxStructureDiffOptions? options = null)
    {
        options ??= new DocxStructureDiffOptions();
        var result = new DocxStructureDiffResult
        {
            BasePath = RedactPath(baseDocxPath),
            TargetPath = RedactPath(targetDocxPath)
        };

        var baseParts = _canonicalizer.ReadCanonicalParts(baseDocxPath, options);
        var targetParts = _canonicalizer.ReadCanonicalParts(targetDocxPath, options);
        foreach (var partName in baseParts.Keys.Concat(targetParts.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var hasBase = baseParts.TryGetValue(partName, out var baseValue);
            var hasTarget = targetParts.TryGetValue(partName, out var targetValue);
            if (hasBase && hasTarget)
            {
                continue;
            }

            AddChange(result, partName, partName, hasBase ? DocxStructureDiffChangeType.Removed : DocxStructureDiffChangeType.Added, ClassifyPart(partName), hasBase ? "DOCX part removed." : "DOCX part added.", baseValue, targetValue);
        }

        var baseMarkers = _canonicalizer.ExtractMarkers(baseDocxPath, options);
        var targetMarkers = _canonicalizer.ExtractMarkers(targetDocxPath, options);
        foreach (var marker in baseMarkers.Keys.Concat(targetMarkers.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var hasBase = baseMarkers.TryGetValue(marker, out var baseValue);
            var hasTarget = targetMarkers.TryGetValue(marker, out var targetValue);
            if (hasBase && hasTarget && string.Equals(baseValue, targetValue, StringComparison.Ordinal))
            {
                continue;
            }

            var partName = marker.Split(':', 2)[0];
            var category = ClassifyMarker(marker);
            AddChange(result, marker, partName, hasBase && hasTarget ? DocxStructureDiffChangeType.Modified : hasBase ? DocxStructureDiffChangeType.Removed : DocxStructureDiffChangeType.Added, category, $"DOCX structural marker changed: {marker}.", baseValue, targetValue);
        }

        if (options.IncludeGenericXmlChanges)
        {
            foreach (var partName in baseParts.Keys.Intersect(targetParts.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                if (string.Equals(baseParts[partName], targetParts[partName], StringComparison.Ordinal))
                {
                    continue;
                }

                if (result.Changes.Any(change => change.PartName == partName))
                {
                    continue;
                }

                AddChange(result, $"{partName}:xml", partName, DocxStructureDiffChangeType.Modified, ClassifyPart(partName), $"Canonical XML changed in {partName}.", Hash(baseParts[partName]), Hash(targetParts[partName]));
            }
        }

        result.Changes = result.Changes
            .OrderBy(change => change.PartName, StringComparer.Ordinal)
            .ThenBy(change => change.Path, StringComparer.Ordinal)
            .ThenBy(change => change.ChangeType)
            .ToList();
        return result;
    }

    private static string RedactPath(string path)
    {
        return Path.GetFileName(path);
    }

    private void AddChange(DocxStructureDiffResult result, string path, string? partName, DocxStructureDiffChangeType type, string category, string message, string? baseValue, string? targetValue)
    {
        result.Changes.Add(new DocxStructureDiffChange
        {
            Path = path,
            PartName = partName,
            ChangeType = type,
            Category = category,
            Message = message,
            BaseValue = baseValue,
            TargetValue = targetValue,
            Severity = _severityClassifier.Classify(category, path, type)
        });
    }

    private static string ClassifyMarker(string marker)
    {
        if (marker.Contains(".margins", StringComparison.Ordinal) || marker.Contains(".pageSize", StringComparison.Ordinal)) return "pageSetup";
        if (marker.Contains(":style.Heading", StringComparison.Ordinal) || marker.Contains(":style.ThesisBody", StringComparison.Ordinal)) return "headingStyle";
        if (marker.Contains("field.toc", StringComparison.Ordinal)) return "toc";
        if (marker.Contains("field.page", StringComparison.Ordinal) || marker.Contains(".pageNumber", StringComparison.Ordinal)) return "pageNumber";
        if (marker.Contains(":table[", StringComparison.Ordinal)) return "table";
        if (marker.Contains(":drawing[", StringComparison.Ordinal)) return "figure";
        if (marker.Contains("footnote", StringComparison.OrdinalIgnoreCase) || marker.Contains("endnote", StringComparison.OrdinalIgnoreCase)) return "notes";
        if (marker.Contains("customProperty", StringComparison.Ordinal)) return "customProperties";
        return "xml";
    }

    private static string ClassifyPart(string partName)
    {
        if (partName.Contains("footnotes", StringComparison.OrdinalIgnoreCase) || partName.Contains("endnotes", StringComparison.OrdinalIgnoreCase)) return "notes";
        if (partName.Contains("custom", StringComparison.OrdinalIgnoreCase)) return "customProperties";
        if (partName.Contains("styles", StringComparison.OrdinalIgnoreCase)) return "headingStyle";
        if (partName.Contains("media", StringComparison.OrdinalIgnoreCase)) return "figure";
        return "package";
    }

    private static string Hash(string value)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }
}
