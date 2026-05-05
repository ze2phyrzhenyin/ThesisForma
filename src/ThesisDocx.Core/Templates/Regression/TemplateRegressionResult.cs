namespace ThesisDocx.Core.Templates.Regression;

using ThesisDocx.Core.Diagnostics;

public sealed class TemplateRegressionResult
{
    public string SuiteName { get; set; } = string.Empty;

    public bool Passed => Cases.All(c => c.Passed);

    public List<TemplateRegressionCaseResult> Cases { get; set; } = [];

    public List<string> FailedCases { get; set; } = [];

    public List<DiagnosticIssue> CaseDiagnostics { get; set; } = [];

    public TemplateRegressionBaselineSummary BaselineSummary { get; set; } = new();

    public List<string> NextActions { get; set; } = [];
}

public sealed class TemplateRegressionCaseResult
{
    public string Id { get; set; } = string.Empty;

    public bool Passed => Errors.Count == 0;

    public List<string> Errors { get; set; } = [];

    public List<string> Warnings { get; set; } = [];

    public Dictionary<string, string> Artifacts { get; set; } = new(StringComparer.Ordinal);

    public int OpenXmlErrorCount { get; set; }

    public bool FormatConformanceValid { get; set; }

    public double LayoutSimilarity { get; set; } = 1.0;

    public bool SnapshotMatches { get; set; } = true;

    public bool RequiredCustomPropertiesPassed { get; set; } = true;

    public bool RequiredPartsPassed { get; set; } = true;
}

public sealed class TemplateRegressionBaselineSummary
{
    public int TotalCases { get; set; }

    public int ComparedCases { get; set; }

    public int SnapshotMatches { get; set; }

    public int LayoutBelowThreshold { get; set; }
}
