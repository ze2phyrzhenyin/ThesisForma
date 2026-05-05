namespace ThesisDocx.Core.Ci;

public sealed class CiQualityCheck
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = "pass";

    public string Message { get; set; } = string.Empty;

    public Dictionary<string, string> Artifacts { get; set; } = new(StringComparer.Ordinal);
}
