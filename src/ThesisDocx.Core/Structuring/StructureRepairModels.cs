using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models;

namespace ThesisDocx.Core.Structuring;

public sealed class StructureAnalysisReport
{
    public string ReportVersion { get; set; } = "1.0.0";
    public string Status { get; set; } = "pass";
    public string RiskLevel { get; set; } = "low";
    public int QualityScore { get; set; } = 100;
    public bool RecommendCodexReview { get; set; }
    public int IssueCount { get; set; }
    public List<StructureAnalysisIssue> Issues { get; set; } = [];
    public List<string> RecommendedActions { get; set; } = [];
}

public sealed class StructureAnalysisIssue
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = DiagnosticSeverity.Warning;
    public string EvidencePath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public sealed class StructureRepairPlan
{
    public string PlanVersion { get; set; } = "1.0.0";
    public string Summary { get; set; } = string.Empty;
    public List<StructureRepairOperation> Operations { get; set; } = [];
    public List<string> ReviewerNotes { get; set; } = [];
}

public sealed class StructureRepairOperation
{
    public string Id { get; set; } = string.Empty;
    public StructureRepairOperationType Type { get; set; } = StructureRepairOperationType.MoveBlock;
    public string SourceEvidencePath { get; set; } = string.Empty;
    public string TargetEvidencePath { get; set; } = string.Empty;
    public string TargetSectionId { get; set; } = string.Empty;
    public string TargetSectionTitle { get; set; } = string.Empty;
    public ThesisSectionKind? TargetSectionKind { get; set; }
    public string BeforeEvidencePath { get; set; } = string.Empty;
    public string AfterEvidencePath { get; set; } = string.Empty;
    public int? HeadingLevel { get; set; }
    public string UnresolvedCode { get; set; } = string.Empty;
    public string UnresolvedMessage { get; set; } = string.Empty;
    public string Severity { get; set; } = DiagnosticSeverity.Warning;
    public string RecommendedAction { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public double Confidence { get; set; } = 0.5;
}

public enum StructureRepairOperationType
{
    MoveBlock,
    EnsureSection,
    AddUnresolvedItem,
    RemoveUnresolvedItem,
    UpdateHeadingLevel,
    PromoteParagraphToHeading,
    DemoteHeadingToParagraph
}

public sealed class StructureRepairApplyReport
{
    public string ReportVersion { get; set; } = "1.0.0";
    public string Status { get; set; } = "pass";
    public int PlannedOperationCount { get; set; }
    public int AppliedOperationCount { get; set; }
    public int RejectedOperationCount { get; set; }
    public int MovedBlockCount { get; set; }
    public int AddedSectionCount { get; set; }
    public int AddedUnresolvedCount { get; set; }
    public int RemovedUnresolvedCount { get; set; }
    public int UpdatedHeadingCount { get; set; }
    public int PromotedHeadingCount { get; set; }
    public int DemotedHeadingCount { get; set; }
    public List<StructureRepairOperationAudit> Operations { get; set; } = [];
    public List<UnifiedDiagnostic> Diagnostics { get; set; } = [];
}

public sealed class StructureRepairOperationAudit
{
    public string OperationId { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string Status { get; set; } = "applied";
    public string SourceEvidencePath { get; set; } = string.Empty;
    public string TargetSectionId { get; set; } = string.Empty;
    public string BeforePath { get; set; } = string.Empty;
    public string AfterPath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public double Confidence { get; set; }
}
