namespace ThesisDocx.Core.Models.Requirements;

public sealed class RequirementCaptureValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<RequirementCaptureValidationIssue> Errors { get; set; } = [];

    public List<RequirementCaptureValidationIssue> Warnings { get; set; } = [];
}

public sealed class RequirementCaptureValidationIssue
{
    public string Code { get; set; } = string.Empty;

    public string Path { get; set; } = "$";

    public string Message { get; set; } = string.Empty;

    public override string ToString() => $"{Code} at {Path}: {Message}";
}
