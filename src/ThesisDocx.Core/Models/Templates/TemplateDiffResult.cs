namespace ThesisDocx.Core.Models.Templates;

public sealed class TemplateDiffResult
{
    public string BaseTemplateId { get; set; } = string.Empty;

    public string TargetTemplateId { get; set; } = string.Empty;

    public List<TemplateDiffChange> Changes { get; set; } = [];
}

public sealed class TemplateDiffChange
{
    public string Path { get; set; } = string.Empty;

    public TemplateDiffChangeType ChangeType { get; set; }

    public string? BaseValue { get; set; }

    public string? TargetValue { get; set; }

    public TemplateDiffCategory Category { get; set; } = TemplateDiffCategory.Unknown;

    public TemplateDiffSeverity Severity { get; set; } = TemplateDiffSeverity.Info;
}

public enum TemplateDiffChangeType
{
    Added,
    Removed,
    Modified
}

public enum TemplateDiffCategory
{
    PageSetup,
    Font,
    Paragraph,
    Heading,
    HeaderFooter,
    Toc,
    Table,
    Figure,
    Equation,
    Bibliography,
    PageTemplate,
    Variable,
    Asset,
    Unknown
}

public enum TemplateDiffSeverity
{
    Info,
    Warning,
    Breaking
}
