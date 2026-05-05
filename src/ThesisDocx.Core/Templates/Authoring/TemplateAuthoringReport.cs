using ThesisDocx.Core.Diagnostics;

namespace ThesisDocx.Core.Templates.Authoring;

public sealed class TemplateAuthoringReport
{
    public string TemplateId { get; set; } = string.Empty;

    public string TemplateVersion { get; set; } = string.Empty;

    public Dictionary<string, object> TemplateSummary { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, object> RequirementMappingSummary { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, object> ValidationSummary { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, object> RegressionSummary { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, object> BaselineSummary { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, object> CoverageSummary { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, object> DiagnosticSummary { get; set; } = new(StringComparer.Ordinal);

    public List<TemplateAuthoringChecklistItem> Checklist { get; set; } = [];

    public List<DiagnosticIssue> BlockingIssues { get; set; } = [];

    public List<DiagnosticIssue> Warnings { get; set; } = [];

    public List<string> RecommendedNextActions { get; set; } = [];

    public string PublishReadiness { get; set; } = "notReady";

    public int QualityScore { get; set; }

    public List<string> ReadinessReasons { get; set; } = [];

    public List<TemplateAuthoringChecklistItem> FailedChecklistItems { get; set; } = [];

    public List<TemplateAuthoringChecklistItem> WarningChecklistItems { get; set; } = [];

    public string BaselineStatus { get; set; } = "unknown";

    public string RequirementMappingStatus { get; set; } = "unknown";

    public string RegressionStatus { get; set; } = "unknown";

    public string GateStatus { get; set; } = "unknown";

    public string DiagnosticStatus { get; set; } = "unknown";

    public string SuggestedMergeDecision { get; set; } = "reject";

    public Dictionary<string, string> RelatedArtifacts { get; set; } = new(StringComparer.Ordinal);
}
