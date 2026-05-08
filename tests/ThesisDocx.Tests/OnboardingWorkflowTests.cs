using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Onboarding;
using ThesisDocx.Core.Onboarding.Packaging;
using ThesisDocx.Core.Onboarding.Reports;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Privacy;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class OnboardingWorkflowTests
{
    [Fact]
    public void Schema_ShouldValidateOnboardingWorkspace()
    {
        var result = new ThesisSchemaValidator().ValidateOnboardingWorkspaceFile(OnboardingManifestPath(), SchemaPath("onboarding-workspace.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldRejectOnboardingWorkspaceWithPathTraversal()
    {
        var path = MutateOnboarding(node => node["paths"]!["templateDir"] = "../templates/private");

        var result = new ThesisSchemaValidator().ValidateOnboardingWorkspaceFile(path, SchemaPath("onboarding-workspace.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectOnboardingWorkspaceWithAbsolutePath()
    {
        var path = MutateOnboarding(node => node["paths"]!["sourceDocumentsDir"] = "/tmp/source-documents");

        var result = new ThesisSchemaValidator().ValidateOnboardingWorkspaceFile(path, SchemaPath("onboarding-workspace.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldValidateTemplatePilotPackageManifest()
    {
        var package = BuildPackage();
        var manifest = ExtractPackageEntry(package, "manifest.json");
        var manifestPath = Path.Combine(NewTempDirectory(), "manifest.json");
        File.WriteAllText(manifestPath, manifest);

        var result = new ThesisSchemaValidator().ValidateTemplatePilotPackageManifestFile(manifestPath, SchemaPath("template-pilot-package-manifest.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldValidateAllOnboardingExamples()
    {
        foreach (var manifest in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "examples", "onboarding"), "onboarding.json", SearchOption.AllDirectories))
        {
            var result = new ThesisSchemaValidator().ValidateOnboardingWorkspaceFile(manifest, SchemaPath("onboarding-workspace.schema.json"));
            Assert.True(result.IsValid, manifest + Environment.NewLine + string.Join(Environment.NewLine, result.Errors));
        }
    }

    [Fact]
    public void OnboardingWorkspaceInitializer_ShouldCreateExpectedDirectories()
    {
        var workspace = NewWorkspacePath();

        new OnboardingWorkspaceInitializer().Initialize(InitOptions(workspace));

        Assert.True(Directory.Exists(Path.Combine(workspace, "source-documents")));
        Assert.True(Directory.Exists(Path.Combine(workspace, "requirements")));
        Assert.True(Directory.Exists(Path.Combine(workspace, "template")));
        Assert.True(Directory.Exists(Path.Combine(workspace, "fixtures")));
        Assert.True(File.Exists(Path.Combine(workspace, ".gitignore")));
    }

    [Fact]
    public void OnboardingWorkspaceValidator_ShouldValidateExampleWorkspace()
    {
        var result = new OnboardingWorkspaceValidator().Validate(OnboardingWorkspacePath());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void OnboardingWorkspaceValidator_ShouldRejectMissingRequiredPaths()
    {
        var workspace = CopyExampleWorkspaceUnderExamples();
        Directory.Delete(Path.Combine(workspace, "template"), recursive: true);

        var result = new OnboardingWorkspaceValidator().Validate(workspace);

        Assert.Contains(result.Errors, error => error.Code == "onboarding.path.templateDirMissing");
    }

    [Fact]
    public void OnboardingWorkspaceInspector_ShouldReportWorkspaceStatus()
    {
        var inspection = new OnboardingWorkspaceInspector().Inspect(OnboardingWorkspacePath());

        Assert.True(inspection.IsValid);
        Assert.Equal("example-engineering-pilot", inspection.WorkspaceId);
        Assert.Equal("ReadyForReview", inspection.Phase);
        Assert.Contains("template", inspection.Paths.Keys);
    }

    [Fact]
    public void Cli_OnboardingInit_ShouldCreateWorkspace()
    {
        var workspace = NewWorkspacePath();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "init", "--workspace", workspace, "--school", "Example University", "--college", "Example Engineering College", "--degree-type", "master", "--locale", "zh-CN");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(workspace, "onboarding.json")));
        Assert.Contains("example-engineering-college", File.ReadAllText(Path.Combine(workspace, "onboarding.json")));
    }

    [Fact]
    public void Cli_OnboardingInspect_ShouldWriteJson()
    {
        var output = Path.Combine(NewTempDirectory(), "inspect.json");

        var result = CliRunner.Run(RepoRoot(), "onboarding", "inspect", "--workspace", OnboardingWorkspacePath(), "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("example-engineering-pilot", json["workspaceId"]!.GetValue<string>());
    }

    [Fact]
    public void Cli_OnboardingValidate_ShouldReturnValidForExample()
    {
        var result = CliRunner.Run(RepoRoot(), "onboarding", "validate", "--workspace", OnboardingWorkspacePath());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("valid", result.StandardOutput);
    }

    [Fact]
    public void Cli_OnboardingScaffoldRequirements_ShouldCreateDraft()
    {
        var workspace = InitializedWorkspace();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "scaffold-requirements", "--workspace", workspace);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("requirement capture draft", File.ReadAllText(Path.Combine(workspace, "requirements", "requirements.json")));
    }

    [Fact]
    public void Cli_OnboardingScaffoldTemplate_ShouldCreateTemplate()
    {
        var workspace = InitializedWorkspace();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "scaffold-template", "--workspace", workspace, "--base-template", TemplatePath());

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(workspace, "template", "template.json")));
    }

    [Fact]
    public void Cli_OnboardingScaffoldFixtures_ShouldCreateFixtures()
    {
        var workspace = InitializedWorkspace();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "scaffold-fixtures", "--workspace", workspace, "--document", DocumentPath());

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.ReadAllText(Path.Combine(workspace, "fixtures", "document.json")).Contains("schemaVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void Cli_OnboardingBaselineInit_ShouldCreateBaselines()
    {
        var workspace = InitializedWorkspace();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "baseline-init", "--workspace", workspace, "--reason", "Initial pilot baseline");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Initial pilot baseline", File.ReadAllText(Path.Combine(workspace, "baselines", "baseline-manifest.json")));
    }

    [Fact]
    public void Cli_OnboardingRunGate_ShouldWriteReport()
    {
        var output = Path.Combine(NewTempDirectory(), "gate.json");
        var workspace = CopyExampleWorkspace();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "run-gate", "--workspace", workspace, "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("1.0.0", json["reportVersion"]!.GetValue<string>());
        Assert.Equal("pass", json["status"]!.GetValue<string>());
        Assert.Contains(json["versionReport"]!["checks"]!.AsArray(), check => check!["kind"]!.GetValue<string>() == "templatePackage");
    }

    [Fact]
    public void Cli_OnboardingDiagnose_ShouldWriteJsonAndMarkdown()
    {
        var dir = NewTempDirectory();
        var jsonPath = Path.Combine(dir, "diagnostic.json");
        var markdownPath = Path.Combine(dir, "diagnostic.md");
        var workspace = CopyExampleWorkspace();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "diagnose", "--workspace", workspace, "--out", jsonPath, "--markdown", markdownPath);

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(File.ReadAllText(jsonPath))!;
        Assert.Equal("1.0.0", json["reportVersion"]!.GetValue<string>());
        Assert.Equal("pass", json["status"]!.GetValue<string>());
        Assert.Contains(json["versionReport"]!["checks"]!.AsArray(), check => check!["kind"]!.GetValue<string>() == "thesisDocument");
        Assert.Contains("Diagnostic", File.ReadAllText(markdownPath));
    }

    [Fact]
    public void Cli_OnboardingAuthoringReport_ShouldWriteJsonAndMarkdown()
    {
        var dir = NewTempDirectory();
        var jsonPath = Path.Combine(dir, "authoring.json");
        var markdownPath = Path.Combine(dir, "authoring.md");
        var workspace = CopyExampleWorkspace();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "authoring-report", "--workspace", workspace, "--out", jsonPath, "--markdown", markdownPath);

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(File.ReadAllText(jsonPath))!;
        Assert.Equal("1.0.0", json["reportVersion"]!.GetValue<string>());
        Assert.Equal("ready", json["publishReadiness"]!.GetValue<string>());
        Assert.Contains(json["versionReport"]!["checks"]!.AsArray(), check => check!["kind"]!.GetValue<string>() == "thesisFormatSpec");
        Assert.Contains("merge decision", File.ReadAllText(markdownPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cli_OnboardingSummary_ShouldWriteJsonAndMarkdown()
    {
        var dir = NewTempDirectory();
        var jsonPath = Path.Combine(dir, "summary.json");
        var markdownPath = Path.Combine(dir, "summary.md");
        var workspace = CopyExampleWorkspace();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "summary", "--workspace", workspace, "--out", jsonPath, "--markdown", markdownPath);

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(File.ReadAllText(jsonPath))!;
        Assert.Equal("1.0.0", json["reportVersion"]!.GetValue<string>());
        Assert.Equal("readyForTemplateLibrary", json["releaseReadiness"]!.GetValue<string>());
        Assert.Contains(json["versionReport"]!["checks"]!.AsArray(), check => check!["kind"]!.GetValue<string>() == "templatePackage");
        Assert.Contains("Onboarding Summary", File.ReadAllText(markdownPath));
    }

    [Fact]
    public void Cli_OnboardingPackage_ShouldCreateZip()
    {
        var output = Path.Combine(NewTempDirectory(), "pilot.template-pilot.zip");
        var workspace = CopyExampleWorkspace();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "package", "--workspace", workspace, "--out", output);

        Assert.Equal(0, result.ExitCode);
        using var archive = ZipFile.OpenRead(output);
        Assert.Contains(archive.Entries, entry => entry.FullName == "manifest.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "requirements/requirements.redacted.json");
    }

    [Fact]
    public void Cli_OnboardingPackageValidate_ShouldReturnValid()
    {
        var package = BuildPackage();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "package-validate", "--package", package);
        var json = JsonNode.Parse(result.StandardOutput)!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("1.0.0", json["reportVersion"]!.GetValue<string>());
        Assert.True(json["isValid"]!.GetValue<bool>());
    }

    [Fact]
    public void Cli_OnboardingScaffold_ShouldNotOverwriteWithoutForce()
    {
        var workspace = InitializedWorkspace();
        var first = CliRunner.Run(RepoRoot(), "onboarding", "scaffold-requirements", "--workspace", workspace);
        var second = CliRunner.Run(RepoRoot(), "onboarding", "scaffold-requirements", "--workspace", workspace);

        Assert.Equal(0, first.ExitCode);
        Assert.NotEqual(0, second.ExitCode);
        Assert.Contains("already exists", second.StandardError);
    }

    [Fact]
    public void PrivacyGuard_ShouldAllowFictionalExamples()
    {
        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = OnboardingWorkspacePath() });

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Findings.Select(f => f.Code)));
        Assert.DoesNotContain(result.Findings, finding => finding.Code == "privacy.realInstitutionInExamples");
    }

    [Fact]
    public void PrivacyGuard_ShouldRejectRealInstitutionWorkspaceUnderExamples()
    {
        var workspace = CopyExampleWorkspaceUnderExamples();
        MutateFile(Path.Combine(workspace, "onboarding.json"), node => node["institution"]!["isRealInstitution"] = true);

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.realInstitutionInExamples" && finding.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void PrivacyGuard_ShouldDetectSourceDocumentsUnderExamples()
    {
        var workspace = CopyExampleWorkspaceUnderExamples();
        File.WriteAllText(Path.Combine(workspace, "source-documents", "manual.pdf"), "not a real pdf");

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.sourceDocumentInExamples");
    }

    [Fact]
    public void PrivacyGuard_ShouldDetectLongEvidenceExcerpt()
    {
        var workspace = CopyExampleWorkspaceUnderExamples();
        MutateFile(Path.Combine(workspace, "requirements", "requirements.json"), node => node["requirements"]![0]!["evidence"]!["shortQuote"] = new string('x', 400));

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace, MaxEvidenceExcerptLength = 120 });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.evidence.tooLong");
    }

    [Fact]
    public void PrivacyGuard_ShouldDetectAbsolutePath()
    {
        var workspace = CopyExampleWorkspaceUnderExamples();
        MutateFile(Path.Combine(workspace, "onboarding.json"), node => node["paths"]!["requirementsPath"] = "/tmp/requirements.json");

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.path.absolute");
    }

    [Fact]
    public void PrivacyGuard_ShouldDetectWindowsAbsolutePath()
    {
        var workspace = CopyExampleWorkspace();
        MutateFile(Path.Combine(workspace, "onboarding.json"), node => node["paths"]!["requirementsPath"] = "C:\\Users\\Example\\requirements.json");

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });

        var finding = Assert.Single(result.Findings, finding => finding.Code == "privacy.path.absolute" && finding.Path.EndsWith("$.paths.requirementsPath", StringComparison.Ordinal));
        Assert.Equal("C:\\U...json", finding.RedactedExcerpt);
        Assert.DoesNotContain("C:\\Users\\Example\\requirements.json", finding.Message);
    }

    [Fact]
    public void PrivacyGuard_ShouldDetectPathTraversal()
    {
        var workspace = CopyExampleWorkspace();
        MutateFile(Path.Combine(workspace, "onboarding.json"), node => node["paths"]!["templateDir"] = "../template");

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.path.traversal");
    }

    [Fact]
    public void PrivacyGuard_ShouldDetectForbiddenFontAsset()
    {
        var workspace = CopyExampleWorkspace();
        File.WriteAllText(Path.Combine(workspace, "template", "assets", "font.ttf"), "font");

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.fontAsset.forbidden");
    }

    [Fact]
    public void PrivacyGuard_ShouldDetectLikelyStudentId()
    {
        var workspace = CopyExampleWorkspace();
        MutateFile(Path.Combine(workspace, "fixtures", "document.json"), node => node["metadata"]!["studentId"] = "202612345678");

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.personal.studentId");
    }

    [Fact]
    public void PrivacyGuard_ShouldDetectLikelyPhoneNumber()
    {
        var workspace = CopyExampleWorkspace();
        MutateFile(Path.Combine(workspace, "requirements", "requirements.json"), node => node["approval"]!["notes"] = "Call 13912345678 for review.");

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.personal.phone");
    }

    [Fact]
    public void PrivacyGuard_ShouldDetectLikelyIdentityIdAndEmail()
    {
        var workspace = CopyExampleWorkspace();
        MutateFile(Path.Combine(workspace, "requirements", "requirements.json"), node =>
        {
            node["approval"]!["notes"] = "Reviewer 110105199001011234 should email reviewer@university.edu.";
        });

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.personal.identityId");
        Assert.Contains(result.Findings, finding => finding.Code == "privacy.personal.email");
        Assert.DoesNotContain(result.Findings, finding => finding.Message.Contains("110105199001011234", StringComparison.Ordinal));
    }

    [Fact]
    public void PrivacyGuard_ShouldDetectGeneratedDocxArtifact()
    {
        var workspace = CopyExampleWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace, "reports"));
        File.WriteAllText(Path.Combine(workspace, "reports", "rendered-draft.docx"), "not a real docx");

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.generatedArtifact.forbidden");
    }

    [Fact]
    public void PrivacyGuard_ShouldSuppressConfiguredWarningCode()
    {
        var workspace = CopyExampleWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace, "reports"));
        File.WriteAllText(Path.Combine(workspace, "reports", "rendered-draft.docx"), "not a real docx");
        var baseline = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });
        var generatedArtifactWarnings = baseline.Findings.Count(finding => finding.Code == "privacy.generatedArtifact.forbidden");

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions
        {
            Path = workspace,
            SuppressedWarningCodes = ["privacy.generatedArtifact.forbidden"]
        });

        Assert.True(result.IsValid);
        Assert.Equal(baseline.WarningCount - generatedArtifactWarnings, result.WarningCount);
        Assert.Equal(generatedArtifactWarnings, result.SuppressedWarningCount);
        Assert.DoesNotContain(result.Findings, finding => finding.Code == "privacy.generatedArtifact.forbidden");
        Assert.Contains(result.SuppressedFindings, finding => finding.Code == "privacy.generatedArtifact.forbidden");
    }

    [Fact]
    public void PrivacyGuard_ShouldNotSuppressPersonalDataWarnings()
    {
        var workspace = CopyExampleWorkspace();
        MutateFile(Path.Combine(workspace, "requirements", "requirements.json"), node => node["approval"]!["notes"] = "Reviewer should email reviewer@university.edu.");

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions
        {
            Path = workspace,
            SuppressedWarningCodes = ["privacy.personal.email"],
            SuppressedWarningPathPrefixes = ["requirements/"]
        });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.personal.email");
        Assert.DoesNotContain(result.SuppressedFindings, finding => finding.Code == "privacy.personal.email");
        Assert.Equal(0, result.SuppressedWarningCount);
    }

    [Fact]
    public void PrivacyGuard_ShouldFailWhenWarningThresholdExceeded()
    {
        var workspace = CopyExampleWorkspace();
        MutateFile(Path.Combine(workspace, "onboarding.json"), node => node["paths"]!["requirementsPath"] = "/tmp/requirements.json");

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions
        {
            Path = workspace,
            MaxWarningCount = 0
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Findings, finding => finding.Code == "privacy.warningThreshold.exceeded" && finding.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void PrivacyGuard_ShouldDetectOversizedBase64Blob()
    {
        var workspace = CopyExampleWorkspace();
        MutateFile(Path.Combine(workspace, "requirements", "requirements.json"), node => node["approval"]!["notes"] = new string('A', 300));

        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace, MaxBase64Length = 120 });

        Assert.Contains(result.Findings, finding => finding.Code == "privacy.base64.oversized" && finding.RedactedExcerpt == "base64:300 chars");
    }

    [Fact]
    public void PrivacyGuard_ShouldNotFlagFictionalExampleUniversity()
    {
        var result = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = OnboardingWorkspacePath() });

        Assert.DoesNotContain(result.Findings, finding => finding.Message.Contains("Example University", StringComparison.Ordinal));
    }

    [Fact]
    public void Cli_PrivacyScan_ShouldWriteJson()
    {
        var output = Path.Combine(NewTempDirectory(), "privacy.json");

        var result = CliRunner.Run(RepoRoot(), "privacy", "scan", "--path", "examples", "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("1.0.0", json["reportVersion"]!.GetValue<string>());
        Assert.True(json["isValid"]!.GetValue<bool>());
    }

    [Fact]
    public void Cli_PrivacyScan_ShouldApplyWarningSuppressionOptions()
    {
        var workspace = CopyExampleWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace, "reports"));
        File.WriteAllText(Path.Combine(workspace, "reports", "rendered-draft.docx"), "not a real docx");
        var expectedSuppressed = new PrivacyGuard()
            .Scan(new PrivacyGuardOptions { Path = workspace })
            .Findings
            .Count(finding => finding.Code == "privacy.generatedArtifact.forbidden");
        var output = Path.Combine(NewTempDirectory(), "privacy.json");

        var result = CliRunner.Run(
            RepoRoot(),
            "privacy",
            "scan",
            "--path",
            workspace,
            "--suppress-warning-code",
            "privacy.generatedArtifact.forbidden",
            "--out",
            output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("1.0.0", json["reportVersion"]!.GetValue<string>());
        Assert.Equal(expectedSuppressed, json["suppressedWarningCount"]!.GetValue<int>());
        Assert.DoesNotContain(json["findings"]!.AsArray(), finding => finding?["code"]?.GetValue<string>() == "privacy.generatedArtifact.forbidden");
    }

    [Fact]
    public void OnboardingReportBuilder_ShouldBuildReportForExampleWorkspace()
    {
        var report = new OnboardingReportBuilder().Build(new OnboardingReportOptions { WorkspacePath = CopyExampleWorkspace() });

        Assert.Equal("example-engineering-pilot", report.WorkspaceId);
        Assert.Equal("1.0.0", report.ReportVersion);
        Assert.Equal("pass", report.PrivacyStatus);
        Assert.NotEmpty(report.Checklist);
        Assert.Contains(report.VersionReport.Checks, check => check.Kind == "templatePackage");
    }

    [Fact]
    public void OnboardingReportBuilder_ShouldMarkReadyWhenQualityPasses()
    {
        var report = new OnboardingReportBuilder().Build(new OnboardingReportOptions { WorkspacePath = CopyExampleWorkspace() });

        Assert.Equal("readyForTemplateLibrary", report.ReleaseReadiness);
    }

    [Fact]
    public void OnboardingReportBuilder_ShouldMarkBlockedForPrivacyFindings()
    {
        var workspace = CopyExampleWorkspaceUnderExamples();
        MutateFile(Path.Combine(workspace, "onboarding.json"), node => node["institution"]!["isRealInstitution"] = true);

        var report = new OnboardingReportBuilder().Build(new OnboardingReportOptions { WorkspacePath = workspace });

        Assert.Equal("blocked", report.ReleaseReadiness);
        Assert.Contains(report.BlockingIssues, issue => issue.Code == "privacy.realInstitutionInExamples");
    }

    [Fact]
    public void OnboardingMarkdownRenderer_ShouldRenderChecklist()
    {
        var report = new OnboardingReportBuilder().Build(new OnboardingReportOptions { WorkspacePath = CopyExampleWorkspace() });

        var markdown = new OnboardingMarkdownRenderer().Render(report);

        Assert.Contains("## Checklist", markdown);
        Assert.Contains("workspace manifest", markdown);
    }

    [Fact]
    public void OnboardingMarkdownRenderer_ShouldRenderNextActions()
    {
        var report = new OnboardingReportBuilder().Build(new OnboardingReportOptions { WorkspacePath = CopyExampleWorkspace() });

        var markdown = new OnboardingMarkdownRenderer().Render(report);

        Assert.Contains("## Next Actions", markdown);
        Assert.Contains("pilot package", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TemplatePilotPackageBuilder_ShouldCreateDeterministicZip()
    {
        var workspace = CopyExampleWorkspace();
        var first = BuildPackage(workspace);
        var second = BuildPackage(workspace);

        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
    }

    [Fact]
    public void TemplatePilotPackageBuilder_ShouldExcludeSourceDocuments()
    {
        var package = BuildPackage();

        using var archive = ZipFile.OpenRead(package);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("source-documents/", StringComparison.Ordinal));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TemplatePilotPackageBuilder_ShouldIncludeRedactedRequirements()
    {
        var package = BuildPackage();
        var redacted = ExtractPackageEntry(package, "requirements/requirements.redacted.json");

        Assert.Contains("REDACTED", redacted);
        Assert.DoesNotContain("Template Reviewer", redacted);
    }

    [Fact]
    public void TemplatePilotPackageBuilder_ShouldIncludeReports()
    {
        var package = BuildPackage();

        using var archive = ZipFile.OpenRead(package);
        Assert.Contains(archive.Entries, entry => entry.FullName == "reports/onboarding-summary.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "reports/gate.json");
    }

    [Fact]
    public void TemplatePilotPackageValidator_ShouldValidateChecksums()
    {
        var package = BuildPackage();

        var result = new TemplatePilotPackageValidator().Validate(package);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.NotEmpty(result.Manifest!.Sha256);
    }

    [Fact]
    public void TemplatePilotPackageValidator_ShouldRejectPackageWithSourceDocx()
    {
        var package = Path.Combine(NewTempDirectory(), "bad.zip");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            archive.CreateEntry("source-documents/manual.docx");
        }

        var result = new TemplatePilotPackageValidator().Validate(package);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Forbidden package entry", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "privacy.package.forbiddenExtension" && diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Cli_OnboardingPackageValidate_ShouldReturnValid_ForBuiltPackage()
    {
        var package = BuildPackage();

        var result = CliRunner.Run(RepoRoot(), "onboarding", "package-validate", "--package", package);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("includedFiles", result.StandardOutput);
    }

    [Fact]
    public void GitIgnore_ShouldIgnoreOnboardingWorkspaces()
    {
        Assert.Contains("onboarding-workspaces/", File.ReadAllText(Path.Combine(RepoRoot(), ".gitignore")));
    }

    [Fact]
    public void GitIgnore_ShouldIgnoreGeneratedDocxAndPdf()
    {
        var gitignore = File.ReadAllText(Path.Combine(RepoRoot(), ".gitignore"));

        Assert.Contains("*.docx", gitignore);
        Assert.Contains("*.pdf", gitignore);
    }

    [Fact]
    public void GitIgnore_ShouldNotIgnoreRequiredExampleJson()
    {
        var gitignore = File.ReadAllText(Path.Combine(RepoRoot(), ".gitignore"));

        Assert.DoesNotContain("examples/*.json", gitignore);
        Assert.DoesNotContain("*.json", gitignore);
    }

    [Fact]
    public void CiQualityGateScript_ShouldRunPrivacyScan()
    {
        Assert.Contains("privacy scan", File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "ci-quality-gate")));
    }

    [Fact]
    public void CiQualityGateScript_ShouldRunOnboardingSummary()
    {
        Assert.Contains("onboarding summary", File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "ci-quality-gate")));
    }

    [Fact]
    public void CiQualityGateScript_ShouldRunOnboardingPackageValidate()
    {
        Assert.Contains("onboarding package-validate", File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "ci-quality-gate")));
    }

    [Fact]
    public void OnboardingWorkspaceValidator_ShouldRejectPathTraversal()
    {
        var workspace = CopyExampleWorkspace();
        MutateFile(Path.Combine(workspace, "onboarding.json"), node => node["paths"]!["templateDir"] = "../template");

        var result = new OnboardingWorkspaceValidator().Validate(workspace);

        Assert.Contains(result.Errors, error => error.Code == "onboarding.path.invalid");
    }

    [Fact]
    public void OnboardingWorkspaceValidator_ShouldRejectUnsupportedSchemaVersion()
    {
        var workspace = CopyExampleWorkspace();
        MutateFile(Path.Combine(workspace, "onboarding.json"), node => node["schemaVersion"] = "9.9.9");

        var result = new OnboardingWorkspaceValidator().Validate(workspace);

        Assert.Contains(result.Errors, error => error.Code.Contains("schemaVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void OnboardingPackage_ShouldRecordPrivacyScanSummary()
    {
        var package = BuildPackage();
        var manifest = JsonNode.Parse(ExtractPackageEntry(package, "manifest.json"))!;

        Assert.True(manifest["privacyScanSummary"]!["isValid"]!.GetValue<bool>());
        Assert.True(manifest["privacyScanSummary"]!["warningCount"]!.GetValue<int>() >= 0);
    }

    [Fact]
    public void OnboardingPackage_ShouldRecordPrivacyPolicySummary()
    {
        var package = BuildPackage();
        var manifest = JsonNode.Parse(ExtractPackageEntry(package, "manifest.json"))!;

        Assert.Equal(240, manifest["privacyPolicySummary"]!["maxEvidenceExcerptLength"]!.GetValue<int>());
        Assert.Contains(manifest["privacyPolicySummary"]!["nonSuppressibleWarningCodePrefixes"]!.AsArray(), item => item?.GetValue<string>() == "privacy.personal.");
    }

    [Fact]
    public void OnboardingPackage_ShouldIncludeChecksumsJson()
    {
        var package = BuildPackage();
        var checksums = JsonNode.Parse(ExtractPackageEntry(package, "checksums.json"))!.AsObject();

        Assert.Contains("manifest.json", checksums.Select(pair => pair.Key));
    }

    [Fact]
    public void RedactionHelper_ShouldTrimLongEvidence()
    {
        var node = JsonNode.Parse(File.ReadAllText(RequirementsPath()))!;
        node["requirements"]![0]!["evidence"]!["shortQuote"] = new string('x', 200);

        var redacted = RedactionHelper.RedactRequirementCapture(node, 50).ToJsonString();

        Assert.Contains(new string('x', 50) + "...", redacted);
        Assert.DoesNotContain(new string('x', 120), redacted);
    }

    private static string BuildPackage(string? workspace = null)
    {
        var output = Path.Combine(NewTempDirectory(), "pilot.template-pilot.zip");
        var result = new TemplatePilotPackageBuilder().Build(workspace ?? CopyExampleWorkspace(), output);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        return output;
    }

    private static string ExtractPackageEntry(string package, string entryName)
    {
        using var archive = ZipFile.OpenRead(package);
        var entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException(entryName);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string InitializedWorkspace()
    {
        var workspace = NewWorkspacePath();
        new OnboardingWorkspaceInitializer().Initialize(InitOptions(workspace));
        return workspace;
    }

    private static OnboardingWorkspaceInitOptions InitOptions(string workspace)
    {
        return new OnboardingWorkspaceInitOptions
        {
            WorkspacePath = workspace,
            School = "Example University",
            College = "Example Engineering College",
            DegreeType = "master",
            Locale = "zh-CN"
        };
    }

    private static string CopyExampleWorkspace()
    {
        var target = NewWorkspacePath();
        CopyDirectory(OnboardingWorkspacePath(), target);
        return target;
    }

    private static string CopyExampleWorkspaceUnderExamples()
    {
        var root = Path.Combine(NewTempDirectory(), "examples");
        var target = Path.Combine(root, "example-engineering-pilot");
        CopyDirectory(OnboardingWorkspacePath(), target);
        return target;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void MutateFile(string path, Action<JsonNode> mutate)
    {
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        mutate(node);
        File.WriteAllText(path, node.ToJsonString(ThesisJson.Options));
    }

    private static string MutateOnboarding(Action<JsonNode> mutate)
    {
        var directory = NewTempDirectory();
        var target = Path.Combine(directory, "onboarding.json");
        File.Copy(OnboardingManifestPath(), target);
        MutateFile(target, mutate);
        return target;
    }

    private static string NewWorkspacePath() => Path.Combine(NewTempDirectory(), "pilot-example");

    private static string NewTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string RepoRoot() => TestRenderHelper.LocateRepoRootForTests();
    private static string SchemaPath(string name) => Path.Combine(RepoRoot(), "schemas", name);
    private static string OnboardingWorkspacePath() => Path.Combine(RepoRoot(), "examples", "onboarding", "example-engineering-pilot");
    private static string OnboardingManifestPath() => Path.Combine(OnboardingWorkspacePath(), "onboarding.json");
    private static string TemplatePath() => Path.Combine(RepoRoot(), "examples", "templates", "example-university-engineering");
    private static string DocumentPath() => Path.Combine(RepoRoot(), "examples", "full-thesis", "document.json");
    private static string RequirementsPath() => Path.Combine(RepoRoot(), "examples", "requirements", "example-engineering-requirements.json");
}
