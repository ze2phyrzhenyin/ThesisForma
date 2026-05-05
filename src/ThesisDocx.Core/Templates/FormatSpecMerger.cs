using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Templates;

public sealed class FormatSpecMerger
{
    public ThesisFormatSpec Merge(ThesisFormatSpec parent, JsonNode? childOverride)
    {
        var parentNode = JsonSerializer.SerializeToNode(parent, ThesisJson.Options)
            ?? throw new InvalidOperationException("Could not serialize parent format spec.");
        var merged = MergeNodes(parentNode, childOverride?.DeepClone()) ?? parentNode;
        return merged.Deserialize<ThesisFormatSpec>(ThesisJson.Options)
            ?? throw new InvalidOperationException("Could not deserialize merged format spec.");
    }

    public JsonNode? MergeNodes(JsonNode? parent, JsonNode? child)
    {
        if (child is null)
        {
            return null;
        }

        if (parent is JsonObject parentObject && child is JsonObject childObject)
        {
            var result = new JsonObject();
            foreach (var property in parentObject)
            {
                result[property.Key] = property.Value?.DeepClone();
            }

            foreach (var property in childObject)
            {
                result[property.Key] = property.Value is null
                    ? null
                    : result.TryGetPropertyValue(property.Key, out var existing)
                        ? MergeNodes(existing, property.Value)
                        : property.Value.DeepClone();
            }

            return result;
        }

        return child.DeepClone();
    }
}
