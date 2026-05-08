using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Validation;

namespace ThesisDocx.Core.Services;

public sealed class ThesisValidateService
{
    public ValidateInputResult ValidateInput(ValidateInputRequest request)
    {
        if (request.Document is null || request.Format is null)
        {
            return ValidateInputResult.Failure("service.input.missing", "Document and format are required for validation.");
        }

        var validation = new ThesisInputValidator().Validate(request.Document, request.Format, request.BaseDirectory);
        return new ValidateInputResult
        {
            Success = validation.IsValid,
            IsValid = validation.IsValid,
            Diagnostics = validation.Diagnostics,
            ErrorCount = validation.Errors.Count,
            WarningCount = validation.Warnings.Count
        };
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
                BaseDirectory = request.BaseDirectory
            });
            if (!validation.IsValid)
            {
                return new RenderResult
                {
                    Success = false,
                    Diagnostics = validation.Diagnostics,
                    ErrorCount = validation.ErrorCount,
                    WarningCount = validation.WarningCount
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
                Artifact = ArtifactMetadata.FromFile("docx", request.OutputPath)
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

public sealed class ValidateInputRequest
{
    public ThesisDocument? Document { get; set; }
    public ThesisFormatSpec? Format { get; set; }
    public string? BaseDirectory { get; set; }
}

public sealed class ValidateInputResult : ServiceResult
{
    public bool IsValid { get; set; }

    public static ValidateInputResult Failure(string code, string message, string? detail = null)
    {
        return new ValidateInputResult { Success = false, ErrorCount = 1, Diagnostics = [Diagnostic(code, message, detail)] };
    }
}

public sealed class RenderRequest
{
    public ThesisDocument? Document { get; set; }
    public ThesisFormatSpec? Format { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string? BaseDirectory { get; set; }
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

public abstract class ServiceResult
{
    public bool Success { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<UnifiedDiagnostic> Diagnostics { get; set; } = [];

    protected static UnifiedDiagnostic Diagnostic(string code, string message, string? detail = null)
    {
        var diagnostic = new UnifiedDiagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Error,
            Path = "$",
            Message = message,
            FixHint = "Check the service request payload and retry.",
            Category = DiagnosticCategory.Rendering,
            Source = "ThesisWorkflowServices"
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
