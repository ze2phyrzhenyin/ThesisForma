namespace ThesisDocx.Core.Validation;

public sealed class ValidationIssue
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? PartName { get; set; }

    public string? Path { get; set; }

    public string? Expected { get; set; }

    public string? Actual { get; set; }

    public override string ToString()
    {
        var location = string.IsNullOrWhiteSpace(PartName) && string.IsNullOrWhiteSpace(Path)
            ? string.Empty
            : $" [{PartName ?? "input"} {Path}]";
        return $"{Code}: {Message}{location}";
    }
}
