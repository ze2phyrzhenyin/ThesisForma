namespace ThesisDocx.Core.Diagnostics;

public sealed class DiagnosticIssue
{
    public string Id { get; set; } = string.Empty;

    public string Code
    {
        get => Id;
        set => Id = value;
    }

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

    public string? FixHint
    {
        get => FixHints.FirstOrDefault()?.SuggestedAction;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            FixHints =
            [
                new FixHint
                {
                    HintId = $"{Id}.inlineFixHint",
                    Title = "Fix hint",
                    Description = value!,
                    SuggestedAction = value!
                }
            ];
        }
    }

    public List<string> RelatedDocs { get; set; } = [];

    public List<string> RelatedFixtures { get; set; } = [];

    public List<string> RelatedPaths { get; set; } = [];

    public Dictionary<string, string> Details { get; set; } = new(StringComparer.Ordinal);

    public string? DocumentationRef { get; set; }
}
