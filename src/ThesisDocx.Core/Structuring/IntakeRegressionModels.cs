using ThesisDocx.Core.Diagnostics;

namespace ThesisDocx.Core.Structuring;

public sealed class IntakeRegressionManifest
{
    public string ManifestVersion { get; set; } = "1.0.0";
    public string Name { get; set; } = string.Empty;
    public string WorkspaceRoot { get; set; } = "intake-regression-workspaces";
    public string DefaultTemplate { get; set; } = string.Empty;
    public string DefaultStructureMode { get; set; } = "auto";
    public int MinimumStructureQualityScore { get; set; }
    public List<string> DefaultAllowedCodexStatuses { get; set; } = ["skipped", "pass", "fallback", "notRequested"];
    public List<IntakeRegressionCase> Cases { get; set; } = [];
}

public sealed class IntakeRegressionCase
{
    public string Id { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Workspace { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string StructureMode { get; set; } = string.Empty;
    public string ExpectedStatus { get; set; } = "pass";
    public int? MinimumStructureQualityScore { get; set; }
    public int? MaximumUnresolvedCount { get; set; }
    public bool RequireCodexReview { get; set; }
    public List<string> AllowedCodexStatuses { get; set; } = [];
    public List<string> ExpectedTextSnippets { get; set; } = [];
}

public sealed class IntakeRegressionReport
{
    public string ReportVersion { get; set; } = "1.0.0";
    public string Status { get; set; } = "notRun";
    public string ManifestPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CaseCount { get; set; }
    public int PassedCaseCount { get; set; }
    public int FailedCaseCount { get; set; }
    public List<IntakeRegressionCaseResult> Cases { get; set; } = [];
    public List<string> BlockingIssues { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> Artifacts { get; set; } = [];
    public List<UnifiedDiagnostic> Diagnostics { get; set; } = [];
}

public sealed class IntakeRegressionCaseResult
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = "notRun";
    public string InputPath { get; set; } = string.Empty;
    public string WorkspacePath { get; set; } = string.Empty;
    public string TemplatePath { get; set; } = string.Empty;
    public string IntakeReportPath { get; set; } = string.Empty;
    public string IntakeReportMarkdownPath { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string ExpectedStatus { get; set; } = "pass";
    public string StructureMode { get; set; } = string.Empty;
    public string StructureAnalysisStatus { get; set; } = string.Empty;
    public string StructureAnalysisRiskLevel { get; set; } = string.Empty;
    public int StructureQualityScore { get; set; }
    public string CodexReviewStatus { get; set; } = string.Empty;
    public int UnresolvedCount { get; set; }
    public bool RenderAttempted { get; set; }
    public bool RenderValid { get; set; }
    public List<string> BlockingIssues { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> Artifacts { get; set; } = [];
}
