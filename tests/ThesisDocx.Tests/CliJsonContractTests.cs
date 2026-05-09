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
        AssertReportVersion(json);
        var diagnostic = RequiredDiagnostic(json, "table.gridSpan.outOfRange");
        AssertDiagnosticContract(diagnostic, expectedSeverity: "error", expectedCategory: "semantic");
        Assert.Equal("$.sections[0].blocks[0].rows[0].cells[0].gridSpan", diagnostic["path"]!.GetValue<string>());
        AssertVersionReport(json, expectedKinds: ["thesisDocument", "thesisFormatSpec"]);
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
        AssertReportVersion(json);
        var diagnostic = RequiredDiagnostic(json, "template.variable.missing");
        AssertDiagnosticContract(diagnostic, expectedSeverity: "error", expectedCategory: "template");
        Assert.Equal("Define the referenced variable or remove the reference.", diagnostic["fixHint"]!.GetValue<string>());
        AssertVersionReport(json, expectedKinds: ["templatePackage", "thesisFormatSpec"]);
    }

    [Fact]
    public void CliJson_UnsupportedVersionReport_ShouldExposeChecksAndDiagnostics()
    {
        var root = RepoRoot();
        var temp = NewTempDirectory();
        var documentPath = Path.Combine(temp, "future-document.json");
        File.WriteAllText(
            documentPath,
            File.ReadAllText(Path.Combine(root, "examples", "simple-thesis", "document.json"))
                .Replace("\"schemaVersion\": \"1.0.0\"", "\"schemaVersion\": \"9.9.9\"", StringComparison.Ordinal));

        var result = CliRunner.Run(
            root,
            "validate-input",
            "--document", documentPath,
            "--format", Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json"),
            "--json");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        var json = ParseObject(result.StandardOutput);
        AssertReportVersion(json);
        var versionReport = AssertVersionReport(json, expectedKinds: ["thesisDocument", "thesisFormatSpec"]);
        Assert.False(versionReport["isValid"]!.GetValue<bool>());
        var check = RequiredVersionCheck(versionReport, "thesisDocument");
        Assert.Equal("future", check["direction"]!.GetValue<string>());
        Assert.False(check["isSupported"]!.GetValue<bool>());
        var diagnostic = RequiredVersionDiagnostic(versionReport, "thesis.schemaVersion.unsupported");
        AssertDiagnosticContract(diagnostic, expectedSeverity: "error", expectedCategory: "schema");
        Assert.Equal("future", diagnostic["details"]!["direction"]!.GetValue<string>());
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
            ("template resolve", () => CliRunner.Run(root, "template", "resolve", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"), "--json"), result => ParseObject(result.StandardOutput), 0),
            ("inspect", () => CliRunner.Run(root, "inspect", "--docx", rendered.DocxPath), result => ParseObject(result.StandardOutput), 0),
            ("template coverage", () => CliRunner.Run(root, "template", "coverage", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering")), result => ParseObject(result.StandardOutput), 0),
            ("requirements validate", () => CliRunner.Run(root, "requirements", "validate", "--requirements", Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"), "--json"), result => ParseObject(result.StandardOutput), 0)
        };

        foreach (var command in commands)
        {
            var result = command.Run();
            Assert.Equal(command.ExpectedExit, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), $"{command.Name}: {result.StandardError}");
            var json = command.ReadJson(result);
            Assert.NotNull(json);
            if (command.Name is "validate" or "template resolve" or "inspect" or "template coverage" or "requirements validate")
            {
                AssertReportVersion(json);
            }
        }

        AssertReportFile(root, "requirements report", Path.Combine(temp, "requirements-report.json"),
            path => CliRunner.Run(root, "requirements", "report", "--requirements", Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"), "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"), "--out", path));
        AssertReportFile(root, "template gate", Path.Combine(temp, "template-gate.json"),
            path => CliRunner.Run(root, "template", "gate", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"), "--document", Path.Combine(root, "examples", "full-thesis", "document.json"), "--out", path),
            expectedVersionKinds: ["thesisDocument", "templatePackage", "thesisFormatSpec"]);
        AssertReportFile(root, "template diagnose", Path.Combine(temp, "template-diagnose.json"),
            path => CliRunner.Run(root, "template", "diagnose", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"), "--document", Path.Combine(root, "examples", "full-thesis", "document.json"), "--requirements", Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"), "--suite", Path.Combine(root, "examples", "template-regression", "template-regression-suite.json"), "--out", path),
            expectedVersionKinds: ["thesisDocument", "templatePackage", "thesisFormatSpec"]);
        AssertReportFile(root, "template authoring-report", Path.Combine(temp, "template-authoring.json"),
            path => CliRunner.Run(root, "template", "authoring-report", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"), "--document", Path.Combine(root, "examples", "full-thesis", "document.json"), "--requirements", Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"), "--suite", Path.Combine(root, "examples", "template-regression", "template-regression-suite.json"), "--threshold", "0.85", "--out", path),
            expectedVersionKinds: ["thesisDocument", "templatePackage", "thesisFormatSpec"]);
        AssertReportFile(root, "negative-fixtures", Path.Combine(temp, "negative-fixtures.json"),
            path => CliRunner.Run(root, "negative-fixtures", "run", "--manifest", Path.Combine(root, "examples", "negative-fixtures", "negative-fixture-manifest.json"), "--out", path));
        AssertReportFile(root, "ci quality-report", Path.Combine(temp, "ci-quality.json"),
            path => CliRunner.Run(root, "ci", "quality-report", "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"), "--document", Path.Combine(root, "examples", "full-thesis", "document.json"), "--requirements", Path.Combine(root, "examples", "requirements", "example-engineering-requirements.json"), "--suite", Path.Combine(root, "examples", "template-regression", "template-regression-suite.json"), "--negative-fixtures", Path.Combine(root, "examples", "negative-fixtures", "negative-fixture-manifest.json"), "--threshold", "0.85", "--out", path),
            expectedVersionKinds: ["thesisDocument", "templatePackage", "thesisFormatSpec"]);
    }

    [Fact]
    public void CliJson_Render_ShouldReturnArtifactMetadata()
    {
        var root = RepoRoot();
        var temp = NewTempDirectory();
        var output = Path.Combine(temp, "rendered.docx");

        var result = CliRunner.Run(
            root,
            "render",
            "--document", Path.Combine(root, "examples", "simple-thesis", "document.json"),
            "--format", Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json"),
            "--out", output,
            "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        Assert.True(File.Exists(output));
        var json = ParseObject(result.StandardOutput);
        AssertReportVersion(json);
        Assert.True(json["success"]!.GetValue<bool>());
        Assert.Equal("rendered.docx", json["artifact"]!["path"]!.GetValue<string>());
        AssertVersionReport(json, expectedKinds: ["thesisDocument", "thesisFormatSpec"]);
    }

    [Fact]
    public void CliJson_RenderInvalidInput_ShouldReturnDiagnosticsWithoutArtifact()
    {
        var root = RepoRoot();
        var temp = NewTempDirectory();
        var documentPath = Path.Combine(temp, "future-document.json");
        var output = Path.Combine(temp, "should-not-render.docx");
        File.WriteAllText(
            documentPath,
            File.ReadAllText(Path.Combine(root, "examples", "simple-thesis", "document.json"))
                .Replace("\"schemaVersion\": \"1.0.0\"", "\"schemaVersion\": \"9.9.9\"", StringComparison.Ordinal));

        var result = CliRunner.Run(
            root,
            "render",
            "--document", documentPath,
            "--format", Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json"),
            "--out", output,
            "--json");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        Assert.False(File.Exists(output));
        var json = ParseObject(result.StandardOutput);
        AssertReportVersion(json);
        Assert.False(json["success"]!.GetValue<bool>());
        AssertDiagnosticContract(RequiredDiagnostic(json, "thesis.schemaVersion.unsupported"), "error", "schema");
        AssertVersionReport(json, expectedKinds: ["thesisDocument", "thesisFormatSpec"]);
    }

    [Fact]
    public void CliJson_RenderWithInvalidTemplate_ShouldReturnTemplateDiagnostics()
    {
        var root = RepoRoot();
        var temp = NewTempDirectory();
        var output = Path.Combine(temp, "should-not-render.docx");

        var result = CliRunner.Run(
            root,
            "render",
            "--document", Path.Combine(root, "examples", "simple-thesis", "document.json"),
            "--template", Path.Combine(root, "examples", "negative-fixtures", "templates", "missing-required-variable"),
            "--out", output,
            "--json");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        Assert.False(File.Exists(output));
        var json = ParseObject(result.StandardOutput);
        AssertReportVersion(json);
        Assert.False(json["success"]!.GetValue<bool>());
        AssertDiagnosticContract(RequiredDiagnostic(json, "template.variable.requiredMissing"), "error", "template");
        AssertVersionReport(json, expectedKinds: ["templatePackage", "thesisFormatSpec"]);
    }

    [Fact]
    public void CliJson_TemplateGateUnsupportedVersion_ShouldExposeVersionReport()
    {
        var root = RepoRoot();
        var temp = NewTempDirectory();
        var documentPath = Path.Combine(temp, "future-document.json");
        File.WriteAllText(
            documentPath,
            File.ReadAllText(Path.Combine(root, "examples", "full-thesis", "document.json"))
                .Replace("\"schemaVersion\": \"1.1.0\"", "\"schemaVersion\": \"9.9.9\"", StringComparison.Ordinal));
        var outputPath = Path.Combine(temp, "gate.json");

        var result = CliRunner.Run(
            root,
            "template", "gate",
            "--template", Path.Combine(root, "examples", "templates", "example-university-engineering"),
            "--document", documentPath,
            "--out", outputPath);

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        var json = ParseObject(File.ReadAllText(outputPath));
        AssertReportVersion(json);
        var versionReport = AssertVersionReport(json, expectedKinds: ["thesisDocument", "templatePackage", "thesisFormatSpec"]);
        Assert.False(versionReport["isValid"]!.GetValue<bool>());
        Assert.Equal("future", RequiredVersionCheck(versionReport, "thesisDocument")["direction"]!.GetValue<string>());
        AssertDiagnosticContract(RequiredVersionDiagnostic(versionReport, "thesis.schemaVersion.unsupported"), "error", "schema");
    }

    private static void AssertReportFile(string root, string name, string outputPath, Func<string, CliResult> run, string[]? expectedVersionKinds = null)
    {
        var result = run(outputPath);
        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), $"{name}: {result.StandardError}");
        Assert.True(File.Exists(outputPath), $"{name} did not write {outputPath}");
        var json = ParseObject(File.ReadAllText(outputPath));
        AssertReportVersion(json);
        Assert.DoesNotContain(EnumerateSeverityValues(json), severity => severity == "breaking");
        if (expectedVersionKinds is not null)
        {
            AssertReportVersion(json);
            AssertVersionReport(json, expectedVersionKinds);
        }

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

    private static void AssertReportVersion(JsonObject json)
    {
        Assert.Equal("1.0.0", json["reportVersion"]?.GetValue<string>());
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

    private static JsonObject AssertVersionReport(JsonObject json, string[] expectedKinds)
    {
        var versionReport = json["versionReport"]?.AsObject()
            ?? throw new Xunit.Sdk.XunitException("Missing versionReport.");
        Assert.Equal("1.0.0", versionReport["reportVersion"]!.GetValue<string>());
        Assert.NotNull(versionReport["diagnostics"]?.AsArray());
        var checks = versionReport["checks"]!.AsArray().OfType<JsonObject>().ToList();
        var kinds = checks
            .Select(check => check["kind"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        foreach (var kind in expectedKinds)
        {
            Assert.Contains(kind, kinds);
        }

        foreach (var check in checks)
        {
            Assert.Contains(check["direction"]!.GetValue<string>(), ValidVersionDirections);
            Assert.True(check["supportedVersions"]!.AsArray().Count > 0);
        }

        foreach (var diagnostic in versionReport["diagnostics"]!.AsArray().OfType<JsonObject>())
        {
            AssertDiagnosticContract(
                diagnostic,
                expectedSeverity: diagnostic["severity"]!.GetValue<string>(),
                expectedCategory: diagnostic["category"]!.GetValue<string>());
        }

        return versionReport;
    }

    private static JsonObject RequiredVersionCheck(JsonObject versionReport, string kind)
    {
        return versionReport["checks"]!.AsArray()
            .OfType<JsonObject>()
            .Single(check => check["kind"]?.GetValue<string>() == kind);
    }

    private static JsonObject RequiredVersionDiagnostic(JsonObject versionReport, string code)
    {
        return versionReport["diagnostics"]!.AsArray()
            .OfType<JsonObject>()
            .Single(diagnostic => diagnostic["code"]?.GetValue<string>() == code);
    }

    private static IEnumerable<string> EnumerateSeverityValues(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj)
            {
                if (pair.Key.Equals("severity", StringComparison.OrdinalIgnoreCase) && pair.Value is JsonValue value && value.TryGetValue<string>(out var severity))
                {
                    yield return severity;
                }

                foreach (var nested in EnumerateSeverityValues(pair.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                foreach (var nested in EnumerateSeverityValues(item))
                {
                    yield return nested;
                }
            }
        }
    }

    private static string RepoRoot() => TestRenderHelper.LocateRepoRootForTests();

    private static readonly HashSet<string> ValidVersionDirections = new(StringComparer.Ordinal)
    {
        "current",
        "supported",
        "old",
        "future",
        "missing",
        "unsupported",
        "unknown"
    };

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
