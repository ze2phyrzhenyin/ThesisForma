namespace ThesisDocx.Core.Diagnostics.FixHints;

public sealed class FixHintRule
{
    public string HintId { get; set; } = string.Empty;

    public FixHintRuleMatch Match { get; set; } = new();

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? SuggestedSpecPath { get; set; }

    public string? SuggestedTemplatePath { get; set; }

    public string SuggestedAction { get; set; } = string.Empty;

    public string? DocsRef { get; set; }

    public string? ExampleFixtureRef { get; set; }

    public string Confidence { get; set; } = "medium";
}

public sealed class FixHintRuleMatch
{
    public string? Source { get; set; }

    public string? Code { get; set; }

    public string? Category { get; set; }

    public string? PathPattern { get; set; }

    public string? Severity { get; set; }
}
