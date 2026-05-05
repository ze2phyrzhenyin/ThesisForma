using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Templates.Gate;
using ThesisDocx.Core.Templates.Regression;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class TemplateRegressionGateTests
{
    [Fact]
    public void TemplateRegressionRunner_ShouldRunExampleSuite()
    {
        var result = new TemplateRegressionRunner().Run(SuitePath(), NewTempDirectory());

        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Cases.SelectMany(c => c.Errors)));
        Assert.Contains(result.Cases, c => c.Id == "example-university-engineering-full" && c.LayoutSimilarity >= 0.99);
    }

    [Fact]
    public void TemplateRegressionRunner_ShouldFailWhenLayoutBelowThreshold()
    {
        var suite = CreateMutatedSuite(node =>
        {
            node["cases"]![0]!["minLayoutSimilarity"] = 1.0;
            node["cases"]![0]!["baselineLayoutPath"] = "bad-baseline.layout.json";
        });
        var badBaseline = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "examples", "template-regression", "baselines", "example-university-engineering-full.layout.json")))!;
        badBaseline["sections"]![0]!["topMarginTwips"] = "1";
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(suite)!, "bad-baseline.layout.json"), badBaseline.ToJsonString(ThesisJson.Options));

        var result = new TemplateRegressionRunner().Run(suite, NewTempDirectory());

        Assert.False(result.Passed);
        Assert.Contains(result.Cases.Single().Errors, error => error.StartsWith("layout.similarityBelowThreshold", StringComparison.Ordinal));
    }

    [Fact]
    public void TemplateRegressionRunner_ShouldCheckRequiredCustomProperties()
    {
        var result = new TemplateRegressionRunner().Run(SuitePath(), NewTempDirectory());

        Assert.True(result.Cases.Single().RequiredCustomPropertiesPassed);
    }

    [Fact]
    public void TemplateRegressionRunner_ShouldCheckRequiredParts()
    {
        var result = new TemplateRegressionRunner().Run(SuitePath(), NewTempDirectory());

        Assert.True(result.Cases.Single().RequiredPartsPassed);
    }

    [Fact]
    public void TemplateRegressionRunner_ShouldProduceDeterministicReport()
    {
        var first = new TemplateRegressionRunner().Run(SuitePath(), NewTempDirectory()).Cases.Select(c => c.Id).ToList();
        var second = new TemplateRegressionRunner().Run(SuitePath(), NewTempDirectory()).Cases.Select(c => c.Id).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Schema_ShouldValidateTemplateRegressionSuite()
    {
        var result = new ThesisSchemaValidator().ValidateTemplateRegressionSuiteFile(SuitePath(), Path.Combine(RepoRoot(), "schemas", "template-regression-suite.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldRejectInvalidTemplateRegressionExpectation()
    {
        var suite = CreateMutatedSuite(node => node["cases"]![0]!["minLayoutSimilarity"] = 2);

        var result = new ThesisSchemaValidator().ValidateTemplateRegressionSuiteFile(suite, Path.Combine(RepoRoot(), "schemas", "template-regression-suite.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Cli_TemplateRegression_ShouldWriteReport()
    {
        var output = Path.Combine(NewTempDirectory(), "report.json");

        var result = CliRunner.Run(RepoRoot(), "template", "regression", "--suite", SuitePath(), "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.True(json["passed"]!.GetValue<bool>());
    }

    [Fact]
    public void TemplateGate_ShouldPassForExampleTemplate()
    {
        var report = RunGate();

        Assert.True(report.Status is TemplateGateStatus.Pass or TemplateGateStatus.PassWithWarnings);
        Assert.DoesNotContain(report.Checks, check => check.Status == TemplateGateCheckStatus.Fail);
    }

    [Fact]
    public void TemplateGate_ShouldFailForInvalidTemplate()
    {
        var template = CreateInvalidTemplate();

        var report = RunGate(templatePath: template);

        Assert.Equal(TemplateGateStatus.Fail, report.Status);
        Assert.Contains(report.Checks, check => check.Code == "template.validate" && check.Status == TemplateGateCheckStatus.Fail);
    }

    [Fact]
    public void TemplateGate_ShouldFailWhenCoverageBelowThreshold()
    {
        var report = RunGate(threshold: 1.0);

        Assert.Equal(TemplateGateStatus.Fail, report.Status);
        Assert.Contains(report.Checks, check => check.Code == "coverage" && check.Status == TemplateGateCheckStatus.Fail);
    }

    [Fact]
    public void TemplateGate_ShouldIncludeArtifacts()
    {
        var report = RunGate();

        Assert.Contains("docx", report.Artifacts.Keys);
        Assert.Contains("layoutSignature", report.Artifacts.Keys);
        Assert.Contains("snapshot", report.Artifacts.Keys);
    }

    [Fact]
    public void TemplateGate_ShouldProduceDeterministicChecks()
    {
        var first = RunGate().Checks.Select(check => check.Code).ToList();
        var second = RunGate().Checks.Select(check => check.Code).ToList();

        Assert.Equal(first, second);
        Assert.Equal(first.OrderBy(code => code, StringComparer.Ordinal), first);
    }

    [Fact]
    public void Cli_TemplateGate_ShouldWriteGateReport()
    {
        var output = Path.Combine(NewTempDirectory(), "gate.json");

        var result = CliRunner.Run(RepoRoot(), "template", "gate", "--template", TemplatePath(), "--document", DocumentPath(), "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.NotEqual("fail", json["status"]!.GetValue<string>());
    }

    private static TemplateGateReport RunGate(string? templatePath = null, double threshold = 0.75)
    {
        return new TemplateGateService().Run(new TemplateGateOptions
        {
            TemplatePath = templatePath ?? TemplatePath(),
            DocumentPath = DocumentPath(),
            OutputDirectory = NewTempDirectory(),
            CoverageThreshold = threshold
        });
    }

    private static string CreateMutatedSuite(Action<JsonNode> mutate)
    {
        var node = JsonNode.Parse(File.ReadAllText(SuitePath()))!;
        node["cases"]![0]!["documentPath"] = DocumentPath();
        node["cases"]![0]!["templatePath"] = TemplatePath();
        mutate(node);
        var directory = NewTempDirectory();
        Directory.CreateDirectory(Path.Combine(directory, "baselines"));
        foreach (var source in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "examples", "template-regression", "baselines")))
        {
            File.Copy(source, Path.Combine(directory, "baselines", Path.GetFileName(source)));
        }

        var path = Path.Combine(directory, "suite.json");
        File.WriteAllText(path, node.ToJsonString(ThesisJson.Options));
        return path;
    }

    private static string CreateInvalidTemplate()
    {
        var directory = NewTempDirectory();
        foreach (var file in Directory.EnumerateFiles(TemplatePath(), "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(TemplatePath(), file);
            var target = Path.Combine(directory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        var templateJson = Path.Combine(directory, "template.json");
        var node = JsonNode.Parse(File.ReadAllText(templateJson))!;
        node["templateSchemaVersion"] = "9.9.9";
        File.WriteAllText(templateJson, node.ToJsonString(ThesisJson.Options));
        return directory;
    }

    private static string SuitePath() => Path.Combine(RepoRoot(), "examples", "template-regression", "template-regression-suite.json");

    private static string TemplatePath() => Path.Combine(RepoRoot(), "examples", "templates", "example-university-engineering");

    private static string DocumentPath() => Path.Combine(RepoRoot(), "examples", "full-thesis", "document.json");

    private static string RepoRoot() => TestRenderHelper.LocateRepoRootForTests();

    private static string NewTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
