namespace ThesisDocx.Core.Diagnostics;

public sealed class DiagnosticIssue
{
    public string Id { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Category { get; set; } = "unknown";

    public string Severity { get; set; } = "warning";

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? PartName { get; set; }

    public string? Path { get; set; }

    public string? SpecPath { get; set; }

    public string? TemplatePath { get; set; }

    public string? RequirementId { get; set; }

    public string? FixtureId { get; set; }

    public string? BaselineId { get; set; }

    public string? Expected { get; set; }

    public string? Actual { get; set; }

    public List<DiagnosticEvidence> Evidence { get; set; } = [];

    public List<FixHint> FixHints { get; set; } = [];

    public List<string> RelatedDocs { get; set; } = [];

    public List<string> RelatedFixtures { get; set; } = [];
}
