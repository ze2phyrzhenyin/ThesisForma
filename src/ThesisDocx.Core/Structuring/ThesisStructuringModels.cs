using ThesisDocx.Core.Models;
using ThesisDocx.Core.Diagnostics;

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
    public string InputDocx { get; set; } = string.Empty;
    public string ExtractionStatus { get; set; } = "notRun";
    public string StructuringStatus { get; set; } = "notRun";
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
