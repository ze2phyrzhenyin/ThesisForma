using ThesisDocx.Core.Models;

namespace ThesisDocx.Core.Extraction;

public sealed class DocxFormatCandidateResult
{
    public ThesisFormatSpec CandidateFormatSpec { get; set; } = new();
    public DocxFormatCandidateReport Report { get; set; } = new();
}

public sealed class DocxFormatCandidateReport
{
    public string ReportVersion { get; set; } = "1.0.0";
    public string SourceExtraction { get; set; } = string.Empty;
    public string CandidateStatus { get; set; } = "needsReview";
    public string CandidateFormatSpecName { get; set; } = string.Empty;
    public string ChaosLevel { get; set; } = "low";
    public double ChaosScore { get; set; }
    public int GeneratedFieldCount { get; set; }
    public List<DocxFormatCandidateField> GeneratedFields { get; set; } = [];
    public List<DocxFormatCandidateClusterUse> ClustersUsed { get; set; } = [];
    public List<DocxFormatCandidateUnresolvedItem> UnresolvedItems { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> RecommendedReviewSteps { get; set; } = [];
}

public sealed class DocxFormatCandidateField
{
    public string Path { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string SourceClusterId { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<string> EvidencePaths { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

public sealed class DocxFormatCandidateClusterUse
{
    public string ClusterId { get; set; } = string.Empty;
    public string RoleHint { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int UsageCount { get; set; }
    public List<string> EvidencePaths { get; set; } = [];
    public List<string> Variance { get; set; } = [];
}

public sealed class DocxFormatCandidateUnresolvedItem
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string Message { get; set; } = string.Empty;
    public List<string> EvidencePaths { get; set; } = [];
    public string RecommendedAction { get; set; } = string.Empty;
}
