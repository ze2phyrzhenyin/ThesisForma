namespace ThesisDocx.Core.Templates.Gate;

public sealed class TemplateGateOptions
{
    public string TemplatePath { get; set; } = string.Empty;

    public string DocumentPath { get; set; } = string.Empty;

    public string? OutputDirectory { get; set; }

    public double CoverageThreshold { get; set; } = 0.75;

    public List<string> ForbiddenAssetExtensions { get; set; } = [".exe", ".dll", ".dylib", ".so", ".sh", ".bat", ".cmd"];
}
