using ThesisDocx.Core.Diagnostics;

namespace ThesisDocx.Core.Ci;

public sealed class CiQualityReport
{
    public string Status { get; set; } = "pass";

    public List<CiQualityCheck> Checks { get; set; } = [];

    public Dictionary<string, string> Artifacts { get; set; } = new(StringComparer.Ordinal);

    public List<DiagnosticIssue> BlockingIssues { get; set; } = [];

    public List<DiagnosticIssue> Warnings { get; set; } = [];

    public List<UnifiedDiagnostic> Diagnostics => BlockingIssues
        .Concat(Warnings)
        .Select(UnifiedDiagnosticMapper.FromDiagnosticIssue)
        .ToList();

    public List<string> RecommendedNextActions { get; set; } = [];

    public string MergeDecision { get; set; } = "approve";

    public int QualityScore { get; set; } = 100;
}
