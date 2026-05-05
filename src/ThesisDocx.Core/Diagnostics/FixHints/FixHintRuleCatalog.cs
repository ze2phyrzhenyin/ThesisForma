using System.Text.Json;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Diagnostics.FixHints;

public sealed class FixHintRuleCatalog
{
    public List<FixHintRule> Rules { get; set; } = [];

    public static FixHintRuleCatalog LoadDefault()
    {
        var path = LocateRulesFile();
        if (path is not null)
        {
            return JsonSerializer.Deserialize<FixHintRuleCatalog>(File.ReadAllText(path), ThesisJson.Options)
                ?? new FixHintRuleCatalog();
        }

        return new FixHintRuleCatalog();
    }

    private static string? LocateRulesFile()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "resources", "fix-hint-rules.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }
        }

        return null;
    }
}
