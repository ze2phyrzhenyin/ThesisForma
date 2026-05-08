using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Ci;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Validation;
using ThesisDocx.Core.Validation.FormatRuleCoverage;
using ThesisDocx.Core.Versioning;

namespace ThesisDocx.Core.Services;

public sealed class ThesisValidateService
{
    public ValidateInputResult ValidateInput(ValidateInputRequest request)
    {
        if (request.Document is null || request.Format is null)
        {
            return ValidateInputResult.Failure("service.input.missing", "Document and format are required for validation.");
        }

        var validation = new ThesisInputValidationResult();
        if (!string.IsNullOrWhiteSpace(request.DocumentPath) && !string.IsNullOrWhiteSpace(request.DocumentSchemaPath))
        {
            Merge(validation, new ThesisSchemaValidator().ValidateDocumentFile(request.DocumentPath, request.DocumentSchemaPath));
        }

        if (!string.IsNullOrWhiteSpace(request.FormatPath) && !string.IsNullOrWhiteSpace(request.FormatSchemaPath))
        {
            Merge(validation, new ThesisSchemaValidator().ValidateFormatFile(request.FormatPath, request.FormatSchemaPath));
        }

        Merge(validation, new ThesisInputValidator().Validate(request.Document, request.Format, request.BaseDirectory));
        return new ValidateInputResult
        {
            Success = validation.IsValid,
            IsValid = validation.IsValid,
            Diagnostics = validation.Diagnostics,
            ErrorCount = validation.Errors.Count,
            WarningCount = validation.Warnings.Count,
            VersionReport = validation.VersionReport
        };
    }

    private static void Merge(ThesisInputValidationResult target, ThesisInputValidationResult source)
    {
        target.Errors.AddRange(source.Errors);
        target.Warnings.AddRange(source.Warnings);
        if (source.VersionReport.Checks.Count > 0)
        {
            target.VersionReport = source.VersionReport;
        }
    }

    public ValidateDocxResult ValidateDocx(ValidateDocxRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DocxPath))
        {
            return ValidateDocxResult.Failure("service.docx.missing", "DOCX path is required for validation.");
        }

        try
        {
            OpenXmlValidationResult validation;
            SchemaVersionReport versionReport;
            if (!string.IsNullOrWhiteSpace(request.TemplatePath))
            {
                var resolution = new TemplateResolver().Resolve(request.TemplatePath);
                validation = new FormatConformanceValidator().Validate(request.DocxPath, request.TemplatePath);
                versionReport = SchemaVersionReport.ForTemplate(resolution.Template?.TemplateSchemaVersion, resolution.FormatSpec?.SchemaVersion);
            }
            else if (request.Format is not null)
            {
                validation = new FormatConformanceValidator().Validate(request.DocxPath, request.Format);
                versionReport = new SchemaVersionReport
                {
                    Checks = [new SchemaVersionSupport().CheckThesisFormatSpec(request.Format.SchemaVersion)]
                };
            }
            else
            {
                return ValidateDocxResult.Failure("service.format.missing", "Format spec or template path is required for DOCX validation.");
            }

            return new ValidateDocxResult
            {
                Success = validation.IsValid,
                IsValid = validation.IsValid,
                Diagnostics = validation.Diagnostics,
                ErrorCount = validation.Errors.Count,
                WarningCount = validation.Warnings.Count,
                CheckedRules = validation.CheckedRules.ToList(),
                Validation = validation,
                VersionReport = versionReport
            };
        }
        catch (Exception ex)
        {
            return ValidateDocxResult.Failure("service.validate.failed", "DOCX validation failed.", ex.Message);
        }
    }
}

public sealed class ThesisRenderService
{
    private readonly ThesisValidateService _validate = new();

    public RenderResult Render(RenderRequest request)
    {
        if (request.Document is null || request.Format is null)
        {
            return RenderResult.Failure("service.input.missing", "Document and format are required for rendering.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return RenderResult.Failure("service.output.missing", "Output path is required for rendering.");
        }

        if (request.ValidateInput)
        {
            var validation = _validate.ValidateInput(new ValidateInputRequest
            {
                Document = request.Document,
                Format = request.Format,
                BaseDirectory = request.BaseDirectory,
                DocumentPath = request.DocumentPath,
                FormatPath = request.FormatPath,
                DocumentSchemaPath = request.DocumentSchemaPath,
                FormatSchemaPath = request.FormatSchemaPath
            });
            if (!validation.IsValid)
            {
                return new RenderResult
                {
                    Success = false,
                    Diagnostics = validation.Diagnostics,
                    ErrorCount = validation.ErrorCount,
                    WarningCount = validation.WarningCount,
                    VersionReport = validation.VersionReport
                };
            }
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputPath)) ?? Directory.GetCurrentDirectory());
            new DocxRenderer().Render(request.Document, request.Format, request.OutputPath, request.RenderContext);
            var openXml = new OpenXmlPackageValidator().Validate(request.OutputPath);
            return new RenderResult
            {
                Success = openXml.IsValid,
                Diagnostics = openXml.Diagnostics,
                ErrorCount = openXml.Errors.Count,
                WarningCount = openXml.Warnings.Count,
                Artifact = ArtifactMetadata.FromFile("docx", request.OutputPath),
                VersionReport = SchemaVersionReport.ForDocumentAndFormat(request.Document.SchemaVersion, request.Format.SchemaVersion)
            };
        }
        catch (Exception ex)
        {
            return RenderResult.Failure("service.render.failed", "DOCX render failed.", ex.Message);
        }
    }
}

public sealed class TemplateResolveService
{
    public TemplateResolveServiceResult Resolve(TemplateResolveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplatePath))
        {
            return TemplateResolveServiceResult.Failure("service.template.missing", "Template path is required.");
        }

        try
        {
            var resolution = new TemplateResolver().Resolve(request.TemplatePath, request.Document, request.Variables);
            return new TemplateResolveServiceResult
            {
                Success = resolution.IsValid,
                IsValid = resolution.IsValid,
                TemplateId = resolution.Template?.Id,
                FormatSpecName = resolution.FormatSpec?.Name,
                PageTemplateCount = resolution.PageTemplates.Count,
                AssetCount = resolution.Assets.Count,
                VariableCount = resolution.Variables.Count,
                Resolution = resolution,
                ErrorCount = resolution.Errors.Count,
                WarningCount = resolution.Warnings.Count,
                VersionReport = SchemaVersionReport.ForTemplate(resolution.Template?.TemplateSchemaVersion, resolution.FormatSpec?.SchemaVersion),
                Diagnostics = resolution.Errors.Select(error => ToDiagnostic(error, DiagnosticSeverity.Error))
                    .Concat(resolution.Warnings.Select(warning => ToDiagnostic(warning, DiagnosticSeverity.Warning)))
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            return TemplateResolveServiceResult.Failure("service.template.resolveFailed", "Template resolve failed.", ex.Message);
        }
    }

    private static UnifiedDiagnostic ToDiagnostic(TemplateIssue issue, string severity)
    {
        return new UnifiedDiagnostic
        {
            Code = UnifiedDiagnosticMapper.CanonicalCode(issue.Code),
            Severity = severity,
            Path = string.IsNullOrWhiteSpace(issue.Path) ? "$" : issue.Path,
            Message = issue.Message,
            FixHint = null,
            Category = DiagnosticCategory.Template,
            Source = "TemplateResolveService"
        };
    }
}

public sealed class TemplateWorkflowService
{
    public TemplateValidateResult Validate(TemplateValidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplatePath))
        {
            return TemplateValidateResult.Failure("service.template.missing", "Template path is required.");
        }

        try
        {
            var validation = new TemplateValidationService().Validate(request.TemplatePath, request.SchemaPath);
            return new TemplateValidateResult
            {
                Success = validation.IsValid,
                IsValid = validation.IsValid,
                ErrorCount = validation.Errors.Count,
                WarningCount = validation.Warnings.Count,
                Diagnostics = validation.Diagnostics,
                VersionReport = validation.VersionReport,
                Validation = validation
            };
        }
        catch (Exception ex)
        {
            return TemplateValidateResult.Failure("service.template.validateFailed", "Template validation failed.", ex.Message);
        }
    }

    public TemplateCoverageResult Coverage(TemplateCoverageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplatePath))
        {
            return TemplateCoverageResult.Failure("service.template.missing", "Template path is required.");
        }

        try
        {
            var coverage = new FormatRuleCoverageReporter().Build(request.TemplatePath);
            return new TemplateCoverageResult
            {
                Success = true,
                Coverage = coverage,
                TemplateId = coverage.TemplateId,
                RuleCount = coverage.Rules.Count
            };
        }
        catch (Exception ex)
        {
            return TemplateCoverageResult.Failure("service.template.coverageFailed", "Template coverage failed.", ex.Message);
        }
    }
}

public sealed class CiQualityReportService
{
    public CiQualityReportServiceResult Build(CiQualityReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OutputDirectory)
            || string.IsNullOrWhiteSpace(request.TemplatePath)
            || string.IsNullOrWhiteSpace(request.DocumentPath)
            || string.IsNullOrWhiteSpace(request.RequirementsPath)
            || string.IsNullOrWhiteSpace(request.SuitePath)
            || string.IsNullOrWhiteSpace(request.NegativeFixturesPath))
        {
            return CiQualityReportServiceResult.Failure("service.ci.request.invalid", "CI quality report requires template, document, requirements, suite, negative fixtures, and output directory.");
        }

        try
        {
            var report = new CiQualityReportBuilder().Build(new CiQualityReportOptions
            {
                TemplatePath = request.TemplatePath,
                DocumentPath = request.DocumentPath,
                RequirementsPath = request.RequirementsPath,
                SuitePath = request.SuitePath,
                NegativeFixturesPath = request.NegativeFixturesPath,
                OutputDirectory = request.OutputDirectory,
                Threshold = request.Threshold
            });
            return new CiQualityReportServiceResult
            {
                Success = report.Status != "fail",
                Report = report,
                ErrorCount = report.BlockingIssues.Count,
                WarningCount = report.Warnings.Count,
                Diagnostics = report.Diagnostics
            };
        }
        catch (Exception ex)
        {
            return CiQualityReportServiceResult.Failure("service.ci.qualityReportFailed", "CI quality report failed.", ex.Message);
        }
    }
}

public sealed class ValidateInputRequest
{
    public ThesisDocument? Document { get; set; }
    public ThesisFormatSpec? Format { get; set; }
    public string? BaseDirectory { get; set; }
    public string? DocumentPath { get; set; }
    public string? FormatPath { get; set; }
    public string? DocumentSchemaPath { get; set; }
    public string? FormatSchemaPath { get; set; }
}

public sealed class ValidateInputResult : ServiceResult
{
    public bool IsValid { get; set; }

    public static ValidateInputResult Failure(string code, string message, string? detail = null)
    {
        return new ValidateInputResult { Success = false, ErrorCount = 1, Diagnostics = [Diagnostic(code, message, detail)] };
    }
}

public sealed class ValidateDocxRequest
{
    public string DocxPath { get; set; } = string.Empty;
    public ThesisFormatSpec? Format { get; set; }
    public string? TemplatePath { get; set; }
}

public sealed class ValidateDocxResult : ServiceResult
{
    public bool IsValid { get; set; }
    public List<string> CheckedRules { get; set; } = [];
    public OpenXmlValidationResult? Validation { get; set; }

    public static ValidateDocxResult Failure(string code, string message, string? detail = null)
    {
        return new ValidateDocxResult { Success = false, ErrorCount = 1, Diagnostics = [Diagnostic(code, message, detail)] };
    }
}

public sealed class RenderRequest
{
    public ThesisDocument? Document { get; set; }
    public ThesisFormatSpec? Format { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string? BaseDirectory { get; set; }
    public string? DocumentPath { get; set; }
    public string? FormatPath { get; set; }
    public string? DocumentSchemaPath { get; set; }
    public string? FormatSchemaPath { get; set; }
    public bool ValidateInput { get; set; } = true;
    public DocxRenderContext? RenderContext { get; set; }
}

public sealed class RenderResult : ServiceResult
{
    public ArtifactMetadata? Artifact { get; set; }

    public static RenderResult Failure(string code, string message, string? detail = null)
    {
        return new RenderResult { Success = false, ErrorCount = 1, Diagnostics = [Diagnostic(code, message, detail)] };
    }
}

public sealed class TemplateResolveRequest
{
    public string TemplatePath { get; set; } = string.Empty;
    public ThesisDocument? Document { get; set; }
    public IReadOnlyDictionary<string, string>? Variables { get; set; }
}

public sealed class TemplateResolveServiceResult : ServiceResult
{
    public bool IsValid { get; set; }
    public string? TemplateId { get; set; }
    public string? FormatSpecName { get; set; }
    public int PageTemplateCount { get; set; }
    public int AssetCount { get; set; }
    public int VariableCount { get; set; }
    public TemplateResolutionResult? Resolution { get; set; }

    public static TemplateResolveServiceResult Failure(string code, string message, string? detail = null)
    {
        return new TemplateResolveServiceResult { Success = false, ErrorCount = 1, Diagnostics = [Diagnostic(code, message, detail)] };
    }
}

public sealed class TemplateValidateRequest
{
    public string TemplatePath { get; set; } = string.Empty;
    public string? SchemaPath { get; set; }
}

public sealed class TemplateValidateResult : ServiceResult
{
    public bool IsValid { get; set; }
    public ThesisInputValidationResult? Validation { get; set; }

    public static TemplateValidateResult Failure(string code, string message, string? detail = null)
    {
        return new TemplateValidateResult
        {
            Success = false,
            ErrorCount = 1,
            Diagnostics = [Diagnostic(code, message, detail, DiagnosticCategory.Template, "TemplateWorkflowService")]
        };
    }
}

public sealed class TemplateCoverageRequest
{
    public string TemplatePath { get; set; } = string.Empty;
}

public sealed class TemplateCoverageResult : ServiceResult
{
    public string TemplateId { get; set; } = string.Empty;
    public int RuleCount { get; set; }
    public FormatRuleCoverageMatrix? Coverage { get; set; }

    public static TemplateCoverageResult Failure(string code, string message, string? detail = null)
    {
        return new TemplateCoverageResult
        {
            Success = false,
            ErrorCount = 1,
            Diagnostics = [Diagnostic(code, message, detail, DiagnosticCategory.Template, "TemplateWorkflowService")]
        };
    }
}

public sealed class CiQualityReportRequest
{
    public string TemplatePath { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string RequirementsPath { get; set; } = string.Empty;
    public string SuitePath { get; set; } = string.Empty;
    public string NegativeFixturesPath { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public double Threshold { get; set; } = 0.85;
}

public sealed class CiQualityReportServiceResult : ServiceResult
{
    public CiQualityReport? Report { get; set; }

    public static CiQualityReportServiceResult Failure(string code, string message, string? detail = null)
    {
        return new CiQualityReportServiceResult
        {
            Success = false,
            ErrorCount = 1,
            Diagnostics = [Diagnostic(code, message, detail, DiagnosticCategory.Regression, "CiQualityReportService")]
        };
    }
}

public abstract class ServiceResult
{
    public bool Success { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<UnifiedDiagnostic> Diagnostics { get; set; } = [];
    public SchemaVersionReport VersionReport { get; set; } = SchemaVersionReport.Empty();

    protected static UnifiedDiagnostic Diagnostic(
        string code,
        string message,
        string? detail = null,
        string category = DiagnosticCategory.Rendering,
        string source = "ThesisWorkflowServices")
    {
        var diagnostic = new UnifiedDiagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Error,
            Path = "$",
            Message = message,
            FixHint = "Check the service request payload and retry.",
            Category = category,
            Source = source
        };
        if (!string.IsNullOrWhiteSpace(detail))
        {
            diagnostic.Details["detail"] = Truncate(detail);
        }

        return diagnostic;
    }

    private static string Truncate(string value)
    {
        return value.Length <= 240 ? value : value[..240] + "...";
    }
}

public sealed class ArtifactMetadata
{
    public string Kind { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long ByteSize { get; set; }

    public static ArtifactMetadata FromFile(string kind, string path)
    {
        return new ArtifactMetadata
        {
            Kind = kind,
            Path = System.IO.Path.GetFileName(path),
            ByteSize = new FileInfo(path).Length
        };
    }
}
