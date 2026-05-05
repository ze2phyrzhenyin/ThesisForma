namespace ThesisDocx.Core.Templates.Baselines;

public sealed class TemplateBaselineManifest
{
    public string SchemaVersion { get; set; } = "1.0.0";

    public string SuiteId { get; set; } = string.Empty;

    public string GeneratedAt { get; set; } = string.Empty;

    public List<TemplateBaselineEntry> Baselines { get; set; } = [];
}
