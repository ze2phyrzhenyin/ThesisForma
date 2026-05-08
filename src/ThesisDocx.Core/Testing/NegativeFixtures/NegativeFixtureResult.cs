using ThesisDocx.Core.Diagnostics;

namespace ThesisDocx.Core.Testing.NegativeFixtures;

public sealed class NegativeFixtureRunResult
{
    public string ReportVersion { get; set; } = "1.0.0";

    public string SuiteId { get; set; } = string.Empty;

    public bool Passed => Cases.All(fixture => fixture.Passed);

    public List<NegativeFixtureCaseResult> Cases { get; set; } = [];

    public List<UnifiedDiagnostic> Diagnostics => Cases
        .SelectMany(fixture => fixture.Diagnostics)
        .ToList();
}

public sealed class NegativeFixtureCaseResult
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool Passed { get; set; }

    public int ExpectedExitCode { get; set; }

    public int ActualExitCode { get; set; }

    public List<string> ExpectedCodes { get; set; } = [];

    public List<string> ActualCodes { get; set; } = [];

    public string ExpectedSeverity { get; set; } = string.Empty;

    public List<string> ActualSeverities { get; set; } = [];

    public List<string> ExpectedFixHintIds { get; set; } = [];

    public List<string> ActualFixHintIds { get; set; } = [];

    public List<DiagnosticIssue> Issues { get; set; } = [];

    public List<UnifiedDiagnostic> Diagnostics => Issues
        .Select(UnifiedDiagnosticMapper.FromDiagnosticIssue)
        .ToList();

    public List<string> Errors { get; set; } = [];
}
