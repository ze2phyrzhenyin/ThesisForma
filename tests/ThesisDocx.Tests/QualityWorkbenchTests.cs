using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Requirements;
using ThesisDocx.Core.Templates.Authoring;
using ThesisDocx.Core.Templates.Baselines;
using ThesisDocx.Core.Templates.Gate;
using ThesisDocx.Core.Templates.Regression;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class QualityWorkbenchTests
{
    [Fact]
    public void Schema_ShouldValidateRequirementCapture()
    {
        var result = new ThesisSchemaValidator().ValidateRequirementCaptureFile(RequirementsPath(), SchemaPath("requirement-capture.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldRejectRequirementCaptureWithAbsoluteSourcePath()
    {
        var result = new ThesisSchemaValidator().ValidateRequirementCaptureFile(InvalidRequirementsPath(), SchemaPath("requirement-capture.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectRequirementCaptureWithAbsolutePath()
    {
        var mutated = CreateMutatedRequirements(node => node["sourceDocuments"]![0]!["path"] = "/tmp/manual.pdf");

        var result = new ThesisSchemaValidator().ValidateRequirementCaptureFile(mutated, SchemaPath("requirement-capture.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectUnknownRequirementCategory()
    {
        var mutated = CreateMutatedRequirements(node => node["requirements"]![0]!["category"] = "unknownCategory");

        var result = new ThesisSchemaValidator().ValidateRequirementCaptureFile(mutated, SchemaPath("requirement-capture.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectInvalidReviewStatus()
    {
        var mutated = CreateMutatedRequirements(node => node["requirements"]![0]!["reviewStatus"] = "needsMagic");

        var result = new ThesisSchemaValidator().ValidateRequirementCaptureFile(mutated, SchemaPath("requirement-capture.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RequirementCaptureValidator_ShouldReturnValidForExample()
    {
        var result = new RequirementCaptureValidator().Validate(LoadRequirements());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void RequirementCaptureValidator_ShouldDetectUnmappedApprovedRequirement()
    {
        var capture = LoadRequirements();
        capture.Mappings.Single(m => m.RequirementId == "req-heading-style").MappingStatus = RequirementMappingStatus.Unmapped;

        var result = new RequirementCaptureValidator().Validate(capture);

        Assert.Contains(result.Errors, error => error.Code == "requirements.item.approvedUnmapped");
    }

    [Fact]
    public void RequirementCaptureValidator_ShouldDetectMissingEvidence()
    {
        var capture = LoadRequirements();
        capture.Requirements.Single(r => r.Id == "req-page-a4-margins").Evidence = new RequirementEvidence();

        var result = new RequirementCaptureValidator().Validate(capture);

        Assert.Contains(result.Errors, error => error.Code == "requirements.item.evidenceMissing");
    }

    [Fact]
    public void RequirementCaptureValidator_ShouldWarnLowConfidenceApprovedRequirement()
    {
        var capture = LoadRequirements();
        capture.Requirements.Single(r => r.Id == "req-page-a4-margins").Confidence = RequirementConfidence.Low;

        var result = new RequirementCaptureValidator().Validate(capture);

        Assert.Contains(result.Warnings, warning => warning.Code == "requirements.item.lowConfidenceApproved");
    }

    [Fact]
    public void RequirementCaptureValidator_ShouldRequireNotesForNotSupportedRequirement()
    {
        var capture = LoadRequirements();
        capture.Mappings.Single(m => m.RequirementId == "req-legacy-watermark").Notes = string.Empty;

        var result = new RequirementCaptureValidator().Validate(capture);

        Assert.Contains(result.Errors, error => error.Code == "requirements.mapping.notSupportedNotesMissing");
    }

    [Fact]
    public void RequirementMappingReporter_ShouldGroupCoverageByCategory()
    {
        var report = new RequirementMappingReporter().Build(LoadRequirements(), TemplatePath());

        Assert.Contains(report.CoverageByCategory, category => category.Category == "PageSetup" && category.Mapped == 1);
        Assert.Contains(report.CoverageByCategory, category => category.Category == "Table" && category.Partial == 1);
    }

    [Fact]
    public void Cli_RequirementsValidate_ShouldReturnValidForExample()
    {
        var result = CliRunner.Run(RepoRoot(), "requirements", "validate", "--requirements", RequirementsPath());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Requirements valid", result.StandardOutput);
    }

    [Fact]
    public void Cli_RequirementsValidate_ShouldReturnNonZeroForInvalidExample()
    {
        var result = CliRunner.Run(RepoRoot(), "requirements", "validate", "--requirements", InvalidRequirementsPath());

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("requirements.item.evidenceMissing", result.StandardError);
    }

    [Fact]
    public void Cli_RequirementsReport_ShouldWriteJson()
    {
        var output = Path.Combine(NewTempDirectory(), "requirements-report.json");

        var result = CliRunner.Run(RepoRoot(), "requirements", "report", "--requirements", RequirementsPath(), "--template", TemplatePath(), "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.True(json["isValid"]!.GetValue<bool>());
    }

    [Fact]
    public void Cli_RequirementsReport_ShouldIncludeCoverageByCategory()
    {
        var output = Path.Combine(NewTempDirectory(), "requirements-report.json");

        CliRunner.Run(RepoRoot(), "requirements", "report", "--requirements", RequirementsPath(), "--template", TemplatePath(), "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.True(json["coverageByCategory"]!.AsArray().Count > 0);
    }

    [Fact]
    public void Cli_RequirementsReport_ShouldIncludeRecommendedActions()
    {
        var output = Path.Combine(NewTempDirectory(), "requirements-report.json");

        CliRunner.Run(RepoRoot(), "requirements", "report", "--requirements", RequirementsPath(), "--template", TemplatePath(), "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.True(json["recommendedNextActions"]!.AsArray().Count > 0);
    }

    [Fact]
    public void Schema_ShouldValidateTemplateBaselineManifest()
    {
        var result = new ThesisSchemaValidator().ValidateTemplateBaselineManifestFile(BaselineManifestPath(), SchemaPath("template-baseline-manifest.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldRejectBaselineManifestWithAbsolutePath()
    {
        var mutated = CreateMutatedBaselineManifest(node => node["baselines"]![0]!["snapshotPath"] = "/tmp/bad.snapshot.txt");

        var result = new ThesisSchemaValidator().ValidateTemplateBaselineManifestFile(mutated, SchemaPath("template-baseline-manifest.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectTemplateBaselineManifestWithInvalidThreshold()
    {
        var mutated = CreateMutatedBaselineManifest(node => node["baselines"]![0]!["layoutThreshold"] = 1.2);

        var result = new ThesisSchemaValidator().ValidateTemplateBaselineManifestFile(mutated, SchemaPath("template-baseline-manifest.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void BaselineManager_ShouldListBaselines()
    {
        var entries = new TemplateBaselineManager().List(SuitePath());

        Assert.Contains(entries, entry => entry.CaseId == "example-university-engineering-full");
    }

    [Fact]
    public void BaselineManager_ShouldInitializeMissingBaselines()
    {
        var manifest = Path.Combine(NewTempDirectory(), "baseline-manifest.json");

        var result = new TemplateBaselineManager().Init(SuitePath(), manifest);

        Assert.Contains(result.Baselines, entry => entry.CaseId == "example-university-engineering-full");
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(manifest)!, result.Baselines.Single().SnapshotPath)));
    }

    [Fact]
    public void BaselineManager_ShouldCompareAgainstBaselines()
    {
        var result = new TemplateBaselineManager().CompareSuite(SuitePath(), NewTempDirectory());

        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Cases.SelectMany(c => c.Errors)));
    }

    [Fact]
    public void BaselineManager_ShouldRequireReasonForUpdate()
    {
        var result = new TemplateBaselineManager().Update(SuitePath(), "example-university-engineering-full", string.Empty, NewTempDirectory());

        Assert.False(result.Updated);
        Assert.Contains("baseline.update.reasonRequired", result.Errors);
    }

    [Fact]
    public void BaselineManager_ShouldProduceDeterministicCompareReport()
    {
        var first = new TemplateBaselineManager().CompareSuite(SuitePath(), NewTempDirectory()).Cases.Select(c => c.CaseId).ToList();
        var second = new TemplateBaselineManager().CompareSuite(SuitePath(), NewTempDirectory()).Cases.Select(c => c.CaseId).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Cli_BaselineList_ShouldPrintBaselines()
    {
        var result = CliRunner.Run(RepoRoot(), "baseline", "list", "--suite", SuitePath());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("example-university-engineering-full", result.StandardOutput);
    }

    [Fact]
    public void Cli_BaselineInit_ShouldCreateManifest()
    {
        var output = Path.Combine(NewTempDirectory(), "baseline-manifest.json");

        var result = CliRunner.Run(RepoRoot(), "baseline", "init", "--suite", SuitePath(), "--out", output);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public void Cli_BaselineCompare_ShouldWriteJson()
    {
        var output = Path.Combine(NewTempDirectory(), "baseline-compare.json");

        var result = CliRunner.Run(RepoRoot(), "baseline", "compare", "--suite", SuitePath(), "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.True(json["passed"]!.GetValue<bool>());
    }

    [Fact]
    public void Cli_BaselineUpdate_ShouldRequireReason()
    {
        var output = Path.Combine(NewTempDirectory(), "baseline-update.json");

        var result = CliRunner.Run(RepoRoot(), "baseline", "update", "--suite", SuitePath(), "--case", "example-university-engineering-full", "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("baseline.update.reasonRequired", json["errors"]!.AsArray().Select(node => node!.GetValue<string>()));
    }

    [Fact]
    public void FixHintEngine_ShouldSuggestSpecPathForMarginMismatch()
    {
        var hints = new FixHintEngine().Suggest(Issue("pageSetup", "page margin mismatch"));

        Assert.Contains(hints, hint => hint.SuggestedSpecPath == "$.pageSetup");
    }

    [Fact]
    public void FixHintEngine_ShouldSuggestHeadingPathForHeadingMismatch()
    {
        var hints = new FixHintEngine().Suggest(Issue("heading", "heading style mismatch"));

        Assert.Contains(hints, hint => hint.SuggestedSpecPath == "$.headings");
    }

    [Fact]
    public void FixHintEngine_ShouldSuggestTablePathForBorderMismatch()
    {
        var hints = new FixHintEngine().Suggest(Issue("table", "table border mismatch"));

        Assert.Contains(hints, hint => hint.SuggestedSpecPath == "$.tables");
    }

    [Fact]
    public void FixHintEngine_ShouldSuggestTocPathForMissingToc()
    {
        var hints = new FixHintEngine().Suggest(Issue("toc", "missing TOC field"));

        Assert.Contains(hints, hint => hint.SuggestedSpecPath == "$.toc");
    }

    [Fact]
    public void FixHintEngine_ShouldSuggestRequirementActionForUnmappedRequirement()
    {
        var hints = new FixHintEngine().Suggest(Issue("requirements", "unmapped approved requirement"));

        Assert.Contains(hints, hint => hint.HintId == "fix.requirementMapping");
    }

    [Fact]
    public void DiagnosticReportBuilder_ShouldMergeGateAndRegressionIssues()
    {
        var gate = FailedGateReport();
        var regression = new TemplateRegressionResult
        {
            Cases = [new TemplateRegressionCaseResult { Id = "case-a", Errors = ["snapshot.mismatch"] }]
        };

        var report = new DiagnosticReportBuilder().Build(gate, regression);

        Assert.Contains(report.Issues, issue => issue.Source == "TemplateGate");
        Assert.Contains(report.Issues, issue => issue.Source == "TemplateRegression");
    }

    [Fact]
    public void DiagnosticReportBuilder_ShouldAttachRelatedFixtures()
    {
        var report = new DiagnosticReportBuilder().Build(requirements: new RequirementMappingReport
        {
            IsValid = false,
            Errors = [new RequirementCaptureValidationIssue { Code = "requirements.item.approvedUnmapped", Path = "$.requirements[0]", Message = "unmapped approved requirement" }],
            UnmappedRequirements = 1
        });

        Assert.Contains(report.Issues.SelectMany(issue => issue.RelatedFixtures), fixture => fixture == "examples/requirements");
    }

    [Fact]
    public void DiagnosticReportBuilder_ShouldProduceDeterministicIssueOrder()
    {
        var first = new DiagnosticReportBuilder().Build(FailedGateReport()).Issues.Select(issue => issue.Id).ToList();
        var second = new DiagnosticReportBuilder().Build(FailedGateReport()).Issues.Select(issue => issue.Id).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Cli_TemplateDiagnose_ShouldWriteDiagnosticReport()
    {
        var output = Path.Combine(NewTempDirectory(), "diagnose.json");

        var result = CliRunner.Run(RepoRoot(), "template", "diagnose", "--template", TemplatePath(), "--document", DocumentPath(), "--requirements", RequirementsPath(), "--suite", SuitePath(), "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("pass", json["status"]!.GetValue<string>());
    }

    [Fact]
    public void Cli_TemplateDiagnose_ShouldIncludeFixHints()
    {
        var output = Path.Combine(NewTempDirectory(), "diagnose.json");

        CliRunner.Run(RepoRoot(), "template", "diagnose", "--template", TemplatePath(), "--document", DocumentPath(), "--requirements", InvalidRequirementsPath(), "--suite", SuitePath(), "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Contains(json["issues"]!.AsArray(), issue => issue!["fixHints"]!.AsArray().Count > 0);
    }

    [Fact]
    public void Cli_TemplateDiagnose_ShouldIncludeRelatedArtifacts()
    {
        var output = Path.Combine(NewTempDirectory(), "diagnose.json");

        CliRunner.Run(RepoRoot(), "template", "diagnose", "--template", TemplatePath(), "--document", DocumentPath(), "--requirements", RequirementsPath(), "--suite", SuitePath(), "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.True(json["relatedArtifacts"]!.AsObject().ContainsKey("docx"));
    }

    [Fact]
    public void Cli_TemplateDiagnose_ShouldReturnNonZeroWhenBreakingIssues()
    {
        var output = Path.Combine(NewTempDirectory(), "diagnose.json");

        var result = CliRunner.Run(RepoRoot(), "template", "diagnose", "--template", TemplatePath(), "--document", DocumentPath(), "--requirements", InvalidRequirementsPath(), "--suite", SuitePath(), "--out", output);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void Cli_TemplateDiagnose_ShouldProduceParseableJson()
    {
        var output = Path.Combine(NewTempDirectory(), "diagnose.json");

        CliRunner.Run(RepoRoot(), "template", "diagnose", "--template", TemplatePath(), "--document", DocumentPath(), "--requirements", RequirementsPath(), "--suite", SuitePath(), "--out", output);

        Assert.NotNull(JsonNode.Parse(File.ReadAllText(output)));
    }

    [Fact]
    public void TemplateAuthoringReportBuilder_ShouldBuildReportForExampleTemplate()
    {
        var report = BuildAuthoringReport();

        Assert.Equal("example-university-engineering", report.TemplateId);
        Assert.True(report.Checklist.Count >= 15);
    }

    [Fact]
    public void TemplateAuthoringReportBuilder_ShouldMarkReadyForPassingTemplate()
    {
        var report = BuildAuthoringReport();

        Assert.Equal("ready", report.PublishReadiness);
    }

    [Fact]
    public void TemplateAuthoringReportBuilder_ShouldMarkNotReadyForBlockingIssues()
    {
        var report = BuildAuthoringReport(threshold: 1.0);

        Assert.Equal("notReady", report.PublishReadiness);
    }

    [Fact]
    public void TemplateAuthoringReportBuilder_ShouldIncludeChecklist()
    {
        var report = BuildAuthoringReport();

        Assert.Contains(report.Checklist, item => item.Code == "gate.passed");
    }

    [Fact]
    public void TemplateAuthoringReportBuilder_ShouldIncludeRequirementSummary()
    {
        var report = BuildAuthoringReport();

        Assert.Equal(6, Convert.ToInt32(report.RequirementMappingSummary["totalRequirements"]));
    }

    [Fact]
    public void Cli_TemplateAuthoringReport_ShouldWriteJson()
    {
        var output = Path.Combine(NewTempDirectory(), "authoring.json");

        var result = CliRunner.Run(RepoRoot(), "template", "authoring-report", "--template", TemplatePath(), "--document", DocumentPath(), "--requirements", RequirementsPath(), "--suite", SuitePath(), "--threshold", "0.85", "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("ready", json["publishReadiness"]!.GetValue<string>());
    }

    [Fact]
    public void TemplateGate_ShouldIncludeDiagnosticsAndFixHints()
    {
        var report = RunGate(threshold: 1.0);

        Assert.NotEmpty(report.Diagnostics);
        Assert.NotEmpty(report.FixHints);
    }

    [Fact]
    public void TemplateGate_ShouldIncludeNextActions()
    {
        var report = RunGate(threshold: 1.0);

        Assert.NotEmpty(report.NextActions);
    }

    [Fact]
    public void TemplateRegression_ShouldIncludeCaseDiagnostics()
    {
        var suite = CreateMutatedSuite(node =>
        {
            node["cases"]![0]!["baselineLayoutPath"] = "bad-baseline.layout.json";
            node["cases"]![0]!["minLayoutSimilarity"] = 1.0;
        });
        var badBaseline = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "examples", "template-regression", "baselines", "example-university-engineering-full.layout.json")))!;
        badBaseline["sections"]![0]!["topMarginTwips"] = "1";
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(suite)!, "bad-baseline.layout.json"), badBaseline.ToJsonString(ThesisJson.Options));

        var result = new TemplateRegressionRunner().Run(suite, NewTempDirectory());

        Assert.NotEmpty(result.CaseDiagnostics);
    }

    [Fact]
    public void TemplateRegression_ShouldIncludeBaselineSummary()
    {
        var result = new TemplateRegressionRunner().Run(SuitePath(), NewTempDirectory());

        Assert.Equal(1, result.BaselineSummary.TotalCases);
        Assert.Equal(1, result.BaselineSummary.SnapshotMatches);
    }

    [Fact]
    public void FormatFixtureBaselines_ShouldHaveEntriesForAllFixtures()
    {
        var manifest = ReadJson<TemplateBaselineManifest>(FixtureBaselineManifestPath());

        Assert.Equal(7, manifest.Baselines.Count);
        Assert.Contains(manifest.Baselines, entry => entry.CaseId == "table-edge-cases");
    }

    [Fact]
    public void BaselineCompare_ShouldRunForFormatFixtures()
    {
        var result = new TemplateBaselineManager().CompareFixtures(FormatFixturesPath(), NewTempDirectory());

        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Cases.SelectMany(c => c.Errors)));
    }

    [Fact]
    public void BaselineCompare_ShouldReportFixtureIdOnFailure()
    {
        var fixtures = CopyDirectory(FormatFixturesPath(), NewTempDirectory());
        var snapshot = Path.Combine(fixtures, "baselines", "table-edge-cases.snapshot.txt");
        File.AppendAllText(snapshot, "intentional mismatch");

        var result = new TemplateBaselineManager().CompareFixtures(fixtures, NewTempDirectory());

        Assert.Contains(result.Cases, c => c.FixtureId == "table-edge-cases" && !c.Passed);
    }

    [Fact]
    public void Schema_ShouldValidateAllQualityWorkflowExamples()
    {
        var schema = new ThesisSchemaValidator();
        var requirements = schema.ValidateRequirementCaptureFile(RequirementsPath(), SchemaPath("requirement-capture.schema.json"));
        var baseline = schema.ValidateTemplateBaselineManifestFile(BaselineManifestPath(), SchemaPath("template-baseline-manifest.schema.json"));
        var fixtureBaseline = schema.ValidateTemplateBaselineManifestFile(FixtureBaselineManifestPath(), SchemaPath("template-baseline-manifest.schema.json"));

        Assert.True(requirements.IsValid, string.Join(Environment.NewLine, requirements.Errors));
        Assert.True(baseline.IsValid, string.Join(Environment.NewLine, baseline.Errors));
        Assert.True(fixtureBaseline.IsValid, string.Join(Environment.NewLine, fixtureBaseline.Errors));
    }

    private static DiagnosticIssue Issue(string category, string message)
    {
        return new DiagnosticIssue { Id = message, Category = category, Message = message, Severity = DiagnosticSeverity.Error };
    }

    private static TemplateGateReport FailedGateReport()
    {
        return new TemplateGateReport
        {
            Checks =
            [
                new TemplateGateCheck { Code = "coverage", Name = "Coverage threshold", Status = TemplateGateCheckStatus.Fail, Message = "coverage below threshold" },
                new TemplateGateCheck { Code = "template.validate", Name = "Template validation", Status = TemplateGateCheckStatus.Pass, Message = "passed" }
            ],
            Artifacts = new Dictionary<string, string>(StringComparer.Ordinal) { ["docx"] = "out/template.docx" }
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

    private static TemplateGateReport RunGate(double threshold = 0.75)
    {
        return new TemplateGateService().Run(new TemplateGateOptions
        {
            TemplatePath = TemplatePath(),
            DocumentPath = DocumentPath(),
            OutputDirectory = NewTempDirectory(),
            CoverageThreshold = threshold
        });
    }

    private static string CreateMutatedRequirements(Action<JsonNode> mutate)
    {
        var node = JsonNode.Parse(File.ReadAllText(RequirementsPath()))!;
        mutate(node);
        var path = Path.Combine(NewTempDirectory(), "requirements.json");
        File.WriteAllText(path, node.ToJsonString(ThesisJson.Options));
        return path;
    }

    private static string CreateMutatedBaselineManifest(Action<JsonNode> mutate)
    {
        var node = JsonNode.Parse(File.ReadAllText(BaselineManifestPath()))!;
        mutate(node);
        var path = Path.Combine(NewTempDirectory(), "baseline-manifest.json");
        File.WriteAllText(path, node.ToJsonString(ThesisJson.Options));
        return path;
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

    private static string CopyDirectory(string sourceDirectory, string targetParent)
    {
        var targetDirectory = Path.Combine(targetParent, Path.GetFileName(sourceDirectory));
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        return targetDirectory;
    }

    private static RequirementCapture LoadRequirements() => new RequirementCaptureLoader().Load(RequirementsPath());

    private static T ReadJson<T>(string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize {path}.");
    }

    private static string RequirementsPath() => Path.Combine(RepoRoot(), "examples", "requirements", "example-engineering-requirements.json");

    private static string InvalidRequirementsPath() => Path.Combine(RepoRoot(), "examples", "requirements", "example-engineering-requirements-invalid.json");

    private static string SuitePath() => Path.Combine(RepoRoot(), "examples", "template-regression", "template-regression-suite.json");

    private static string BaselineManifestPath() => Path.Combine(RepoRoot(), "examples", "template-regression", "baselines", "baseline-manifest.json");

    private static string FixtureBaselineManifestPath() => Path.Combine(RepoRoot(), "examples", "format-fixtures", "baselines", "format-fixture-baseline-manifest.json");

    private static string FormatFixturesPath() => Path.Combine(RepoRoot(), "examples", "format-fixtures");

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
