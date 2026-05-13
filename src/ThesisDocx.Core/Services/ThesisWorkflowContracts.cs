using ThesisDocx.Core.Ci;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Onboarding.Packaging;
using ThesisDocx.Core.Privacy;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Requirements;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Templates.Authoring;
using ThesisDocx.Core.Templates.Gate;
using ThesisDocx.Core.Testing.NegativeFixtures;
using ThesisDocx.Core.Validation;
using ThesisDocx.Core.Validation.FormatRuleCoverage;
using ThesisDocx.Core.Versioning;

namespace ThesisDocx.Core.Services;

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
        return new TemplateResolveServiceResult
        {
            Success = false,
            ErrorCount = 1,
            Diagnostics = [Diagnostic(code, message, detail, DiagnosticCategory.Template, "TemplateResolveService")]
        };
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

public sealed class TemplateGateRequest
{
    public string TemplatePath { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public double CoverageThreshold { get; set; } = 0.75;
}

public sealed class TemplateGateServiceResult : ServiceResult
{
    public TemplateGateReport? Report { get; set; }

    public static TemplateGateServiceResult Failure(string code, string message, string? detail = null)
    {
        return new TemplateGateServiceResult
        {
            Success = false,
            ErrorCount = 1,
            Diagnostics = [Diagnostic(code, message, detail, DiagnosticCategory.Template, "TemplateWorkflowService")]
        };
    }
}

public sealed class TemplateDiagnoseRequest
{
    public string TemplatePath { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string? RequirementsPath { get; set; }
    public string? SuitePath { get; set; }
    public string OutputDirectory { get; set; } = string.Empty;
    public double CoverageThreshold { get; set; } = 0.75;
}

public sealed class TemplateDiagnoseServiceResult : ServiceResult
{
    public DiagnosticReport? Report { get; set; }

    public static TemplateDiagnoseServiceResult Failure(string code, string message, string? detail = null)
    {
        return new TemplateDiagnoseServiceResult
        {
            Success = false,
            ErrorCount = 1,
            Diagnostics = [Diagnostic(code, message, detail, DiagnosticCategory.Template, "TemplateWorkflowService")]
        };
    }
}

public sealed class TemplateAuthoringReportRequest
{
    public string TemplatePath { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string? RequirementsPath { get; set; }
    public string? SuitePath { get; set; }
    public string OutputDirectory { get; set; } = string.Empty;
    public double CoverageThreshold { get; set; } = 0.85;
}

public sealed class TemplateAuthoringReportServiceResult : ServiceResult
{
    public TemplateAuthoringReport? Report { get; set; }

    public static TemplateAuthoringReportServiceResult Failure(string code, string message, string? detail = null)
    {
        return new TemplateAuthoringReportServiceResult
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

public sealed class RequirementsValidateRequest
{
    public string RequirementsPath { get; set; } = string.Empty;
    public string? SchemaPath { get; set; }
}

public sealed class RequirementsValidateServiceResult : ServiceResult
{
    public bool IsValid { get; set; }
    public RequirementCaptureValidationResult? Validation { get; set; }

    public static RequirementsValidateServiceResult Failure(string code, string message, string? detail = null)
    {
        return new RequirementsValidateServiceResult
        {
            Success = false,
            ErrorCount = 1,
            Diagnostics = [Diagnostic(code, message, detail, DiagnosticCategory.Requirement, "RequirementsWorkflowService")]
        };
    }
}

public sealed class RequirementsReportRequest
{
    public string RequirementsPath { get; set; } = string.Empty;
    public string? TemplatePath { get; set; }
}

public sealed class RequirementsReportServiceResult : ServiceResult
{
    public bool IsValid { get; set; }
    public RequirementMappingReport? Report { get; set; }

    public static RequirementsReportServiceResult Failure(string code, string message, string? detail = null)
    {
        return new RequirementsReportServiceResult
        {
            Success = false,
            ErrorCount = 1,
            Diagnostics = [Diagnostic(code, message, detail, DiagnosticCategory.Requirement, "RequirementsWorkflowService")]
        };
    }
}

public sealed class NegativeFixturesRunRequest
{
    public string ManifestPath { get; set; } = string.Empty;
}

public sealed class NegativeFixturesRunServiceResult : ServiceResult
{
    public bool Passed { get; set; }
    public NegativeFixtureRunResult? Result { get; set; }

    public static NegativeFixturesRunServiceResult Failure(string code, string message, string? detail = null)
    {
        return new NegativeFixturesRunServiceResult
        {
            Success = false,
            ErrorCount = 1,
            Diagnostics = [Diagnostic(code, message, detail, DiagnosticCategory.Regression, "NegativeFixturesWorkflowService")]
        };
    }
}

public sealed class PrivacyScanRequest
{
    public PrivacyGuardOptions? Options { get; set; }
}

public sealed class PrivacyScanServiceResult : ServiceResult
{
    public bool IsValid { get; set; }
    public PrivacyGuardResult? Scan { get; set; }

    public static PrivacyScanServiceResult Failure(string code, string message, string? detail = null)
    {
        return new PrivacyScanServiceResult
        {
            Success = false,
            ErrorCount = 1,
            Diagnostics = [Diagnostic(code, message, detail, DiagnosticCategory.Privacy, "PrivacyWorkflowService")]
        };
    }
}

public sealed class OnboardingPackageValidateRequest
{
    public string PackagePath { get; set; } = string.Empty;
}

public sealed class OnboardingPackageValidateServiceResult : ServiceResult
{
    public bool IsValid { get; set; }
    public TemplatePilotPackageValidationResult? Validation { get; set; }

    public static OnboardingPackageValidateServiceResult Failure(string code, string message, string? detail = null)
    {
        return new OnboardingPackageValidateServiceResult
        {
            Success = false,
            ErrorCount = 1,
            Diagnostics = [Diagnostic(code, message, detail, DiagnosticCategory.Privacy, "OnboardingPackageWorkflowService")]
        };
    }
}

public abstract class ServiceResult
{
    public string ReportVersion { get; set; } = "1.0.0";
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
