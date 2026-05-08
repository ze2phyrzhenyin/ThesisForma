using System.Text.Json;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Onboarding.Packaging;
using ThesisDocx.Core.Privacy;
using ThesisDocx.Core.Services;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class CoreServiceFacadeTests
{
    [Fact]
    public void ValidateService_ShouldReturnSuccessForValidInput()
    {
        var (document, format, baseDirectory) = LoadSimple();

        var result = new ThesisValidateService().ValidateInput(new ValidateInputRequest
        {
            Document = document,
            Format = format,
            BaseDirectory = baseDirectory
        });

        Assert.True(result.Success);
        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == "error");
        Assert.Contains(result.VersionReport.Checks, check => check.Kind == "thesisDocument");
        Assert.Contains(result.VersionReport.Checks, check => check.Kind == "thesisFormatSpec");
        Assert.DoesNotContain(result.VersionReport.Checks, check => check.Direction == "unsupported");
    }

    [Fact]
    public void ValidateService_ShouldReturnDiagnosticsForBadInput()
    {
        var (_, format, baseDirectory) = LoadSimple();
        var document = new ThesisDocument();

        var result = new ThesisValidateService().ValidateInput(new ValidateInputRequest
        {
            Document = document,
            Format = format,
            BaseDirectory = baseDirectory
        });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "document.sections.empty");
    }

    [Fact]
    public void RenderService_ShouldReturnArtifactMetadataWithoutAbsolutePath()
    {
        var (document, format, baseDirectory) = LoadSimple();
        var output = Path.Combine(NewTempDirectory(), "service-render.docx");

        var result = new ThesisRenderService().Render(new RenderRequest
        {
            Document = document,
            Format = format,
            BaseDirectory = baseDirectory,
            OutputPath = output
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Artifact);
        Assert.Equal("service-render.docx", result.Artifact!.Path);
        Assert.True(result.Artifact.ByteSize > 0);
        Assert.DoesNotContain(Path.GetTempPath(), JsonSerializer.Serialize(result, ThesisJson.Options));
    }

    [Fact]
    public void TemplateResolveService_ShouldReturnResolvedMetadata()
    {
        var (document, _, _) = LoadSimple();
        var result = new TemplateResolveService().Resolve(new TemplateResolveRequest
        {
            TemplatePath = TemplatePath(),
            Document = document,
            Variables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["schoolName"] = "Example University"
            }
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("example-university-engineering", result.TemplateId);
        Assert.True(result.PageTemplateCount > 0);
        Assert.NotNull(result.Resolution?.FormatSpec);
        Assert.Contains(result.VersionReport.Checks, check => check.Kind == "templatePackage");
        Assert.DoesNotContain(result.VersionReport.Checks, check => check.Direction == "unsupported");
    }

    [Fact]
    public void TemplateWorkflowService_ShouldValidateTemplateWithVersionReport()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();

        var result = new TemplateWorkflowService().Validate(new TemplateValidateRequest
        {
            TemplatePath = TemplatePath(),
            SchemaPath = Path.Combine(root, "schemas", "template-package.schema.json")
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Validation);
        Assert.True(result.IsValid);
        Assert.Contains(result.VersionReport.Checks, check => check.Kind == "templatePackage");
        Assert.Contains(result.VersionReport.Checks, check => check.Kind == "thesisFormatSpec");
    }

    [Fact]
    public void TemplateWorkflowService_ShouldReturnCoverageMetadata()
    {
        var result = new TemplateWorkflowService().Coverage(new TemplateCoverageRequest
        {
            TemplatePath = TemplatePath()
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("example-university-engineering", result.TemplateId);
        Assert.True(result.RuleCount > 0);
        Assert.NotNull(result.Coverage);
    }

    [Fact]
    public void TemplateWorkflowService_ShouldRunTemplateGate()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();

        var result = new TemplateWorkflowService().Gate(new TemplateGateRequest
        {
            TemplatePath = TemplatePath(),
            DocumentPath = Path.Combine(root, "examples", "full-thesis", "document.json"),
            OutputDirectory = Path.Combine(NewTempDirectory(), "gate"),
            CoverageThreshold = 0.85
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Report);
        Assert.Equal("example-university-engineering", result.Report!.TemplateId);
    }

    [Fact]
    public void TemplateWorkflowService_ShouldBuildDiagnosticReport()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();

        var result = new TemplateWorkflowService().Diagnose(new TemplateDiagnoseRequest
        {
            TemplatePath = TemplatePath(),
            DocumentPath = Path.Combine(root, "examples", "full-thesis", "document.json"),
            RequirementsPath = Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"),
            SuitePath = Path.Combine(root, "examples", "template-regression", "template-regression-suite.json"),
            OutputDirectory = Path.Combine(NewTempDirectory(), "diagnose")
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Report);
        Assert.Equal("pass", result.Report!.Status);
    }

    [Fact]
    public void TemplateWorkflowService_ShouldBuildAuthoringReport()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();

        var result = new TemplateWorkflowService().AuthoringReport(new TemplateAuthoringReportRequest
        {
            TemplatePath = TemplatePath(),
            DocumentPath = Path.Combine(root, "examples", "full-thesis", "document.json"),
            RequirementsPath = Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"),
            SuitePath = Path.Combine(root, "examples", "template-regression", "template-regression-suite.json"),
            OutputDirectory = Path.Combine(NewTempDirectory(), "authoring"),
            CoverageThreshold = 0.85
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Report);
        Assert.Equal("ready", result.Report!.PublishReadiness);
    }

    [Fact]
    public void TemplateWorkflowService_ShouldReturnGateRequestDiagnostic()
    {
        var result = new TemplateWorkflowService().Gate(new TemplateGateRequest
        {
            TemplatePath = TemplatePath(),
            OutputDirectory = NewTempDirectory()
        });

        Assert.False(result.Success);
        Assert.Null(result.Report);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "service.template.gate.request.invalid");
    }

    [Fact]
    public void TemplateWorkflowService_ShouldReturnDiagnoseRequestDiagnostic()
    {
        var result = new TemplateWorkflowService().Diagnose(new TemplateDiagnoseRequest
        {
            DocumentPath = "missing.json",
            OutputDirectory = NewTempDirectory()
        });

        Assert.False(result.Success);
        Assert.Null(result.Report);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "service.template.diagnose.request.invalid");
    }

    [Fact]
    public void TemplateWorkflowService_ShouldReturnAuthoringReportRequestDiagnostic()
    {
        var result = new TemplateWorkflowService().AuthoringReport(new TemplateAuthoringReportRequest
        {
            TemplatePath = TemplatePath(),
            DocumentPath = "missing.json"
        });

        Assert.False(result.Success);
        Assert.Null(result.Report);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "service.template.authoringReport.request.invalid");
    }

    [Fact]
    public void CiQualityReportService_ShouldReturnReportForPassingGate()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var outputDirectory = Path.Combine(NewTempDirectory(), "ci");

        var result = new CiQualityReportService().Build(new CiQualityReportRequest
        {
            TemplatePath = TemplatePath(),
            DocumentPath = Path.Combine(root, "examples", "full-thesis", "document.json"),
            RequirementsPath = Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"),
            SuitePath = Path.Combine(root, "examples", "template-regression", "template-regression-suite.json"),
            NegativeFixturesPath = Path.Combine(root, "examples", "negative-fixtures", "negative-fixture-manifest.json"),
            OutputDirectory = outputDirectory,
            Threshold = 0.85
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Report);
        Assert.Equal("pass", result.Report!.Status);
        Assert.Contains("gateReport", result.Report.Artifacts.Keys);
    }

    [Fact]
    public void RequirementsWorkflowService_ShouldValidateAndReport()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var requirementsPath = Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json");
        var service = new RequirementsWorkflowService();

        var validation = service.Validate(new RequirementsValidateRequest
        {
            RequirementsPath = requirementsPath,
            SchemaPath = Path.Combine(root, "schemas", "requirement-capture.schema.json")
        });
        var report = service.Report(new RequirementsReportRequest
        {
            RequirementsPath = requirementsPath,
            TemplatePath = TemplatePath()
        });

        Assert.True(validation.Success, string.Join(Environment.NewLine, validation.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(validation.Validation);
        Assert.True(report.Success, string.Join(Environment.NewLine, report.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(report.Report);
        Assert.True(report.Report!.TotalRequirements > 0);
    }

    [Fact]
    public void NegativeFixturesWorkflowService_ShouldRunManifest()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();

        var result = new NegativeFixturesWorkflowService().Run(new NegativeFixturesRunRequest
        {
            ManifestPath = Path.Combine(root, "examples", "negative-fixtures", "negative-fixture-manifest.json")
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Result);
        Assert.True(result.Result!.Cases.Count > 0);
    }

    [Fact]
    public void PrivacyWorkflowService_ShouldScanExamples()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();

        var result = new PrivacyWorkflowService().Scan(new PrivacyScanRequest
        {
            Options = new PrivacyGuardOptions
            {
                Path = Path.Combine(root, "examples"),
                SuppressedWarningCodes = ["privacy.generatedArtifact.forbidden"]
            }
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Scan);
        Assert.True(result.Scan!.SuppressedWarningCount >= 0);
    }

    [Fact]
    public void OnboardingPackageWorkflowService_ShouldValidatePackage()
    {
        var packagePath = Path.Combine(NewTempDirectory(), "example.template-pilot.zip");
        var build = new TemplatePilotPackageBuilder().Build(OnboardingWorkspacePath(), packagePath);

        var result = new OnboardingPackageWorkflowService().Validate(new OnboardingPackageValidateRequest
        {
            PackagePath = packagePath
        });

        Assert.True(build.IsValid, string.Join(Environment.NewLine, build.Errors));
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Validation);
        Assert.True(result.Validation!.IsValid);
    }

    [Theory]
    [InlineData("requirements")]
    [InlineData("negative-fixtures")]
    [InlineData("privacy")]
    [InlineData("package-validate")]
    public void WorkflowServices_ShouldReturnRequestDiagnostics(string service)
    {
        ServiceResult result = service switch
        {
            "requirements" => new RequirementsWorkflowService().Validate(new RequirementsValidateRequest()),
            "negative-fixtures" => new NegativeFixturesWorkflowService().Run(new NegativeFixturesRunRequest()),
            "privacy" => new PrivacyWorkflowService().Scan(new PrivacyScanRequest()),
            "package-validate" => new OnboardingPackageWorkflowService().Validate(new OnboardingPackageValidateRequest()),
            _ => throw new ArgumentOutOfRangeException(nameof(service))
        };

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("error", diagnostic.Severity));
    }

    private static (ThesisDocument Document, ThesisFormatSpec Format, string BaseDirectory) LoadSimple()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var documentPath = Path.Combine(root, "examples", "simple-thesis", "document.json");
        var formatPath = Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json");
        return (
            JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options)!,
            JsonSerializer.Deserialize<ThesisFormatSpec>(File.ReadAllText(formatPath), ThesisJson.Options)!,
            Path.GetDirectoryName(documentPath)!);
    }

    private static string TemplatePath()
    {
        return Path.Combine(TestRenderHelper.LocateRepoRootForTests(), "examples", "templates", "example-university-engineering");
    }

    private static string OnboardingWorkspacePath()
    {
        return Path.Combine(TestRenderHelper.LocateRepoRootForTests(), "examples", "onboarding", "example-engineering-pilot");
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
