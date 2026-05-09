using System.Text.Json;
using ThesisDocx.Core.Diff;
using ThesisDocx.Core.Diff.Layout;
using ThesisDocx.Core.Onboarding.Packaging;
using ThesisDocx.Core.Privacy;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class ReportContractSchemaTests
{
    [Fact]
    public void Schema_ShouldValidateRenderResultReportContract()
    {
        var root = RepoRoot();
        var output = TempPath("rendered.docx");
        var result = CliRunner.Run(
            root,
            "render",
            "--document", Path.Combine(root, "examples", "simple-thesis", "document.json"),
            "--format", Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json"),
            "--out", output,
            "--json");
        var report = TempPath("render-result.json");
        File.WriteAllText(report, result.StandardOutput);

        Assert.Equal(0, result.ExitCode);
        AssertReportContractValid(report);
    }

    [Fact]
    public void Schema_ShouldValidatePrivacyReportContract()
    {
        var report = TempPath("privacy.json");
        var scan = new PrivacyGuard().Scan(new PrivacyGuardOptions
        {
            Path = Path.Combine(RepoRoot(), "examples"),
            SuppressedWarningCodes = ["privacy.generatedArtifact.forbidden"]
        });
        File.WriteAllText(report, JsonSerializer.Serialize(scan, ThesisJson.Options));

        AssertReportContractValid(report);
    }

    [Fact]
    public void Schema_ShouldValidateDocxDiffAndLayoutReportContracts()
    {
        var docx = TestRenderHelper.RenderFullThesis().DocxPath;
        var diffReport = TempPath("docx-diff.json");
        var signatureReport = TempPath("layout-signature.json");
        var compareReport = TempPath("layout-compare.json");
        var signature = new DocxLayoutSignatureExtractor().Extract(docx);

        File.WriteAllText(diffReport, JsonSerializer.Serialize(new DocxStructureDiffEngine().Compare(docx, docx), ThesisJson.Options));
        File.WriteAllText(signatureReport, JsonSerializer.Serialize(signature, ThesisJson.Options));
        File.WriteAllText(compareReport, JsonSerializer.Serialize(new LayoutSignatureComparer().Compare(signature, signature), ThesisJson.Options));

        AssertReportContractValid(diffReport);
        AssertReportContractValid(signatureReport);
        AssertReportContractValid(compareReport);
    }

    [Fact]
    public void Schema_ShouldValidateOnboardingPackageValidationReportContract()
    {
        var packagePath = TempPath("example.template-pilot.zip");
        var build = new TemplatePilotPackageBuilder().Build(OnboardingWorkspacePath(), packagePath);
        var validation = new TemplatePilotPackageValidator().Validate(packagePath);
        var report = TempPath("package-validation.json");
        File.WriteAllText(report, JsonSerializer.Serialize(validation, ThesisJson.Options));

        Assert.True(build.IsValid, string.Join(Environment.NewLine, build.Errors));
        AssertReportContractValid(report);
    }

    [Fact]
    public void Schema_ShouldRejectReportWithoutRootReportVersion()
    {
        var report = TempPath("invalid-report.json");
        File.WriteAllText(report, """{"success":true,"errorCount":0,"warningCount":0,"diagnostics":[]}""");

        var result = new ThesisSchemaValidator().ValidateReportContractFile(report, SchemaPath());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectInvalidVersionReportDirectionAndSeverity()
    {
        var report = TempPath("invalid-version-report.json");
        File.WriteAllText(
            report,
            """
            {
              "reportVersion": "1.0.0",
              "success": false,
              "errorCount": 1,
              "warningCount": 0,
              "diagnostics": [
                {
                  "code": "thesis.schemaVersion.unsupported",
                  "severity": "breaking",
                  "path": "$.schemaVersion",
                  "message": "Unsupported version.",
                  "category": "schema",
                  "source": "test"
                }
              ],
              "versionReport": {
                "reportVersion": "1.0.0",
                "isValid": false,
                "checks": [
                  {
                    "kind": "thesisDocument",
                    "version": "9.9.9",
                    "isSupported": false,
                    "direction": "newerThanKnown",
                    "supportedVersions": ["1.0.0", "1.1.0"]
                  }
                ],
                "diagnostics": []
              }
            }
            """);

        var result = new ThesisSchemaValidator().ValidateReportContractFile(report, SchemaPath());

        Assert.False(result.IsValid);
    }

    private static void AssertReportContractValid(string reportPath)
    {
        var result = new ThesisSchemaValidator().ValidateReportContractFile(reportPath, SchemaPath());
        Assert.True(result.IsValid, reportPath + Environment.NewLine + string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
    }

    private static string SchemaPath()
    {
        return Path.Combine(RepoRoot(), "schemas", "report-contract.schema.json");
    }

    private static string RepoRoot()
    {
        return TestRenderHelper.LocateRepoRootForTests();
    }

    private static string OnboardingWorkspacePath()
    {
        return Path.Combine(RepoRoot(), "examples", "onboarding", "example-engineering-pilot");
    }

    private static string TempPath(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}
