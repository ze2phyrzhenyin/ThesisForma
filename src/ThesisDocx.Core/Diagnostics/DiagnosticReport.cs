namespace ThesisDocx.Core.Diagnostics;

public sealed class DiagnosticReport
{
    public string Status { get; set; } = "pass";

    public DiagnosticReportSummary Summary { get; set; } = new();

    public int IssueCount => Issues.Count;

    public int BreakingCount => Issues.Count(i => UnifiedDiagnosticMapper.IsError(i.Severity));

    public int WarningCount => Issues.Count(i => UnifiedDiagnosticMapper.IsWarning(i.Severity));

    public List<DiagnosticIssue> Issues { get; set; } = [];

    public List<UnifiedDiagnostic> Diagnostics => Issues
        .Select(UnifiedDiagnosticMapper.FromDiagnosticIssue)
        .ToList();

    public List<DiagnosticIssueGroup> GroupedIssues { get; set; } = [];

    public List<string> TopRecommendedActions { get; set; } = [];

    public string? MarkdownSummary { get; set; }

    public Dictionary<string, string> RelatedArtifacts { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> ArtifactPaths { get; set; } = new(StringComparer.Ordinal);
}

public sealed class DiagnosticReportSummary
{
    public string Status { get; set; } = "pass";

    public int TotalIssues { get; set; }

    public int BreakingIssues { get; set; }

    public int Warnings { get; set; }

    public List<string> TopCategories { get; set; } = [];

    public List<string> TopSpecPaths { get; set; } = [];
}

public sealed class DiagnosticIssueGroup
{
    public string GroupKey { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public int Count { get; set; }

    public List<DiagnosticIssue> Issues { get; set; } = [];
}
