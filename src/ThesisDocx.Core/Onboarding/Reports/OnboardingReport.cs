using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Privacy;
using ThesisDocx.Core.Templates.Authoring;
using ThesisDocx.Core.Templates.Gate;

namespace ThesisDocx.Core.Onboarding.Reports;

public sealed class OnboardingReport
{
    public string WorkspaceId { get; set; } = string.Empty;
    public Dictionary<string, object> InstitutionSummary { get; set; } = new(StringComparer.Ordinal);
    public string Phase { get; set; } = string.Empty;
    public string PrivacyStatus { get; set; } = "unknown";
    public string RequirementStatus { get; set; } = "unknown";
    public string TemplateStatus { get; set; } = "unknown";
    public string FixtureStatus { get; set; } = "unknown";
    public string BaselineStatus { get; set; } = "unknown";
    public string GateStatus { get; set; } = "unknown";
    public string DiagnoseStatus { get; set; } = "unknown";
    public string AuthoringStatus { get; set; } = "unknown";
    public string ReleaseReadiness { get; set; } = "notReady";
    public List<OnboardingReportIssue> BlockingIssues { get; set; } = [];
    public List<OnboardingReportIssue> Warnings { get; set; } = [];
    public List<string> RecommendedNextActions { get; set; } = [];
    public List<string> ArtifactPaths { get; set; } = [];
    public List<OnboardingChecklistItem> Checklist { get; set; } = [];
}

public sealed class OnboardingReportIssue
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string Message { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class OnboardingChecklistItem
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string Message { get; set; } = string.Empty;
}

public sealed class OnboardingReportBuilder
{
    public OnboardingReport Build(OnboardingReportOptions options)
    {
        var workspace = OnboardingWorkspaceInspector.Load(options.WorkspacePath);
        Directory.CreateDirectory(workspace.ReportsDirectory);
        Directory.CreateDirectory(workspace.ArtifactsDirectory);

        var validation = new OnboardingWorkspaceValidator().Validate(options.WorkspacePath);
        var privacy = new PrivacyGuard().Scan(PrivacyGuardOptions.FromPolicy(options.WorkspacePath, workspace.Manifest.Privacy));
        var templatePath = workspace.TemplateDirectory;
        var documentPath = workspace.DocumentPath;
        var requirementsPath = workspace.RequirementsFile;

        TemplateGateReport? gate = null;
        DiagnosticReport? diagnostic = null;
        TemplateAuthoringReport? authoring = null;
        if (File.Exists(Path.Combine(templatePath, "template.json")) && File.Exists(documentPath))
        {
            gate = new TemplateGateService().Run(new TemplateGateOptions
            {
                TemplatePath = templatePath,
                DocumentPath = documentPath,
                OutputDirectory = Path.Combine(workspace.ArtifactsDirectory, "gate"),
                CoverageThreshold = workspace.Manifest.Quality.CoverageThreshold
            });
            diagnostic = new DiagnosticReportBuilder().Build(gate, artifacts: gate.Artifacts);
            authoring = new TemplateAuthoringReportBuilder().Build(new TemplateAuthoringReportOptions
            {
                TemplatePath = templatePath,
                DocumentPath = documentPath,
                RequirementsPath = File.Exists(requirementsPath) ? requirementsPath : null,
                OutputDirectory = Path.Combine(workspace.ArtifactsDirectory, "authoring"),
                CoverageThreshold = workspace.Manifest.Quality.CoverageThreshold
            });
        }

        var report = new OnboardingReport
        {
            WorkspaceId = workspace.Manifest.WorkspaceId,
            Phase = workspace.Manifest.Status.Phase.ToString(),
            InstitutionSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["school"] = workspace.Manifest.Institution.School,
                ["college"] = workspace.Manifest.Institution.College,
                ["degreeType"] = workspace.Manifest.Institution.DegreeType,
                ["locale"] = workspace.Manifest.Institution.Locale,
                ["isRealInstitution"] = workspace.Manifest.Institution.IsRealInstitution
            },
            PrivacyStatus = privacy.IsValid ? "pass" : "fail",
            RequirementStatus = validation.Errors.Any(e => e.Code.StartsWith("requirements.", StringComparison.Ordinal)) ? "fail" : File.Exists(requirementsPath) ? "pass" : "missing",
            TemplateStatus = File.Exists(Path.Combine(templatePath, "template.json")) ? "present" : "missing",
            FixtureStatus = File.Exists(documentPath) ? "present" : "missing",
            BaselineStatus = Directory.Exists(workspace.BaselinesDirectory) && Directory.EnumerateFiles(workspace.BaselinesDirectory).Any() ? "present" : "missing",
            GateStatus = gate?.Status.ToString() ?? "notRun",
            DiagnoseStatus = diagnostic?.Status ?? "notRun",
            AuthoringStatus = authoring?.PublishReadiness ?? "notRun"
        };

        report.BlockingIssues.AddRange(validation.Errors.Select(e => Issue(e.Code, DiagnosticSeverity.Error, e.Message, e.Path)));
        report.Warnings.AddRange(validation.Warnings.Select(e => Issue(e.Code, DiagnosticSeverity.Warning, e.Message, e.Path)));
        report.BlockingIssues.AddRange(privacy.Findings.Where(f => UnifiedDiagnosticMapper.IsError(f.Severity)).Select(f => Issue(f.Code, f.Severity, f.Message, f.Path)));
        report.Warnings.AddRange(privacy.Findings.Where(f => !UnifiedDiagnosticMapper.IsError(f.Severity)).Select(f => Issue(f.Code, f.Severity, f.Message, f.Path)));
        if (gate is not null && gate.Status == TemplateGateStatus.Fail)
        {
            report.BlockingIssues.Add(Issue("onboarding.gate.failed", DiagnosticSeverity.Error, "Template gate failed.", "gate"));
        }

        if (authoring is not null && authoring.PublishReadiness == "notReady")
        {
            report.BlockingIssues.Add(Issue("onboarding.authoring.notReady", DiagnosticSeverity.Error, "Authoring report is not ready.", "authoring"));
        }

        report.Checklist = BuildChecklist(report, privacy, validation, gate, authoring).OrderBy(i => i.Code, StringComparer.Ordinal).ToList();
        report.ReleaseReadiness = report.BlockingIssues.Count > 0
            ? "blocked"
            : gate?.Status == TemplateGateStatus.Pass && authoring?.PublishReadiness == "ready"
                ? "readyForTemplateLibrary"
                : "readyForInternalReview";
        report.RecommendedNextActions = BuildActions(report);
        report.ArtifactPaths = CollectArtifacts(workspace, gate, authoring);
        return report;
    }

    private static List<OnboardingChecklistItem> BuildChecklist(
        OnboardingReport report,
        PrivacyGuardResult privacy,
        OnboardingWorkspaceValidationResult validation,
        TemplateGateReport? gate,
        TemplateAuthoringReport? authoring)
    {
        return
        [
            Item("workspace.valid", "workspace manifest and required paths valid", validation.IsValid),
            Item("privacy.clean", "privacy scan has no error findings", privacy.IsValid, $"{privacy.BreakingCount} errors; {privacy.WarningCount} warnings; {privacy.SuppressedWarningCount} suppressed warnings"),
            Item("requirements.present", "RequirementCapture present and valid", report.RequirementStatus == "pass"),
            Item("template.present", "TemplatePackage scaffold present", report.TemplateStatus == "present"),
            Item("fixtures.present", "fixture document present", report.FixtureStatus == "present"),
            Item("baselines.present", "baseline manifest present", report.BaselineStatus == "present"),
            Item("gate.pass", "template gate pass", gate?.Status == TemplateGateStatus.Pass),
            Item("authoring.ready", "authoring report ready", authoring?.PublishReadiness == "ready")
        ];
    }

    private static List<string> BuildActions(OnboardingReport report)
    {
        var actions = new List<string>();
        if (report.PrivacyStatus != "pass") actions.Add("Resolve privacy findings before packaging or publishing.");
        if (report.RequirementStatus != "pass") actions.Add("Complete and validate RequirementCapture.");
        if (report.TemplateStatus != "present") actions.Add("Run onboarding scaffold-template from a reviewed base template.");
        if (report.FixtureStatus != "present") actions.Add("Run onboarding scaffold-fixtures with redacted thesis fixtures.");
        if (report.GateStatus is "Fail" or "notRun") actions.Add("Run onboarding run-gate and fix blocking template checks.");
        if (actions.Count == 0) actions.Add("Submit the pilot package for manual template review.");
        return actions.Order(StringComparer.Ordinal).ToList();
    }

    private static List<string> CollectArtifacts(ResolvedOnboardingWorkspace workspace, TemplateGateReport? gate, TemplateAuthoringReport? authoring)
    {
        var paths = new List<string>();
        if (gate is not null) paths.AddRange(gate.ArtifactPaths.Values);
        if (authoring is not null) paths.AddRange(authoring.RelatedArtifacts.Values);
        paths.Add(workspace.RequirementsFile);
        paths.Add(Path.Combine(workspace.Root, "onboarding.json"));
        return paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    private static OnboardingChecklistItem Item(string code, string title, bool passed, string? message = null)
    {
        return new OnboardingChecklistItem
        {
            Code = code,
            Title = title,
            Status = passed ? "pass" : "fail",
            Message = string.IsNullOrWhiteSpace(message) ? (passed ? "passed" : "failed") : message
        };
    }

    private static OnboardingReportIssue Issue(string code, string severity, string message, string path)
    {
        return new OnboardingReportIssue { Code = code, Severity = severity, Message = message, Path = path };
    }
}

public sealed class OnboardingReportOptions
{
    public string WorkspacePath { get; set; } = string.Empty;
}

public sealed class OnboardingMarkdownRenderer
{
    public string Render(OnboardingReport report)
    {
        var lines = new List<string>
        {
            $"# Onboarding Summary: {report.WorkspaceId}",
            string.Empty,
            $"- Release readiness: `{report.ReleaseReadiness}`",
            $"- Phase: `{report.Phase}`",
            $"- Privacy: `{report.PrivacyStatus}`",
            $"- Gate: `{report.GateStatus}`",
            $"- Authoring: `{report.AuthoringStatus}`",
            string.Empty,
            "## Blocking Issues"
        };
        lines.AddRange(report.BlockingIssues.Count == 0
            ? ["- None"]
            : report.BlockingIssues.Select(issue => $"- `{issue.Code}` {issue.Message} ({issue.Path})"));
        lines.Add(string.Empty);
        lines.Add("## Warnings");
        lines.AddRange(report.Warnings.Count == 0
            ? ["- None"]
            : report.Warnings.Take(10).Select(issue => $"- `{issue.Code}` {issue.Message} ({issue.Path})"));
        lines.Add(string.Empty);
        lines.Add("## Next Actions");
        lines.AddRange(report.RecommendedNextActions.Select(action => $"- {action}"));
        lines.Add(string.Empty);
        lines.Add("## Checklist");
        lines.AddRange(report.Checklist.Select(item => $"- `{item.Status}` {item.Title} (`{item.Code}`)"));
        lines.Add(string.Empty);
        lines.Add("## Artifacts");
        lines.AddRange(report.ArtifactPaths.Take(12).Select(path => $"- `{path}`"));
        return string.Join(Environment.NewLine, lines);
    }
}

public sealed class OnboardingReportSection
{
    public string Title { get; set; } = string.Empty;
    public Dictionary<string, object> Values { get; set; } = new(StringComparer.Ordinal);
}
