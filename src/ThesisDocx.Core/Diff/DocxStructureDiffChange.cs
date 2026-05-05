namespace ThesisDocx.Core.Diff;

public sealed class DocxStructureDiffChange
{
    public string Path { get; set; } = string.Empty;

    public string? PartName { get; set; }

    public DocxStructureDiffChangeType ChangeType { get; set; }

    public string Category { get; set; } = "unknown";

    public string Message { get; set; } = string.Empty;

    public DocxDiffSeverity Severity { get; set; } = DocxDiffSeverity.Warning;

    public string? BaseValue { get; set; }

    public string? TargetValue { get; set; }
}

public enum DocxStructureDiffChangeType
{
    Added,
    Removed,
    Modified
}

public enum DocxDiffSeverity
{
    Info,
    Warning,
    Breaking
}
