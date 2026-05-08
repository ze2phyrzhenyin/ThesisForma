namespace ThesisDocx.Core.Validation;

using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Versioning;

public sealed class ThesisInputValidationResult
{
    public string Source { get; set; } = "ThesisInputValidator";

    public bool IsValid => Errors.Count == 0;

    public List<ThesisInputValidationError> Errors { get; set; } = [];

    public List<ThesisInputValidationError> Warnings { get; set; } = [];

    public SchemaVersionReport VersionReport { get; set; } = SchemaVersionReport.Empty();

    public List<UnifiedDiagnostic> Diagnostics => Errors
        .Select(error => UnifiedDiagnosticMapper.FromInputError(error, DiagnosticSeverity.Error, Source))
        .Concat(Warnings.Select(warning => UnifiedDiagnosticMapper.FromInputError(warning, DiagnosticSeverity.Warning, Source)))
        .ToList();

    public void Add(string code, string path, string message)
    {
        Errors.Add(new ThesisInputValidationError
        {
            Code = code,
            Path = path,
            Message = message
        });
    }

    public void AddWarning(string code, string path, string message)
    {
        Warnings.Add(new ThesisInputValidationError
        {
            Code = code,
            Path = path,
            Message = message
        });
    }
}
