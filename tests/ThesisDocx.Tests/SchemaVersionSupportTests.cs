using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Versioning;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class SchemaVersionSupportTests
{
    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.1.0")]
    public void ThesisDocumentVersions_ShouldDeclareSupportedRange(string version)
    {
        var result = new SchemaVersionSupport().CheckThesisDocument(version);

        Assert.True(result.IsSupported);
        Assert.Null(result.Diagnostic);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.1.0")]
    [InlineData("1.2.0")]
    public void FormatSpecVersions_ShouldDeclareSupportedRange(string version)
    {
        var result = new SchemaVersionSupport().CheckThesisFormatSpec(version);

        Assert.True(result.IsSupported);
    }

    [Fact]
    public void TemplatePackageVersion_ShouldDeclareSupportedRange()
    {
        var result = new SchemaVersionSupport().CheckTemplatePackage("1.0.0");

        Assert.True(result.IsSupported);
    }

    [Theory]
    [InlineData("thesisDocument", "1.0.0", "supported")]
    [InlineData("thesisDocument", "1.1.0", "current")]
    [InlineData("thesisFormatSpec", "1.0.0", "supported")]
    [InlineData("thesisFormatSpec", "1.2.0", "current")]
    [InlineData("templatePackage", "1.0.0", "current")]
    public void SupportedVersions_ShouldClassifyDirection(string kind, string version, string expectedDirection)
    {
        var support = new SchemaVersionSupport();
        var result = kind switch
        {
            "thesisDocument" => support.CheckThesisDocument(version),
            "thesisFormatSpec" => support.CheckThesisFormatSpec(version),
            "templatePackage" => support.CheckTemplatePackage(version),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        Assert.True(result.IsSupported);
        Assert.Equal(expectedDirection, result.Direction);
    }

    [Fact]
    public void UnsupportedOldVersion_ShouldReturnDiagnostic()
    {
        var result = new SchemaVersionSupport().CheckThesisDocument("0.9.0");

        Assert.False(result.IsSupported);
        Assert.Equal("old", result.Direction);
        Assert.Equal("thesis.schemaVersion.unsupported", result.Diagnostic!.Code);
        Assert.Equal("error", result.Diagnostic.Severity);
    }

    [Fact]
    public void UnsupportedFutureVersion_ShouldReturnDiagnostic()
    {
        var result = new SchemaVersionSupport().CheckThesisFormatSpec("9.0.0");

        Assert.False(result.IsSupported);
        Assert.Equal("future", result.Direction);
        Assert.Equal("format.schemaVersion.unsupported", result.Diagnostic!.Code);
        Assert.Equal("1.0.0,1.1.0,1.2.0", result.Diagnostic.Details["supportedVersions"]);
    }

    [Fact]
    public void NoOpMigrators_ShouldPreserveVersions()
    {
        var document = new ThesisDocument { SchemaVersion = "1.0.0" };
        var format = new ThesisFormatSpec { SchemaVersion = "1.2.0" };
        var template = new TemplatePackage { TemplateSchemaVersion = "1.0.0" };

        Assert.Equal("1.0.0", new NoOpThesisDocumentMigrator().Migrate(document, "1.1.0").SchemaVersion);
        Assert.Equal("1.2.0", new NoOpFormatSpecMigrator().Migrate(format, "1.2.0").SchemaVersion);
        Assert.Equal("1.0.0", new NoOpTemplatePackageMigrator().Migrate(template, "1.0.0").TemplateSchemaVersion);
    }

    [Fact]
    public void VersionReport_ShouldAggregateDocumentAndFormatChecks()
    {
        var report = SchemaVersionReport.ForDocumentAndFormat("1.0.0", "1.2.0");

        Assert.Equal("1.0.0", report.ReportVersion);
        Assert.True(report.IsValid);
        Assert.Contains(report.Checks, check => check.Kind == "thesisDocument" && check.Direction == "supported");
        Assert.Contains(report.Checks, check => check.Kind == "thesisFormatSpec" && check.Direction == "current");
    }

    [Fact]
    public void Cli_ValidateInput_ShouldReportUnsupportedFutureVersion()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var document = JsonSerializer.Deserialize<ThesisDocument>(
            File.ReadAllText(Path.Combine(root, "examples", "simple-thesis", "document.json")),
            ThesisJson.Options)!;
        document.SchemaVersion = "9.9.9";
        var temp = NewTempDirectory();
        var documentPath = Path.Combine(temp, "future-document.json");
        File.WriteAllText(documentPath, JsonSerializer.Serialize(document, ThesisJson.Options));

        var result = CliRunner.Run(
            root,
            "validate-input",
            "--document", documentPath,
            "--format", Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json"),
            "--json");
        var json = JsonNode.Parse(result.StandardOutput)!.AsObject();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(json["diagnostics"]!.AsArray(), diagnostic => diagnostic?["code"]?.GetValue<string>() == "thesis.schemaVersion.unsupported");
        var checks = json["versionReport"]!["checks"]!.AsArray();
        Assert.Contains(checks, check =>
            check?["kind"]?.GetValue<string>() == "thesisDocument"
            && check["direction"]?.GetValue<string>() == "future");
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
