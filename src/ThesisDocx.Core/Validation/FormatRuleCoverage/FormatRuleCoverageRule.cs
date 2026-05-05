namespace ThesisDocx.Core.Validation.FormatRuleCoverage;

public sealed class FormatRuleCoverageRule
{
    public string RuleId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string SpecPath { get; set; } = string.Empty;

    public bool SchemaCovered { get; set; }

    public bool RendererCovered { get; set; }

    public bool ValidatorCovered { get; set; }

    public bool TestCovered { get; set; }

    public bool InspectCovered { get; set; }

    public string Status { get; set; } = "planned";

    public string Notes { get; set; } = string.Empty;
}
