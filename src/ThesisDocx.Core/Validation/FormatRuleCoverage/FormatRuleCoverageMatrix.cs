namespace ThesisDocx.Core.Validation.FormatRuleCoverage;

public sealed class FormatRuleCoverageMatrix
{
    public string TemplateId { get; set; } = string.Empty;

    public List<FormatRuleCoverageRule> Rules { get; set; } = [];
}
