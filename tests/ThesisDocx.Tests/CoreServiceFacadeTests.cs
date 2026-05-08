using System.Text.Json;
using ThesisDocx.Core.Models;
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

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
