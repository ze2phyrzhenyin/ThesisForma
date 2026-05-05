namespace ThesisDocx.Core.Models.Requirements;

public sealed class RequirementSource
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public RequirementSourceType Type { get; set; } = RequirementSourceType.ManualNote;

    public string Path { get; set; } = string.Empty;

    public string? CapturedAt { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public enum RequirementSourceType
{
    Pdf,
    Docx,
    Webpage,
    ManualNote,
    Other
}
