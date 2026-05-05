namespace ThesisDocx.Core.Templates.Gate;

using ThesisDocx.Core.Diagnostics;

public sealed class TemplateGateReport
{
    public string TemplateId { get; set; } = string.Empty;

    public TemplateGateStatus Status { get; set; }

    public List<TemplateGateCheck> Checks { get; set; } = [];

    public Dictionary<string, string> Artifacts { get; set; } = new(StringComparer.Ordinal);

    public double CoverageRatio { get; set; }

    public List<DiagnosticIssue> Diagnostics { get; set; } = [];

    public List<FixHint> FixHints { get; set; } = [];

    public List<TemplateGateChecklistItem> Checklist { get; set; } = [];

    public List<string> NextActions { get; set; } = [];

    public Dictionary<string, string> ArtifactPaths { get; set; } = new(StringComparer.Ordinal);
}

public enum TemplateGateStatus
{
    Pass,
    PassWithWarnings,
    Fail
}

public sealed class TemplateGateChecklistItem
{
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";

    public string Message { get; set; } = string.Empty;
}
