using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Ci;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Onboarding.Packaging;
using ThesisDocx.Core.Privacy;
using ThesisDocx.Core.Requirements;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Templates.Authoring;
using ThesisDocx.Core.Templates.Baselines;
using ThesisDocx.Core.Templates.Gate;
using ThesisDocx.Core.Templates.Regression;
using ThesisDocx.Core.Testing.NegativeFixtures;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Core.Validation.FormatRuleCoverage;
using ThesisDocx.Core.Versioning;
using System.Text.Json;

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
            target.VersionReport.MergeFrom(source.VersionReport);
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
                versionReport = SchemaVersionReport.ForFormat(request.Format.SchemaVersion);
            }
            else
            {
                return ValidateDocxResult.Failure("service.format.missing", "Format spec or template path is required for DOCX validation.");
            }

            var diagnostics = ServiceDiagnostics.Merge(validation.Diagnostics, versionReport.Diagnostics);
            var versionErrorCount = versionReport.Diagnostics.Count(diagnostic => UnifiedDiagnosticMapper.IsError(diagnostic.Severity));
            var versionWarningCount = versionReport.Diagnostics.Count(diagnostic => UnifiedDiagnosticMapper.IsWarning(diagnostic.Severity));
            var isValid = validation.IsValid && versionReport.IsValid;

            return new ValidateDocxResult
            {
                Success = isValid,
                IsValid = isValid,
                Diagnostics = diagnostics,
                ErrorCount = validation.Errors.Count + versionErrorCount,
                WarningCount = validation.Warnings.Count + versionWarningCount,
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
            var versionReport = SchemaVersionReport.ForTemplate(resolution.Template?.TemplateSchemaVersion, resolution.FormatSpec?.SchemaVersion);
            var diagnostics = ServiceDiagnostics.Merge(
                resolution.Errors.Select(error => ToDiagnostic(error, DiagnosticSeverity.Error))
                    .Concat(resolution.Warnings.Select(warning => ToDiagnostic(warning, DiagnosticSeverity.Warning))),
                versionReport.Diagnostics);
            var errorCount = diagnostics.Count(diagnostic => UnifiedDiagnosticMapper.IsError(diagnostic.Severity));
            var warningCount = diagnostics.Count(diagnostic => UnifiedDiagnosticMapper.IsWarning(diagnostic.Severity));
            var isValid = resolution.IsValid && versionReport.IsValid;

            return new TemplateResolveServiceResult
            {
                Success = isValid,
                IsValid = isValid,
                TemplateId = resolution.Template?.Id,
                FormatSpecName = resolution.FormatSpec?.Name,
                PageTemplateCount = resolution.PageTemplates.Count,
                AssetCount = resolution.Assets.Count,
                VariableCount = resolution.Variables.Count,
                Resolution = resolution,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                VersionReport = versionReport,
                Diagnostics = diagnostics
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
            FixHint = FixHintForTemplateIssue(issue.Code),
            Category = DiagnosticCategory.Template,
            Source = "TemplateResolveService"
        };
    }

    private static string? FixHintForTemplateIssue(string code)
    {
        return UnifiedDiagnosticMapper.CanonicalCode(code) switch
        {
            "template.variable.missing" => "Define the referenced variable or remove the reference.",
            "template.variable.requiredMissing" => "Provide the required variable with --var, a defaultValue, or a metadata sourcePath.",
            "template.variable.optionalMissing" => "Provide a defaultValue, metadata sourcePath, or --var override if this value should appear.",
            "template.variable.unknownSupplied" => "Remove the unknown --var entry or declare it in the template variables list.",
            "template.variable.defaultTypeMismatch" => "Make the variable defaultValue match its declared type.",
            "template.asset.missing" => "Define the referenced asset and keep it inside the template package.",
            "template.formatSpec.missing" => "Provide formatSpec or a relative formatSpecRef in the template package.",
            "template.schemaVersion.unsupported" => "Use a supported templateSchemaVersion or add an explicit migration.",
            _ => "Fix the template issue before resolving or rendering."
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

    public TemplateGateServiceResult Gate(TemplateGateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplatePath)
            || string.IsNullOrWhiteSpace(request.DocumentPath)
            || string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return TemplateGateServiceResult.Failure("service.template.gate.request.invalid", "Template gate requires template, document, and output directory.");
        }

        if (ValidateTemplateWorkflowPaths(request.TemplatePath, request.DocumentPath) is { } pathIssue)
        {
            return TemplateGateServiceResult.Failure(pathIssue.Code, pathIssue.Message);
        }

        try
        {
            var report = new TemplateGateService().Run(new TemplateGateOptions
            {
                TemplatePath = request.TemplatePath,
                DocumentPath = request.DocumentPath,
                OutputDirectory = request.OutputDirectory,
                CoverageThreshold = request.CoverageThreshold
            });
            return new TemplateGateServiceResult
            {
                Success = report.Status != TemplateGateStatus.Fail,
                Report = report,
                ErrorCount = report.Diagnostics.Count(issue => UnifiedDiagnosticMapper.IsError(issue.Severity)),
                WarningCount = report.Diagnostics.Count(issue => UnifiedDiagnosticMapper.IsWarning(issue.Severity)),
                Diagnostics = report.Diagnostics.Select(UnifiedDiagnosticMapper.FromDiagnosticIssue).ToList()
            };
        }
        catch (Exception ex)
        {
            return TemplateGateServiceResult.Failure("service.template.gateFailed", "Template gate failed.", ex.Message);
        }
    }

    public TemplateDiagnoseServiceResult Diagnose(TemplateDiagnoseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplatePath)
            || string.IsNullOrWhiteSpace(request.DocumentPath)
            || string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return TemplateDiagnoseServiceResult.Failure("service.template.diagnose.request.invalid", "Template diagnose requires template, document, and output directory.");
        }

        if (ValidateTemplateWorkflowPaths(request.TemplatePath, request.DocumentPath, request.RequirementsPath, request.SuitePath) is { } pathIssue)
        {
            return TemplateDiagnoseServiceResult.Failure(pathIssue.Code, pathIssue.Message);
        }

        try
        {
            Directory.CreateDirectory(request.OutputDirectory);
            var gate = new TemplateGateService().Run(new TemplateGateOptions
            {
                TemplatePath = request.TemplatePath,
                DocumentPath = request.DocumentPath,
                OutputDirectory = Path.Combine(request.OutputDirectory, "gate"),
                CoverageThreshold = request.CoverageThreshold
            });
            var regression = string.IsNullOrWhiteSpace(request.SuitePath)
                ? null
                : new TemplateRegressionRunner().Run(request.SuitePath, Path.Combine(request.OutputDirectory, "regression"));
            var baseline = string.IsNullOrWhiteSpace(request.SuitePath)
                ? null
                : new TemplateBaselineManager().CompareSuite(request.SuitePath, Path.Combine(request.OutputDirectory, "baseline"));
            RequirementMappingReport? requirements = null;
            if (!string.IsNullOrWhiteSpace(request.RequirementsPath))
            {
                requirements = new RequirementMappingReporter().Build(new RequirementCaptureLoader().Load(request.RequirementsPath), request.TemplatePath);
                var requirementsReportPath = Path.Combine(request.OutputDirectory, "requirements-report.json");
                File.WriteAllText(requirementsReportPath, JsonSerializer.Serialize(requirements, ThesisJson.Options));
                gate.Artifacts["requirementsReport"] = requirementsReportPath;
            }

            var report = new DiagnosticReportBuilder().Build(gate, regression, baseline, requirements, artifacts: gate.Artifacts);
            return new TemplateDiagnoseServiceResult
            {
                Success = report.BreakingCount == 0,
                Report = report,
                ErrorCount = report.BreakingCount,
                WarningCount = report.WarningCount,
                Diagnostics = report.Diagnostics
            };
        }
        catch (Exception ex)
        {
            return TemplateDiagnoseServiceResult.Failure("service.template.diagnoseFailed", "Template diagnose failed.", ex.Message);
        }
    }

    public TemplateAuthoringReportServiceResult AuthoringReport(TemplateAuthoringReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplatePath)
            || string.IsNullOrWhiteSpace(request.DocumentPath)
            || string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return TemplateAuthoringReportServiceResult.Failure("service.template.authoringReport.request.invalid", "Template authoring report requires template, document, and output directory.");
        }

        if (ValidateTemplateWorkflowPaths(request.TemplatePath, request.DocumentPath, request.RequirementsPath, request.SuitePath) is { } pathIssue)
        {
            return TemplateAuthoringReportServiceResult.Failure(pathIssue.Code, pathIssue.Message);
        }

        try
        {
            var report = new TemplateAuthoringReportBuilder().Build(new TemplateAuthoringReportOptions
            {
                TemplatePath = request.TemplatePath,
                DocumentPath = request.DocumentPath,
                RequirementsPath = request.RequirementsPath,
                SuitePath = request.SuitePath,
                OutputDirectory = request.OutputDirectory,
                CoverageThreshold = request.CoverageThreshold
            });
            return new TemplateAuthoringReportServiceResult
            {
                Success = report.PublishReadiness != "notReady",
                Report = report,
                ErrorCount = report.BlockingIssues.Count,
                WarningCount = report.Warnings.Count,
                Diagnostics = report.Diagnostics
            };
        }
        catch (Exception ex)
        {
            return TemplateAuthoringReportServiceResult.Failure("service.template.authoringReportFailed", "Template authoring report failed.", ex.Message);
        }
    }

    private static PathValidationIssue? ValidateTemplateWorkflowPaths(
        string templatePath,
        string documentPath,
        string? requirementsPath = null,
        string? suitePath = null)
    {
        if (!Directory.Exists(templatePath))
        {
            return new PathValidationIssue("service.template.pathMissing", "Template path does not exist.");
        }

        if (!File.Exists(documentPath))
        {
            return new PathValidationIssue("service.document.pathMissing", "Document path does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(requirementsPath) && !File.Exists(requirementsPath))
        {
            return new PathValidationIssue("service.requirements.pathMissing", "Requirements path does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(suitePath) && !File.Exists(suitePath))
        {
            return new PathValidationIssue("service.regressionSuite.pathMissing", "Template regression suite path does not exist.");
        }

        return null;
    }

    private sealed record PathValidationIssue(string Code, string Message);
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

public sealed class RequirementsWorkflowService
{
    public RequirementsValidateServiceResult Validate(RequirementsValidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequirementsPath))
        {
            return RequirementsValidateServiceResult.Failure("service.requirements.missing", "RequirementCapture path is required.");
        }

        try
        {
            var result = new RequirementCaptureValidationResult();
            if (!string.IsNullOrWhiteSpace(request.SchemaPath))
            {
                var schema = new ThesisSchemaValidator().ValidateRequirementCaptureFile(request.RequirementsPath, request.SchemaPath);
                result.Errors.AddRange(schema.Errors.Select(error => new RequirementCaptureValidationIssue
                {
                    Code = error.Code,
                    Path = error.Path,
                    Message = error.Message
                }));
                result.Warnings.AddRange(schema.Warnings.Select(warning => new RequirementCaptureValidationIssue
                {
                    Code = warning.Code,
                    Path = warning.Path,
                    Message = warning.Message
                }));
            }

            var semantic = new RequirementCaptureValidator().Validate(new RequirementCaptureLoader().Load(request.RequirementsPath));
            result.Errors.AddRange(semantic.Errors);
            result.Warnings.AddRange(semantic.Warnings);
            return new RequirementsValidateServiceResult
            {
                Success = result.IsValid,
                IsValid = result.IsValid,
                Validation = result,
                ErrorCount = result.Errors.Count,
                WarningCount = result.Warnings.Count,
                Diagnostics = result.Diagnostics
            };
        }
        catch (Exception ex)
        {
            return RequirementsValidateServiceResult.Failure("service.requirements.validateFailed", "RequirementCapture validation failed.", ex.Message);
        }
    }

    public RequirementsReportServiceResult Report(RequirementsReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequirementsPath))
        {
            return RequirementsReportServiceResult.Failure("service.requirements.missing", "RequirementCapture path is required.");
        }

        try
        {
            var report = new RequirementMappingReporter().Build(new RequirementCaptureLoader().Load(request.RequirementsPath), request.TemplatePath);
            return new RequirementsReportServiceResult
            {
                Success = report.IsValid,
                IsValid = report.IsValid,
                Report = report,
                ErrorCount = report.Errors.Count,
                WarningCount = report.Warnings.Count,
                Diagnostics = report.Errors.Select(error => UnifiedDiagnosticMapper.FromRequirementIssue(error, DiagnosticSeverity.Error, "RequirementMappingReporter"))
                    .Concat(report.Warnings.Select(warning => UnifiedDiagnosticMapper.FromRequirementIssue(warning, DiagnosticSeverity.Warning, "RequirementMappingReporter")))
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            return RequirementsReportServiceResult.Failure("service.requirements.reportFailed", "Requirement mapping report failed.", ex.Message);
        }
    }
}

public sealed class NegativeFixturesWorkflowService
{
    public NegativeFixturesRunServiceResult Run(NegativeFixturesRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ManifestPath))
        {
            return NegativeFixturesRunServiceResult.Failure("service.negativeFixtures.manifestMissing", "Negative fixture manifest path is required.");
        }

        try
        {
            var result = new NegativeFixtureRunner().Run(request.ManifestPath);
            return new NegativeFixturesRunServiceResult
            {
                Success = result.Passed,
                Passed = result.Passed,
                Result = result,
                ErrorCount = result.Cases.Count(c => !c.Passed),
                Diagnostics = result.Cases
                    .Where(c => !c.Passed)
                    .Select(c => new UnifiedDiagnostic
                    {
                        Code = "negativeFixture.case.failed",
                        Severity = DiagnosticSeverity.Error,
                        Path = c.Id,
                        Message = string.Join("; ", c.Errors),
                        FixHint = "Update the fixture or expected diagnostic contract so the negative case observes the intended failure.",
                        Category = DiagnosticCategory.Regression,
                        Source = "NegativeFixturesWorkflowService"
                    })
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            return NegativeFixturesRunServiceResult.Failure("service.negativeFixtures.runFailed", "Negative fixtures run failed.", ex.Message);
        }
    }
}

public sealed class PrivacyWorkflowService
{
    public PrivacyScanServiceResult Scan(PrivacyScanRequest request)
    {
        if (request.Options is null || string.IsNullOrWhiteSpace(request.Options.Path))
        {
            return PrivacyScanServiceResult.Failure("service.privacy.pathMissing", "Privacy scan path is required.");
        }

        try
        {
            var scan = new PrivacyGuard().Scan(request.Options);
            return new PrivacyScanServiceResult
            {
                Success = scan.IsValid,
                IsValid = scan.IsValid,
                Scan = scan,
                ErrorCount = scan.BreakingCount,
                WarningCount = scan.WarningCount,
                Diagnostics = scan.Diagnostics
            };
        }
        catch (Exception ex)
        {
            return PrivacyScanServiceResult.Failure("service.privacy.scanFailed", "Privacy scan failed.", ex.Message);
        }
    }
}

public sealed class OnboardingPackageWorkflowService
{
    public OnboardingPackageValidateServiceResult Validate(OnboardingPackageValidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PackagePath))
        {
            return OnboardingPackageValidateServiceResult.Failure("service.onboarding.packageMissing", "Pilot package path is required.");
        }

        try
        {
            var validation = new TemplatePilotPackageValidator().Validate(request.PackagePath);
            return new OnboardingPackageValidateServiceResult
            {
                Success = validation.IsValid,
                IsValid = validation.IsValid,
                Validation = validation,
                ErrorCount = validation.Diagnostics.Count(diagnostic => UnifiedDiagnosticMapper.IsError(diagnostic.Severity)),
                WarningCount = validation.Diagnostics.Count(diagnostic => UnifiedDiagnosticMapper.IsWarning(diagnostic.Severity)),
                Diagnostics = validation.Diagnostics
            };
        }
        catch (Exception ex)
        {
            return OnboardingPackageValidateServiceResult.Failure("service.onboarding.packageValidateFailed", "Pilot package validation failed.", ex.Message);
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

internal static class ServiceDiagnostics
{
    public static List<UnifiedDiagnostic> Merge(IEnumerable<UnifiedDiagnostic> primary, IEnumerable<UnifiedDiagnostic> secondary)
    {
        var diagnostics = primary.ToList();
        foreach (var diagnostic in secondary)
        {
            if (!diagnostics.Any(existing => existing.Code == diagnostic.Code && existing.Path == diagnostic.Path))
            {
                diagnostics.Add(diagnostic);
            }
        }

        return diagnostics;
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
