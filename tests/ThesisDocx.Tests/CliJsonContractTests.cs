using System.Text.Json.Nodes;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class CliJsonContractTests
{
    [Fact]
    public void CliJson_InvalidInputDiagnostics_ShouldUseUnifiedContract()
    {
        var root = RepoRoot();
        var result = CliRunner.Run(
            root,
            "validate-input",
            "--document", Path.Combine(root, "examples", "negative-fixtures", "documents", "table-gridspan-too-wide.json"),
            "--format", Path.Combine(root, "examples", "format-specs", "strict-cn-thesis.json"),
            "--json");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        var json = ParseObject(result.StandardOutput);
        var diagnostic = RequiredDiagnostic(json, "table.gridSpan.outOfRange");
        AssertDiagnosticContract(diagnostic, expectedSeverity: "error", expectedCategory: "semantic");
        Assert.Equal("$.sections[0].blocks[0].rows[0].cells[0].gridSpan", diagnostic["path"]!.GetValue<string>());
    }

    [Fact]
    public void CliJson_TemplateValidateDiagnostics_ShouldUseUnifiedContract()
    {
        var root = RepoRoot();
        var result = CliRunner.Run(
            root,
            "template", "validate",
            "--template", Path.Combine(root, "examples", "negative-fixtures", "templates", "missing-variable-reference"),
            "--json");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        var json = ParseObject(result.StandardOutput);
        var diagnostic = RequiredDiagnostic(json, "template.variable.missing");
        AssertDiagnosticContract(diagnostic, expectedSeverity: "error", expectedCategory: "template");
        Assert.Equal("Define the referenced variable or remove the reference.", diagnostic["fixHint"]!.GetValue<string>());
    }

    [Fact]
    public void CliJson_CoreCommands_ShouldProduceParseableJson()
    {
        var root = RepoRoot();
        var rendered = TestRenderHelper.RenderSimpleThesis();
        var temp = NewTempDirectory();

        var commands = new List<(string Name, Func<CliResult> Run, Func<CliResult, JsonObject> ReadJson, int ExpectedExit)>
        {
            ("validate", () => CliRunner.Run(root, "validate", "--docx", rendered.DocxPath, "--format", Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json"), "--json"), result => ParseObject(result.StandardOutput), 0),
            ("template inspect", () => CliRunner.Run(root, "template", "inspect", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering")), result => ParseObject(result.StandardOutput), 0),
            ("template resolve", () => CliRunner.Run(root, "template", "resolve", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering")), result => ParseObject(result.StandardOutput), 0),
            ("template coverage", () => CliRunner.Run(root, "template", "coverage", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering")), result => ParseObject(result.StandardOutput), 0),
            ("requirements validate", () => CliRunner.Run(root, "requirements", "validate", "--requirements", Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"), "--json"), result => ParseObject(result.StandardOutput), 0)
        };

        foreach (var command in commands)
        {
            var result = command.Run();
            Assert.Equal(command.ExpectedExit, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), $"{command.Name}: {result.StandardError}");
            Assert.NotNull(command.ReadJson(result));
        }

        AssertReportFile(root, "requirements report", Path.Combine(temp, "requirements-report.json"),
            path => CliRunner.Run(root, "requirements", "report", "--requirements", Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"), "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"), "--out", path));
        AssertReportFile(root, "template gate", Path.Combine(temp, "template-gate.json"),
            path => CliRunner.Run(root, "template", "gate", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"), "--document", Path.Combine(root, "examples", "full-thesis", "document.json"), "--out", path));
        AssertReportFile(root, "template diagnose", Path.Combine(temp, "template-diagnose.json"),
            path => CliRunner.Run(root, "template", "diagnose", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"), "--document", Path.Combine(root, "examples", "full-thesis", "document.json"), "--requirements", Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"), "--suite", Path.Combine(root, "examples", "template-regression", "template-regression-suite.json"), "--out", path));
        AssertReportFile(root, "template authoring-report", Path.Combine(temp, "template-authoring.json"),
            path => CliRunner.Run(root, "template", "authoring-report", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"), "--document", Path.Combine(root, "examples", "full-thesis", "document.json"), "--requirements", Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"), "--suite", Path.Combine(root, "examples", "template-regression", "template-regression-suite.json"), "--threshold", "0.85", "--out", path));
        AssertReportFile(root, "negative-fixtures", Path.Combine(temp, "negative-fixtures.json"),
            path => CliRunner.Run(root, "negative-fixtures", "run", "--manifest", Path.Combine(root, "examples", "negative-fixtures", "negative-fixture-manifest.json"), "--out", path));
        AssertReportFile(root, "ci quality-report", Path.Combine(temp, "ci-quality.json"),
            path => CliRunner.Run(root, "ci", "quality-report", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"), "--document", Path.Combine(root, "examples", "full-thesis", "document.json"), "--requirements", Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"), "--suite", Path.Combine(root, "examples", "template-regression", "template-regression-suite.json"), "--negative-fixtures", Path.Combine(root, "examples", "negative-fixtures", "negative-fixture-manifest.json"), "--threshold", "0.85", "--out", path));
    }

    private static void AssertReportFile(string root, string name, string outputPath, Func<string, CliResult> run)
    {
        var result = run(outputPath);
        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), $"{name}: {result.StandardError}");
        Assert.True(File.Exists(outputPath), $"{name} did not write {outputPath}");
        Assert.NotNull(ParseObject(File.ReadAllText(outputPath)));
        _ = root;
    }

    private static JsonObject ParseObject(string json)
    {
        return JsonNode.Parse(json)?.AsObject()
            ?? throw new Xunit.Sdk.XunitException("Expected JSON object.");
    }

    private static JsonObject RequiredDiagnostic(JsonObject json, string code)
    {
        var diagnostics = json["diagnostics"]?.AsArray()
            ?? throw new Xunit.Sdk.XunitException("Missing diagnostics array.");
        return diagnostics
            .OfType<JsonObject>()
            .Single(diagnostic => diagnostic["code"]?.GetValue<string>() == code);
    }

    private static void AssertDiagnosticContract(JsonObject diagnostic, string expectedSeverity, string expectedCategory)
    {
        Assert.Equal(expectedSeverity, diagnostic["severity"]!.GetValue<string>());
        Assert.Equal(expectedCategory, diagnostic["category"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(diagnostic["code"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(diagnostic["path"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(diagnostic["message"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(diagnostic["fixHint"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(diagnostic["source"]!.GetValue<string>()));
    }

    private static string RepoRoot() => TestRenderHelper.LocateRepoRootForTests();

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
