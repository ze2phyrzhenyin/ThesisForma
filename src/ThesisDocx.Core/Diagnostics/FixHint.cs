namespace ThesisDocx.Core.Diagnostics;

public sealed class FixHint
{
    public string HintId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? SuggestedSpecPath { get; set; }

    public string? SuggestedTemplatePath { get; set; }

    public string SuggestedAction { get; set; } = string.Empty;

    public string Confidence { get; set; } = "medium";

    public string? DocsRef { get; set; }

    public string? ExampleFixtureRef { get; set; }
}
