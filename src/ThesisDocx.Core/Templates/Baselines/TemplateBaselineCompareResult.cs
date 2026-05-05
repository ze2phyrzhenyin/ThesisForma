namespace ThesisDocx.Core.Templates.Baselines;

public sealed class TemplateBaselineCompareResult
{
    public string SuiteId { get; set; } = string.Empty;

    public bool Passed => Cases.All(c => c.Passed);

    public List<TemplateBaselineCaseCompareResult> Cases { get; set; } = [];
}

public sealed class TemplateBaselineCaseCompareResult
{
    public string CaseId { get; set; } = string.Empty;

    public string? FixtureId { get; set; }

    public bool Passed => Errors.Count == 0;

    public double LayoutSimilarity { get; set; } = 1.0;

    public bool SnapshotMatches { get; set; } = true;

    public List<string> Errors { get; set; } = [];

    public List<BaselineDiffSummary> Diffs { get; set; } = [];

    public Dictionary<string, string> Artifacts { get; set; } = new(StringComparer.Ordinal);
}

public sealed class BaselineDiffSummary
{
    public string Category { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Severity { get; set; } = "warning";

    public string? Expected { get; set; }

    public string? Actual { get; set; }
}
