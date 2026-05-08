namespace ThesisDocx.Core.Models.Requirements;

using ThesisDocx.Core.Diagnostics;

public sealed class RequirementCaptureValidationResult
{
    public string ReportVersion { get; set; } = "1.0.0";

    public bool IsValid => Errors.Count == 0;

    public List<RequirementCaptureValidationIssue> Errors { get; set; } = [];

    public List<RequirementCaptureValidationIssue> Warnings { get; set; } = [];

    public List<UnifiedDiagnostic> Diagnostics => Errors
        .Select(error => UnifiedDiagnosticMapper.FromRequirementIssue(error, DiagnosticSeverity.Error, "RequirementCaptureValidator"))
        .Concat(Warnings.Select(warning => UnifiedDiagnosticMapper.FromRequirementIssue(warning, DiagnosticSeverity.Warning, "RequirementCaptureValidator")))
        .ToList();
}

public sealed class RequirementCaptureValidationIssue
{
    public string Code { get; set; } = string.Empty;

    public string Path { get; set; } = "$";

    public string Message { get; set; } = string.Empty;

    public override string ToString() => $"{Code} at {Path}: {Message}";
}
