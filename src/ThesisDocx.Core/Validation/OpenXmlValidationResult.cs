namespace ThesisDocx.Core.Validation;

using ThesisDocx.Core.Diagnostics;

public sealed class OpenXmlValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<ValidationIssue> Errors { get; set; } = [];

    public List<ValidationIssue> Warnings { get; set; } = [];

    public List<UnifiedDiagnostic> Diagnostics => Errors
        .Select(error => UnifiedDiagnosticMapper.FromValidationIssue(error, DiagnosticSeverity.Error, "OpenXmlValidator"))
        .Concat(Warnings.Select(warning => UnifiedDiagnosticMapper.FromValidationIssue(warning, DiagnosticSeverity.Warning, "OpenXmlValidator")))
        .ToList();

    public List<string> CheckedRules { get; set; } = [];
}
