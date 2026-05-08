namespace ThesisDocx.Core.Validation.FormatRuleCoverage;

public sealed class FormatRuleCoverageMatrix
{
    public string ReportVersion { get; set; } = "1.0.0";

    public string TemplateId { get; set; } = string.Empty;

    public List<FormatRuleCoverageRule> Rules { get; set; } = [];
}
