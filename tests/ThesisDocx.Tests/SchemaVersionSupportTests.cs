using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Versioning;

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
}
