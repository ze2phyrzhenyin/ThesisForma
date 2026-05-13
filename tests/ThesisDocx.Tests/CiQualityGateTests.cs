using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Ci;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Diagnostics.FixHints;
using ThesisDocx.Core.Templates.Authoring;
using ThesisDocx.Core.Testing.NegativeFixtures;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class CiQualityGateTests
{
    [Fact]
    public void CiScripts_ShouldExist()
    {
        foreach (var script in CiScripts())
        {
            Assert.True(File.Exists(Path.Combine(RepoRoot(), "scripts", script)), script);
        }
    }

    [Fact]
    public void CiScripts_ShouldReferenceQualityGateCommands()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "ci-quality-gate"));

        Assert.Contains("dotnet build ThesisDocx.slnx", content);
        Assert.Contains("dotnet test ThesisDocx.slnx", content);
        Assert.Contains("scripts/ci-negative-fixtures", content);
        Assert.Contains("ci quality-report", content);
    }

    [Fact]
    public void CiWorkflow_ShouldContainBuildTestAndQualityGate()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "ci", "workflows", "quality-gate.yml"));

        Assert.Contains("actions/checkout", content);
        Assert.Contains("actions/setup-dotnet", content);
        Assert.Contains("dotnet build", content);
        Assert.Contains("dotnet test", content);
        Assert.Contains("scripts/ci-quality-gate", content);
        Assert.Contains("actions/upload-artifact", content);
    }

    [Fact]
    public void GithubCiWorkflow_ShouldContainNodeSetupAndQualityGate()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "quality-gate.yml"));

        Assert.Contains("actions/checkout", content);
        Assert.Contains("actions/setup-dotnet", content);
        Assert.Contains("actions/setup-node", content);
        Assert.Contains("cache: npm", content);
        Assert.Contains("cache-dependency-path: web/package-lock.json", content);
        Assert.Contains("scripts/ci-quality-gate", content);
        Assert.Contains("actions/upload-artifact", content);
        Assert.Contains("path: out/ci", content);
    }

    [Fact]
    public void CiQualityGateScript_ShouldUseDeterministicOutputDirectory()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "ci-quality-gate"));

        Assert.Contains("CI_OUT_DIR", content);
        Assert.Contains("--out)", content);
        Assert.Contains("out/ci", content);
        Assert.DoesNotContain("/tmp/", content);
    }

    [Fact]
    public void CiRenderExamplesScript_ShouldRenderSimpleAndTemplate()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "ci-render-examples"));

        Assert.Contains("$OUT_DIR/simple.docx", content);
        Assert.Contains("$OUT_DIR/template-full.docx", content);
    }

    [Fact]
    public void CiTemplateQualityScript_ShouldRunGateDiagnoseAndAuthoring()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "ci-template-quality"));

        Assert.Contains("template gate", content);
        Assert.Contains("template diagnose", content);
        Assert.Contains("template authoring-report", content);
    }

    [Fact]
    public void CiQualityGateScript_ShouldInvokeWebQualityGate()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "ci-quality-gate"));

        Assert.Contains("scripts/web-quality-gate", content);
    }

    [Fact]
    public void WebQualityGateScript_ShouldRunReproducibleWebChecks()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "web-quality-gate"));

        Assert.Contains("npm --prefix web ci", content);
        Assert.Contains("npm --prefix web run typecheck", content);
        Assert.Contains("npm --prefix web test", content);
        Assert.Contains("npm --prefix web run build", content);
        Assert.Contains("npm --prefix web run e2e", content);
        Assert.Contains("WEB_E2E:-1", content);
    }

    [Fact]
    public void PlaywrightConfig_ShouldUseIsolatedServerPort()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "web", "playwright.config.ts"));

        Assert.Contains("PLAYWRIGHT_PORT", content);
        Assert.Contains("127.0.0.1", content);
        Assert.Contains("reuseExistingServer: false", content);
        Assert.Contains("port: e2ePort", content);
    }

    [Fact]
    public void CiNegativeFixturesScript_ShouldRunNegativeFixtures()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "ci-negative-fixtures"));

        Assert.Contains("negative-fixtures run", content);
        Assert.Contains("examples/negative-fixtures/negative-fixture-manifest.json", content);
    }

    [Fact]
    public void CiSchemaCheckScript_ShouldRunGeneratedDocsTypesAndExamples()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "ci-schema-check"));

        Assert.Contains("scripts/generate-schema-docs --check", content);
        Assert.Contains("scripts/generate-web-types --check", content);
        Assert.Contains("validate-input", content);
        Assert.Contains("examples/simple-thesis/document.json", content);
        Assert.Contains("examples/full-thesis/document.json", content);
        Assert.Contains("requirements validate", content);
        Assert.Contains("template validate", content);
    }

    [Fact]
    public void Schema_ShouldValidateNegativeFixtureManifest()
    {
        var result = new ThesisSchemaValidator().ValidateNegativeFixtureManifestFile(NegativeManifestPath(), SchemaPath("negative-fixture-manifest.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldRejectNegativeFixtureManifestWithUnknownType()
    {
        var mutated = CreateMutatedNegativeManifest(node => node["cases"]![0]!["type"] = "unknown");

        var result = new ThesisSchemaValidator().ValidateNegativeFixtureManifestFile(mutated, SchemaPath("negative-fixture-manifest.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectNegativeFixtureManifestWithInvalidSeverity()
    {
        var mutated = CreateMutatedNegativeManifest(node => node["cases"]![0]!["expectedSeverity"] = "severe");

        var result = new ThesisSchemaValidator().ValidateNegativeFixtureManifestFile(mutated, SchemaPath("negative-fixture-manifest.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldValidateFixHintRules()
    {
        var result = new ThesisSchemaValidator().ValidateFixHintRulesFile(FixHintRulesPath(), SchemaPath("fix-hint-rules.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldValidateAllQualityGateExamples()
    {
        var schema = new ThesisSchemaValidator();
        var negative = schema.ValidateNegativeFixtureManifestFile(NegativeManifestPath(), SchemaPath("negative-fixture-manifest.schema.json"));
        var rules = schema.ValidateFixHintRulesFile(FixHintRulesPath(), SchemaPath("fix-hint-rules.schema.json"));

        Assert.True(negative.IsValid, string.Join(Environment.NewLine, negative.Errors));
        Assert.True(rules.IsValid, string.Join(Environment.NewLine, rules.Errors));
    }

    [Fact]
    public void FixHintRuleCatalog_ShouldLoadRules()
    {
        var catalog = FixHintRuleCatalog.LoadDefault();

        Assert.True(catalog.Rules.Count >= 33);
    }

    [Fact]
    public void FixHintRuleCatalog_ShouldContainRequiredRules()
    {
        var ids = FixHintRuleCatalog.LoadDefault().Rules.Select(rule => rule.HintId).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("fix.pageMargins", ids);
        Assert.Contains("fix.templateAsset", ids);
        Assert.Contains("fix.requirementEvidence", ids);
        Assert.Contains("fix.schemaVersion", ids);
    }

    [Fact]
    public void FixHintEngine_ShouldUseCatalogRuleForMarginMismatch()
    {
        var hint = new FixHintEngine().Suggest(Issue("PageMarginMismatch", "pageSetup")).Single();

        Assert.Equal("fix.pageMargins", hint.HintId);
        Assert.Equal("$.pageSetup", hint.SuggestedSpecPath);
    }

    [Fact]
    public void FixHintEngine_ShouldUseCatalogRuleForMissingAsset()
    {
        var hint = new FixHintEngine().Suggest(Issue("TemplateMissingAsset", "template")).Single();

        Assert.Equal("fix.templateAsset", hint.HintId);
        Assert.Equal("$.assets", hint.SuggestedTemplatePath);
    }

    [Fact]
    public void FixHintEngine_ShouldUseCatalogRuleForRequirementMissingEvidence()
    {
        var hint = new FixHintEngine().Suggest(Issue("RequirementMissingEvidence", "requirements")).Single();

        Assert.Equal("fix.requirementEvidence", hint.HintId);
    }

    [Fact]
    public void FixHintEngine_ShouldReturnGenericHintWhenNoRuleMatches()
    {
        var hint = new FixHintEngine().Suggest(Issue("UnknownProblem", "unknown")).Single();

        Assert.Equal("fix.review", hint.HintId);
    }

    [Fact]
    public void FixHintEngine_ShouldAttachDocsOrFixtureRefs()
    {
        var hints = FixHintRuleCatalog.LoadDefault().Rules;

        Assert.All(hints, rule => Assert.True(!string.IsNullOrWhiteSpace(rule.DocsRef) || !string.IsNullOrWhiteSpace(rule.ExampleFixtureRef)));
    }

    [Fact]
    public void FixHintRules_ShouldReferenceExistingDocs()
    {
        foreach (var rule in FixHintRuleCatalog.LoadDefault().Rules.Where(rule => !string.IsNullOrWhiteSpace(rule.DocsRef)))
        {
            Assert.True(File.Exists(Path.Combine(RepoRoot(), rule.DocsRef!)), rule.DocsRef);
        }
    }

    [Fact]
    public void NegativeFixtureRunner_ShouldPassWhenExpectedFailuresOccur()
    {
        var result = new NegativeFixtureRunner().Run(NegativeManifestPath());

        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Cases.SelectMany(c => c.Errors)));
    }

    [Fact]
    public void NegativeFixtureRunner_ShouldFailWhenNegativeFixtureUnexpectedlyPasses()
    {
        var manifest = CreateMutatedNegativeManifest(node =>
        {
            node["cases"]![0]!["expectedExitCode"] = 0;
            node["cases"]![0]!["expectedCodes"] = new JsonArray("NotExpected");
        });

        var result = new NegativeFixtureRunner().Run(manifest);

        Assert.False(result.Passed);
        Assert.Contains(result.Cases, c => !c.Passed);
    }

    [Fact]
    public void NegativeFixtureRunner_ShouldCheckExpectedCodes()
    {
        var manifest = CreateMutatedNegativeManifest(node => node["cases"]![0]!["expectedCodes"] = new JsonArray("MissingCode"));

        var result = new NegativeFixtureRunner().Run(manifest);

        var target = result.Cases.Single(c => c.Id == "requirements-missing-evidence");
        Assert.Contains(target.Errors, error => error == "missingCode:MissingCode");
    }

    [Fact]
    public void NegativeFixtureRunner_ShouldCheckExpectedFixHints()
    {
        var manifest = CreateMutatedNegativeManifest(node => node["cases"]![0]!["expectedFixHintIds"] = new JsonArray("fix.notFound"));

        var result = new NegativeFixtureRunner().Run(manifest);

        var target = result.Cases.Single(c => c.Id == "requirements-missing-evidence");
        Assert.Contains(target.Errors, error => error == "missingFixHint:fix.notFound");
    }

    [Fact]
    public void NegativeFixtureRunner_ShouldReportExpectedVsActual()
    {
        var result = new NegativeFixtureRunner().Run(NegativeManifestPath());
        var fixture = result.Cases.Single(c => c.Id == "requirements-missing-evidence");

        Assert.Contains("RequirementMissingEvidence", fixture.ExpectedCodes);
        Assert.Contains("RequirementMissingEvidence", fixture.ActualCodes);
    }

    [Fact]
    public void NegativeFixtureRunner_ShouldIncludeActualSeverity()
    {
        var result = new NegativeFixtureRunner().Run(NegativeManifestPath());

        Assert.Contains(result.Cases, c => c.Id == "requirements-low-confidence-approved" && c.ActualSeverities.Contains("warning"));
    }

    [Fact]
    public void NegativeFixtureRunner_ShouldIncludeActualFixHints()
    {
        var result = new NegativeFixtureRunner().Run(NegativeManifestPath());

        Assert.Contains(result.Cases, c => c.ActualFixHintIds.Contains("fix.templateAsset"));
    }

    [Fact]
    public void Cli_NegativeFixturesRun_ShouldWriteReport()
    {
        var output = Path.Combine(NewTempDirectory(), "negative-fixtures.json");

        var result = CliRunner.Run(RepoRoot(), "negative-fixtures", "run", "--manifest", NegativeManifestPath(), "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.True(json["passed"]!.GetValue<bool>());
    }

    [Fact]
    public void DiagnosticReportBuilder_ShouldComputeSummary()
    {
        var report = new DiagnosticReportBuilder().Build(requirements: RequirementReportWithIssue());

        Assert.Equal("fail", report.Summary.Status);
        Assert.True(report.Summary.BreakingIssues >= 1);
        Assert.Contains("requirements", report.Summary.TopCategories);
    }

    [Fact]
    public void DiagnosticIssueGrouper_ShouldGroupByCategoryAndSpecPath()
    {
        var issues = new[]
        {
            Issue("PageMarginMismatch", "pageSetup", "$.pageSetup"),
            Issue("PageSizeMismatch", "pageSetup", "$.pageSetup")
        };

        var groups = new DiagnosticIssueGrouper().Group(issues);

        Assert.Single(groups);
        Assert.Equal(2, groups[0].Count);
    }

    [Fact]
    public void DiagnosticReportMarkdownRenderer_ShouldRenderBlockingIssues()
    {
        var report = new DiagnosticReportBuilder().Build(requirements: RequirementReportWithIssue());
        var markdown = new DiagnosticReportMarkdownRenderer().Render(report);

        Assert.Contains("Errors", markdown);
        Assert.Contains("Requirement mapping issue", markdown);
    }

    [Fact]
    public void DiagnosticReportMarkdownRenderer_ShouldRenderFixHints()
    {
        var report = new DiagnosticReportBuilder().Build(requirements: RequirementReportWithIssue());
        var markdown = new DiagnosticReportMarkdownRenderer().Render(report);

        Assert.Contains("fix.requirementMapping", markdown);
    }

    [Fact]
    public void DiagnosticReportMarkdownRenderer_ShouldRenderTopActions()
    {
        var report = new DiagnosticReportBuilder().Build(requirements: RequirementReportWithIssue());
        var markdown = new DiagnosticReportMarkdownRenderer().Render(report);

        Assert.Contains("Top Actions", markdown);
    }

    [Fact]
    public void DiagnosticReportMarkdownRenderer_ShouldRenderPassSummary()
    {
        var markdown = new DiagnosticReportMarkdownRenderer().Render(new DiagnosticReport { Status = "pass" });

        Assert.Contains("No error-level diagnostic issues", markdown);
    }

    [Fact]
    public void Cli_TemplateDiagnose_ShouldWriteMarkdownReport()
    {
        var directory = NewTempDirectory();
        var json = Path.Combine(directory, "diagnose.json");
        var markdown = Path.Combine(directory, "diagnose.md");

        var result = CliRunner.Run(RepoRoot(), "template", "diagnose", "--template", TemplatePath(), "--document", DocumentPath(), "--requirements", RequirementsPath(), "--suite", SuitePath(), "--out", json, "--markdown", markdown);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Template Diagnostic Report", File.ReadAllText(markdown));
    }

    [Fact]
    public void TemplateAuthoringReportBuilder_ShouldComputeQualityScore()
    {
        var report = BuildAuthoringReport();

        Assert.InRange(report.QualityScore, 1, 100);
    }

    [Fact]
    public void TemplateAuthoringReportBuilder_ShouldSuggestApproveForPassingTemplate()
    {
        var report = BuildAuthoringReport();

        Assert.Equal("approve", report.SuggestedMergeDecision);
    }

    [Fact]
    public void TemplateAuthoringReportBuilder_ShouldSuggestRejectForBlockingIssues()
    {
        var report = BuildAuthoringReport(threshold: 1.0);

        Assert.Equal("reject", report.SuggestedMergeDecision);
    }

    [Fact]
    public void TemplateAuthoringMarkdownRenderer_ShouldRenderChecklist()
    {
        var markdown = new TemplateAuthoringMarkdownRenderer().Render(BuildAuthoringReport());

        Assert.Contains("Checklist", markdown);
        Assert.Contains("template schema valid", markdown);
    }

    [Fact]
    public void TemplateAuthoringReportBuilder_ShouldIncludePublishReadinessCoverageChecklist()
    {
        var report = BuildAuthoringReport();
        var codes = report.Checklist.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("pageTemplate.image", codes);
        Assert.Contains("formatSpec.bibliography", codes);
        Assert.Contains("document.advancedTable", codes);
        Assert.DoesNotContain(report.Checklist, item => item.Status == "fail");
        Assert.True(((Dictionary<string, bool>)report.TemplateSummary["pageTemplateElementCoverage"])["fieldTable"]);
        Assert.True(((Dictionary<string, bool>)report.CoverageSummary["documentFeatureCoverage"])["notes"]);
    }

    [Fact]
    public void TemplateAuthoringMarkdownRenderer_ShouldRenderMergeDecision()
    {
        var markdown = new TemplateAuthoringMarkdownRenderer().Render(BuildAuthoringReport());

        Assert.Contains("Merge decision", markdown);
        Assert.Contains("approve", markdown);
    }

    [Fact]
    public void Cli_TemplateAuthoringReport_ShouldWriteMarkdownReport()
    {
        var directory = NewTempDirectory();
        var json = Path.Combine(directory, "authoring.json");
        var markdown = Path.Combine(directory, "authoring.md");

        var result = CliRunner.Run(RepoRoot(), "template", "authoring-report", "--template", TemplatePath(), "--document", DocumentPath(), "--requirements", RequirementsPath(), "--suite", SuitePath(), "--threshold", "0.85", "--out", json, "--markdown", markdown);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Template Authoring Report", File.ReadAllText(markdown));
    }

    [Fact]
    public void CiQualityReportBuilder_ShouldAggregateChecks()
    {
        var report = BuildCiReport();

        Assert.Contains(report.Checks, check => check.Code == "gate");
        Assert.Contains(report.Checks, check => check.Code == "negativeFixtures");
    }

    [Fact]
    public void CiQualityReportBuilder_ShouldFailWhenNegativeFixturesFail()
    {
        var manifest = CreateMutatedNegativeManifest(node => node["cases"]![0]!["expectedCodes"] = new JsonArray("WrongCode"));
        var report = BuildCiReport(negativeFixtures: manifest);

        Assert.Equal("fail", report.Status);
        Assert.Equal("reject", report.MergeDecision);
    }

    [Fact]
    public void CiQualityReportBuilder_ShouldIncludeArtifacts()
    {
        var report = BuildCiReport();

        Assert.Contains("gateReport", report.Artifacts.Keys);
        Assert.Contains("negativeFixturesReport", report.Artifacts.Keys);
        Assert.All(report.Artifacts.Values, artifact => Assert.False(Path.IsPathRooted(artifact)));
    }

    [Fact]
    public void CiQualityMarkdownRenderer_ShouldRenderMergeDecision()
    {
        var markdown = new CiQualityMarkdownRenderer().Render(BuildCiReport());

        Assert.Contains("Merge decision", markdown);
        Assert.Contains("approve", markdown);
    }

    [Fact]
    public void Cli_CiQualityReport_ShouldWriteJsonAndMarkdown()
    {
        var directory = NewTempDirectory();
        var json = Path.Combine(directory, "quality.json");
        var markdown = Path.Combine(directory, "quality.md");

        var result = CliRunner.Run(RepoRoot(), "ci", "quality-report", "--template", TemplatePath(), "--document", DocumentPath(), "--requirements", RequirementsPath(), "--suite", SuitePath(), "--negative-fixtures", NegativeManifestPath(), "--threshold", "0.85", "--out", json, "--markdown", markdown);
        var node = JsonNode.Parse(File.ReadAllText(json))!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("pass", node["status"]!.GetValue<string>());
        Assert.Contains("CI Template Quality Report", File.ReadAllText(markdown));
    }

    [Fact]
    public void Cli_CiQualityReport_ShouldReturnNonZeroOnFailure()
    {
        var manifest = CreateMutatedNegativeManifest(node => node["cases"]![0]!["expectedCodes"] = new JsonArray("WrongCode"));
        var directory = NewTempDirectory();

        var result = CliRunner.Run(RepoRoot(), "ci", "quality-report", "--template", TemplatePath(), "--document", DocumentPath(), "--requirements", RequirementsPath(), "--suite", SuitePath(), "--negative-fixtures", manifest, "--threshold", "0.85", "--out", Path.Combine(directory, "quality.json"), "--markdown", Path.Combine(directory, "quality.md"));

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void DiagnosticReportSchema_ShouldValidateGeneratedReport()
    {
        var directory = NewTempDirectory();
        var reportPath = Path.Combine(directory, "diagnose.json");
        var report = new DiagnosticReportBuilder().Build(requirements: RequirementReportWithIssue());
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, ThesisJson.Options));

        var result = new ThesisSchemaValidator().ValidateDiagnosticReportFile(reportPath, SchemaPath("diagnostic-report.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void FixHintRulesSchema_ShouldRejectRuleWithoutDocsOrFixture()
    {
        var path = Path.Combine(NewTempDirectory(), "rules.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new FixHintRuleCatalog
        {
            Rules =
            [
                new FixHintRule
                {
                    HintId = "fix.bad",
                    Match = new FixHintRuleMatch { Code = "Bad" },
                    Title = "Bad",
                    Description = "Bad",
                    SuggestedAction = "Bad",
                    Confidence = "high"
                }
            ]
        }, ThesisJson.Options));

        var result = new ThesisSchemaValidator().ValidateFixHintRulesFile(path, SchemaPath("fix-hint-rules.schema.json"));

        Assert.False(result.IsValid);
    }

    private static DiagnosticIssue Issue(string id, string category, string? path = null)
    {
        return new DiagnosticIssue
        {
            Id = id,
            Source = "Test",
            Category = category,
            Severity = DiagnosticSeverity.Error,
            Title = id,
            Message = id,
            SpecPath = path
        };
    }

    private static ThesisDocx.Core.Requirements.RequirementMappingReport RequirementReportWithIssue()
    {
        return new ThesisDocx.Core.Requirements.RequirementMappingReport
        {
            IsValid = false,
            Errors =
            [
                new ThesisDocx.Core.Models.Requirements.RequirementCaptureValidationIssue
                {
                    Code = "RequirementUnmappedApproved",
                    Path = "$.mappings",
                    Message = "unmapped approved requirement"
                }
            ],
            UnmappedRequirements = 1
        };
    }

    private static TemplateAuthoringReport BuildAuthoringReport(double threshold = 0.85)
    {
        return new TemplateAuthoringReportBuilder().Build(new TemplateAuthoringReportOptions
        {
            TemplatePath = TemplatePath(),
            DocumentPath = DocumentPath(),
            RequirementsPath = RequirementsPath(),
            SuitePath = SuitePath(),
            OutputDirectory = NewTempDirectory(),
            CoverageThreshold = threshold
        });
    }

    private static CiQualityReport BuildCiReport(string? negativeFixtures = null)
    {
        return new CiQualityReportBuilder().Build(new CiQualityReportOptions
        {
            TemplatePath = TemplatePath(),
            DocumentPath = DocumentPath(),
            RequirementsPath = RequirementsPath(),
            SuitePath = SuitePath(),
            NegativeFixturesPath = negativeFixtures ?? NegativeManifestPath(),
            OutputDirectory = NewTempDirectory(),
            Threshold = 0.85
        });
    }

    private static string CreateMutatedNegativeManifest(Action<JsonNode> mutate)
    {
        var node = JsonNode.Parse(File.ReadAllText(NegativeManifestPath()))!;
        mutate(node);
        var path = Path.Combine(NewTempDirectory(), "negative-fixture-manifest.json");
        File.WriteAllText(path, node.ToJsonString(ThesisJson.Options));
        return path;
    }

    private static IEnumerable<string> CiScripts()
    {
        return [
            "ci-quality-gate",
            "ci-render-examples",
            "ci-template-quality",
            "ci-schema-check",
            "ci-negative-fixtures",
            "generate-schema-docs",
            "generate-web-types",
            "schema-codegen.mjs"
        ];
    }

    private static string NegativeManifestPath() => Path.Combine(RepoRoot(), "examples", "negative-fixtures", "negative-fixture-manifest.json");

    private static string FixHintRulesPath() => Path.Combine(RepoRoot(), "resources", "fix-hint-rules.json");

    private static string RequirementsPath() => Path.Combine(RepoRoot(), "examples", "requirements", "example-engineering-requirements.json");

    private static string SuitePath() => Path.Combine(RepoRoot(), "examples", "template-regression", "template-regression-suite.json");

    private static string TemplatePath() => Path.Combine(RepoRoot(), "examples", "templates", "example-university-engineering");

    private static string DocumentPath() => Path.Combine(RepoRoot(), "examples", "full-thesis", "document.json");

    private static string SchemaPath(string name) => Path.Combine(RepoRoot(), "schemas", name);

    private static string RepoRoot() => TestRenderHelper.LocateRepoRootForTests();

    private static string NewTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
