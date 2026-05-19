using ThesisDocx.Core.Models;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Validation.ContentPreservation;

namespace ThesisDocx.Core.Structuring;

public sealed class ThesisStructuringInput
{
    public string ExtractionPath { get; set; } = string.Empty;
}

public sealed class ThesisStructuringResult
{
    public ThesisDocument Document { get; set; } = new();
    public ThesisStructureMappingReport Report { get; set; } = new();
    public List<ThesisStructureUnresolvedItem> UnresolvedItems { get; set; } = [];
    public List<ThesisStructureEvidenceLink> EvidenceLinks { get; set; } = [];
}

public sealed class ThesisStructureMappingReport
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public string SourceExtraction { get; set; } = string.Empty;
    public int RuleBasedMappedCount { get; set; }
    public int UnresolvedCount { get; set; }
    public int LowConfidenceCount { get; set; }
    public double EvidenceCoverageRatio { get; set; }
    public ContentPreservationResult ContentPreservation { get; set; } = new();
    public List<string> RecommendedCodexReviewSteps { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> BlockingIssues { get; set; } = [];
    public List<ThesisStructureEvidenceLink> EvidenceLinks { get; set; } = [];
}

public sealed class ThesisStructureEvidenceLink
{
    public string StructuredPath { get; set; } = string.Empty;
    public string EvidencePath { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public sealed class ThesisStructureUnresolvedItem
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string Message { get; set; } = string.Empty;
    public string EvidencePath { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
}

public sealed class ThesisDocumentDraftValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class IntakeDocxReport
{
    public string ReportVersion { get; set; } = "1.0.0";
    public string InputDocx { get; set; } = string.Empty;
    public string ExtractionStatus { get; set; } = "notRun";
    public string FormatCandidateStatus { get; set; } = "notRun";
    public string FormatChaosLevel { get; set; } = "notRun";
    public double FormatChaosScore { get; set; }
    public int FormatCandidateGeneratedFieldCount { get; set; }
    public int FormatCandidateUnresolvedCount { get; set; }
    public string StructureMode { get; set; } = "rule";
    public string StructureAnalysisStatus { get; set; } = "notRun";
    public string StructureAnalysisRiskLevel { get; set; } = "notRun";
    public int StructureQualityScore { get; set; }
    public int StructureAnalysisIssueCount { get; set; }
    public bool StructureAnalysisRecommendedCodexReview { get; set; }
    public string StructuringStatus { get; set; } = "notRun";
    public string CodexReviewStatus { get; set; } = "notRequested";
    public int? CodexReviewExitCode { get; set; }
    public string? CodexReviewReportPath { get; set; }
    public string DraftContentPreservationStatus { get; set; } = "notRun";
    public int DraftContentMissingSegments { get; set; }
    public int DraftContentBlockingIssues { get; set; }
    public bool ThesisDocumentDraftValid { get; set; }
    public bool RenderAttempted { get; set; }
    public bool RenderValid { get; set; }
    public int UnresolvedCount { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> BlockingIssues { get; set; } = [];
    public List<UnifiedDiagnostic> Diagnostics { get; set; } = [];
    public List<string> RecommendedNextActions { get; set; } = [];
    public List<string> Artifacts { get; set; } = [];
}

public sealed class CodexStructureReviewOptions
{
    public string WorkspacePath { get; set; } = string.Empty;
    public string ExtractionPath { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string MappingReportPath { get; set; } = string.Empty;
    public string UnresolvedPath { get; set; } = string.Empty;
    public string? EvidencePath { get; set; }
    public string PromptPath { get; set; } = string.Empty;
    public string ReviewReportPath { get; set; } = string.Empty;
    public string StructureAnalysisPath { get; set; } = string.Empty;
    public string RepairPlanPath { get; set; } = string.Empty;
    public string RepairPlanSchemaPath { get; set; } = string.Empty;
    public string RepairApplyReportPath { get; set; } = string.Empty;
    public string? FormatCandidateReportPath { get; set; }
    public string? TemplatePath { get; set; }
    public string CodexCommand { get; set; } = "codex";
    public string CodexSandbox { get; set; } = "workspace-write";
    public string CodexApprovalPolicy { get; set; } = "never";
    public string? Model { get; set; }
    public string? Profile { get; set; }
    public bool SkipGitRepoCheck { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 900;
    public List<string> ExtraArguments { get; set; } = [];
}

public sealed class CodexStructureReviewReport
{
    public string ReportVersion { get; set; } = "1.0.0";
    public string Status { get; set; } = "notRun";
    public bool CodexInvoked { get; set; }
    public string CodexCommand { get; set; } = string.Empty;
    public List<string> CodexArguments { get; set; } = [];
    public int? CodexExitCode { get; set; }
    public bool TimedOut { get; set; }
    public string WorkspacePath { get; set; } = string.Empty;
    public string ExtractionPath { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string MappingReportPath { get; set; } = string.Empty;
    public string UnresolvedPath { get; set; } = string.Empty;
    public string? EvidencePath { get; set; }
    public string PromptPath { get; set; } = string.Empty;
    public string LastMessagePath { get; set; } = string.Empty;
    public string StructureAnalysisPath { get; set; } = string.Empty;
    public string RepairPlanPath { get; set; } = string.Empty;
    public string RepairPlanSchemaPath { get; set; } = string.Empty;
    public string RepairApplyReportPath { get; set; } = string.Empty;
    public string DraftHashBefore { get; set; } = string.Empty;
    public string StructuredArtifactHashBefore { get; set; } = string.Empty;
    public string DraftHashAfterCodex { get; set; } = string.Empty;
    public string StructuredArtifactHashAfterCodex { get; set; } = string.Empty;
    public string DraftHashAfter { get; set; } = string.Empty;
    public bool DirectArtifactEditDetected { get; set; }
    public string StructureAnalysisRiskLevel { get; set; } = "notRun";
    public int StructureQualityScore { get; set; }
    public bool StructureAnalysisRecommendedCodexReview { get; set; }
    public int StructureAnalysisIssueCount { get; set; }
    public int PlannedOperationCount { get; set; }
    public int AppliedOperationCount { get; set; }
    public int RejectedOperationCount { get; set; }
    public int MovedBlockCount { get; set; }
    public string DraftContentPreservationStatus { get; set; } = "notRun";
    public int DraftContentMissingSegments { get; set; }
    public int DraftContentBlockingIssues { get; set; }
    public int EvidenceLinkCount { get; set; }
    public List<string> Artifacts { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> BlockingIssues { get; set; } = [];
    public List<UnifiedDiagnostic> Diagnostics { get; set; } = [];
    public string StandardOutputExcerpt { get; set; } = string.Empty;
    public string StandardErrorExcerpt { get; set; } = string.Empty;
    public string LastMessageExcerpt { get; set; } = string.Empty;
}
