using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Templates;

public sealed class TemplateDiffEngine
{
    public TemplateDiffResult Compare(string baseTemplatePath, string targetTemplatePath)
    {
        var baseResolved = new TemplateResolver().Resolve(baseTemplatePath);
        var targetResolved = new TemplateResolver().Resolve(targetTemplatePath);
        var result = new TemplateDiffResult
        {
            BaseTemplateId = baseResolved.Template?.Id ?? string.Empty,
            TargetTemplateId = targetResolved.Template?.Id ?? string.Empty
        };

        var baseValues = Flatten(JsonSerializer.SerializeToNode(baseResolved.FormatSpec, ThesisJson.Options), "$.formatSpec");
        var targetValues = Flatten(JsonSerializer.SerializeToNode(targetResolved.FormatSpec, ThesisJson.Options), "$.formatSpec");
        foreach (var path in baseValues.Keys.Concat(targetValues.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var hasBase = baseValues.TryGetValue(path, out var baseValue);
            var hasTarget = targetValues.TryGetValue(path, out var targetValue);
            if (hasBase && hasTarget && string.Equals(baseValue, targetValue, StringComparison.Ordinal))
            {
                continue;
            }

            result.Changes.Add(new TemplateDiffChange
            {
                Path = path,
                ChangeType = hasBase && hasTarget ? TemplateDiffChangeType.Modified : hasBase ? TemplateDiffChangeType.Removed : TemplateDiffChangeType.Added,
                BaseValue = baseValue,
                TargetValue = targetValue,
                Category = Classify(path),
                Severity = IsCoreFormatPath(path) ? TemplateDiffSeverity.Warning : TemplateDiffSeverity.Info
            });
        }

        result.Changes = result.Changes.OrderBy(change => change.Path, StringComparer.Ordinal).ToList();
        return result;
    }

    public string ToHumanReadable(TemplateDiffResult diff)
    {
        if (diff.Changes.Count == 0)
        {
            return $"No changes between {diff.BaseTemplateId} and {diff.TargetTemplateId}.";
        }

        return string.Join(Environment.NewLine, diff.Changes.Select(change =>
            $"{change.ChangeType} {change.Category} {change.Path}: {change.BaseValue ?? "<missing>"} -> {change.TargetValue ?? "<missing>"}"));
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
                values[path] = array.ToJsonString(ThesisJson.Options);
                break;
            case null:
                values[path] = null;
                break;
            default:
                values[path] = node.ToJsonString(ThesisJson.Options);
                break;
        }
    }

    private static TemplateDiffCategory Classify(string path)
    {
        if (path.Contains(".pageSetup.", StringComparison.Ordinal)) return TemplateDiffCategory.PageSetup;
        if (path.Contains(".defaultFont.", StringComparison.Ordinal)) return TemplateDiffCategory.Font;
        if (path.Contains(".bodyParagraph.", StringComparison.Ordinal)) return TemplateDiffCategory.Paragraph;
        if (path.Contains(".headings.", StringComparison.Ordinal)) return TemplateDiffCategory.Heading;
        if (path.Contains(".headerFooter.", StringComparison.Ordinal)) return TemplateDiffCategory.HeaderFooter;
        if (path.Contains(".toc.", StringComparison.Ordinal)) return TemplateDiffCategory.Toc;
        if (path.Contains(".tables.", StringComparison.Ordinal)) return TemplateDiffCategory.Table;
        if (path.Contains(".figures.", StringComparison.Ordinal)) return TemplateDiffCategory.Figure;
        if (path.Contains(".equations.", StringComparison.Ordinal)) return TemplateDiffCategory.Equation;
        if (path.Contains(".bibliography.", StringComparison.Ordinal)) return TemplateDiffCategory.Bibliography;
        return TemplateDiffCategory.Unknown;
    }

    private static bool IsCoreFormatPath(string path)
    {
        return Classify(path) is not TemplateDiffCategory.Unknown;
    }
}
