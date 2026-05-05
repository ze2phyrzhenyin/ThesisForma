using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Diff.Layout;

public sealed class LayoutSignatureComparer
{
    private readonly DocxDiffSeverityClassifier _classifier = new();

    public LayoutSignatureCompareResult Compare(DocxLayoutSignature baseSignature, DocxLayoutSignature targetSignature, double threshold = 0.99)
    {
        var baseValues = Flatten(JsonSerializer.SerializeToNode(baseSignature, ThesisJson.Options), "$");
        var targetValues = Flatten(JsonSerializer.SerializeToNode(targetSignature, ThesisJson.Options), "$");
        baseValues.Remove("$.sourcePath");
        targetValues.Remove("$.sourcePath");

        var differences = new List<LayoutSignatureDifference>();
        var allPaths = baseValues.Keys.Concat(targetValues.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        foreach (var path in allPaths)
        {
            var baseValue = baseValues.GetValueOrDefault(path);
            var targetValue = targetValues.GetValueOrDefault(path);
            if (string.Equals(baseValue, targetValue, StringComparison.Ordinal))
            {
                continue;
            }

            var category = Classify(path);
            differences.Add(new LayoutSignatureDifference
            {
                Path = path,
                BaseValue = baseValue,
                TargetValue = targetValue,
                Category = category,
                Severity = _classifier.Classify(category, path, DocxStructureDiffChangeType.Modified)
            });
        }

        var denominator = Math.Max(1, allPaths.Count);
        var similarity = (denominator - differences.Count) / (double)denominator;
        return new LayoutSignatureCompareResult
        {
            SimilarityScore = Math.Round(similarity, 6),
            Threshold = threshold,
            MeetsThreshold = similarity >= threshold,
            Differences = differences,
            BreakingDifferences = differences.Where(d => d.Severity == DocxDiffSeverity.Breaking).ToList(),
            Warnings = differences.Where(d => d.Severity == DocxDiffSeverity.Warning).ToList()
        };
    }

    private static Dictionary<string, string?> Flatten(JsonNode? node, string path)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        FlattenInto(node, path, values);
        return values;
    }

    private static void FlattenInto(JsonNode? node, string path, Dictionary<string, string?> values)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    FlattenInto(property.Value, $"{path}.{property.Key}", values);
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    FlattenInto(array[i], $"{path}[{i}]", values);
                }

                if (array.Count == 0)
                {
                    values[path] = "[]";
                }

                break;
            case null:
                values[path] = null;
                break;
            default:
                values[path] = node.ToJsonString(ThesisJson.Options);
                break;
        }
    }

    private static string Classify(string path)
    {
        if (path.Contains(".sections", StringComparison.Ordinal) && (path.Contains("Margin", StringComparison.Ordinal) || path.Contains("PageWidth", StringComparison.Ordinal) || path.Contains("PageHeight", StringComparison.Ordinal))) return "pageSetup";
        if (path.Contains(".sections", StringComparison.Ordinal) && path.Contains("PageNumber", StringComparison.Ordinal)) return "pageNumber";
        if (path.Contains(".styles", StringComparison.Ordinal)) return "headingStyle";
        if (path.Contains(".tables", StringComparison.Ordinal)) return "table";
        if (path.Contains(".figures", StringComparison.Ordinal)) return "figure";
        if (path.Contains(".equations", StringComparison.Ordinal)) return "equation";
        if (path.Contains(".fields.toc", StringComparison.OrdinalIgnoreCase)) return "toc";
        if (path.Contains(".fields.page", StringComparison.OrdinalIgnoreCase)) return "pageNumber";
        if (path.Contains(".footnotes", StringComparison.OrdinalIgnoreCase) || path.Contains(".endnotes", StringComparison.OrdinalIgnoreCase)) return "notes";
        if (path.Contains(".customProperties", StringComparison.Ordinal)) return "customProperties";
        if (path.Contains(".bibliography", StringComparison.Ordinal)) return "bibliography";
        return "layout";
    }
}
