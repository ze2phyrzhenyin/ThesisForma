namespace ThesisDocx.Core.Templates.Regression;

public sealed class TemplateRegressionCase
{
    public string Id { get; set; } = string.Empty;

    public string DocumentPath { get; set; } = string.Empty;

    public string? FormatPath { get; set; }

    public string? TemplatePath { get; set; }

    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.Ordinal);

    public double MinLayoutSimilarity { get; set; } = 0.99;

    public string? BaselineLayoutPath { get; set; }

    public string? BaselineSnapshotPath { get; set; }

    public List<string> RequiredCustomProperties { get; set; } = [];

    public List<string> RequiredParts { get; set; } = [];
}
