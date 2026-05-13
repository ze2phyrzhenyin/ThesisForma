using System.Text.Json.Nodes;

namespace ThesisDocx.Core.Templates.Authoring;

public sealed class FormatCandidateDecisionSet
{
    public string SchemaVersion { get; set; } = "1.0.0";

    public string SourceCandidateFormatSpec { get; set; } = string.Empty;

    public string SourceCandidateReport { get; set; } = string.Empty;

    public string Reviewer { get; set; } = string.Empty;

    public string ReviewedAt { get; set; } = string.Empty;

    public bool RiskAccepted { get; set; }

    public string RiskAcceptanceReason { get; set; } = string.Empty;

    public List<FormatCandidateDecision> Decisions { get; set; } = [];
}

public sealed class FormatCandidateDecision
{
    public string FieldPath { get; set; } = string.Empty;

    public FormatCandidateDecisionKind Decision { get; set; } = FormatCandidateDecisionKind.Reject;

    public JsonNode? Value { get; set; }

    public string Reviewer { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public List<string> EvidencePaths { get; set; } = [];
}

public enum FormatCandidateDecisionKind
{
    Accept,
    Reject,
    Modify
}

public sealed class TemplateCandidateProposalOptions
{
    public string TemplatePath { get; set; } = string.Empty;

    public string CandidateFormatSpecPath { get; set; } = string.Empty;

    public string CandidateReportPath { get; set; } = string.Empty;

    public string DecisionsPath { get; set; } = string.Empty;

    public string OutputTemplatePath { get; set; } = string.Empty;
}

public sealed class TemplateCandidateProposalResult
{
    public TemplateCandidateProposalReport Report { get; set; } = new();
}

public sealed class TemplateCandidateProposalReport
{
    public string ReportVersion { get; set; } = "1.0.0";

    public string Status { get; set; } = "fail";

    public string SourceTemplate { get; set; } = string.Empty;

    public string ProposedTemplate { get; set; } = string.Empty;

    public string SourceCandidateFormatSpec { get; set; } = string.Empty;

    public string SourceCandidateReport { get; set; } = string.Empty;

    public string DecisionsPath { get; set; } = string.Empty;

    public string ChaosLevel { get; set; } = "unknown";

    public double ChaosScore { get; set; }

    public bool RiskAccepted { get; set; }

    public int CandidateFieldCount { get; set; }

    public int AcceptedCount { get; set; }

    public int ModifiedCount { get; set; }

    public int RejectedCount { get; set; }

    public int SkippedCount { get; set; }

    public List<TemplateCandidateProposalAppliedField> AppliedFields { get; set; } = [];

    public List<TemplateCandidateProposalIssue> Issues { get; set; } = [];

    public List<string> Artifacts { get; set; } = [];
}

public sealed class TemplateCandidateProposalAppliedField
{
    public string FieldPath { get; set; } = string.Empty;

    public string Decision { get; set; } = string.Empty;

    public string Reviewer { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string ValuePreview { get; set; } = string.Empty;

    public List<string> EvidencePaths { get; set; } = [];
}

public sealed class TemplateCandidateProposalIssue
{
    public string Code { get; set; } = string.Empty;

    public string Severity { get; set; } = "error";

    public string Path { get; set; } = "$";

    public string Message { get; set; } = string.Empty;

    public string FixHint { get; set; } = string.Empty;
}
