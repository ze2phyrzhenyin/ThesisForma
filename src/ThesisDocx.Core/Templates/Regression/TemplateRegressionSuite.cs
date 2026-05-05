namespace ThesisDocx.Core.Templates.Regression;

public sealed class TemplateRegressionSuite
{
    public string SuiteSchemaVersion { get; set; } = "1.0.0";

    public string Name { get; set; } = string.Empty;

    public List<TemplateRegressionCase> Cases { get; set; } = [];
}
