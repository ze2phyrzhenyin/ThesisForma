namespace ThesisDocx.Core.Templates.Baselines;

public sealed class TemplateBaselineEntry
{
    public string CaseId { get; set; } = string.Empty;

    public string TemplateId { get; set; } = string.Empty;

    public string TemplateVersion { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public string SnapshotPath { get; set; } = string.Empty;

    public string LayoutSignaturePath { get; set; } = string.Empty;

    public string InspectPath { get; set; } = string.Empty;

    public string StructureDiffPolicy { get; set; } = "failOnBreaking";

    public double LayoutThreshold { get; set; } = 0.99;

    public List<string> AllowedDiffCategories { get; set; } = [];

    public List<string> ForbiddenDiffCategories { get; set; } = [];

    public string? LastUpdatedAt { get; set; }

    public string Notes { get; set; } = string.Empty;
}
