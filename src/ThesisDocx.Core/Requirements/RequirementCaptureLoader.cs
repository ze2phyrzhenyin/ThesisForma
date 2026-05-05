using System.Text.Json;
using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Requirements;

public sealed class RequirementCaptureLoader
{
    public RequirementCapture Load(string path)
    {
        return JsonSerializer.Deserialize<RequirementCapture>(File.ReadAllText(path), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize requirement capture '{path}'.");
    }
}
