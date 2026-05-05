namespace ThesisDocx.Core.Validation;

public sealed class OpenXmlValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<ValidationIssue> Errors { get; set; } = [];

    public List<ValidationIssue> Warnings { get; set; } = [];

    public List<string> CheckedRules { get; set; } = [];
}
