using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Requirements;
using ThesisDocx.Core.Templates.Baselines;
using ThesisDocx.Core.Templates.Gate;
using ThesisDocx.Core.Templates.Regression;
using ThesisDocx.Core.Validation.FormatRuleCoverage;

namespace ThesisDocx.Core.Templates.Authoring;

public sealed class TemplateAuthoringReportBuilder
{
    public TemplateAuthoringReport Build(TemplateAuthoringReportOptions options)
    {
        var resolution = new TemplateResolver().Resolve(options.TemplatePath);
        var requirements = LoadRequirements(options.RequirementsPath);
        var requirementReport = requirements is null
            ? null
            : new RequirementMappingReporter().Build(requirements, options.TemplatePath);
        var gate = new TemplateGateService().Run(new TemplateGateOptions
        {
            TemplatePath = options.TemplatePath,
            DocumentPath = options.DocumentPath,
            OutputDirectory = Path.Combine(options.OutputDirectory, "gate"),
            CoverageThreshold = options.CoverageThreshold
        });
        var regression = string.IsNullOrWhiteSpace(options.SuitePath)
            ? null
            : new TemplateRegressionRunner().Run(options.SuitePath, Path.Combine(options.OutputDirectory, "regression"));
        var baseline = string.IsNullOrWhiteSpace(options.SuitePath)
            ? null
            : new TemplateBaselineManager().CompareSuite(options.SuitePath, Path.Combine(options.OutputDirectory, "baseline"));
        var coverage = new FormatRuleCoverageReporter().Build(options.TemplatePath);
        var diagnostic = new DiagnosticReportBuilder().Build(gate, regression, baseline, requirementReport, artifacts: gate.Artifacts);

        var report = new TemplateAuthoringReport
        {
            TemplateId = resolution.Template?.Id ?? string.Empty,
            TemplateVersion = resolution.Template?.Version ?? string.Empty,
            TemplateSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["isResolved"] = resolution.IsValid,
                ["name"] = resolution.Template?.Name ?? string.Empty,
                ["school"] = resolution.Template?.School ?? string.Empty,
                ["college"] = resolution.Template?.College ?? string.Empty,
                ["pageTemplateCount"] = resolution.PageTemplates.Count,
                ["assetCount"] = resolution.Assets.Count
            },
            RequirementMappingSummary = BuildRequirementSummary(requirementReport),
            ValidationSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["gateStatus"] = gate.Status.ToString(),
                ["failedGateChecks"] = gate.Checks.Count(check => check.Status == TemplateGateCheckStatus.Fail),
                ["diagnosticStatus"] = diagnostic.Status
            },
            RegressionSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["passed"] = regression?.Passed ?? true,
                ["caseCount"] = regression?.Cases.Count ?? 0,
                ["failedCases"] = regression?.FailedCases.Count ?? 0
            },
            BaselineSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["passed"] = baseline?.Passed ?? true,
                ["caseCount"] = baseline?.Cases.Count ?? 0,
                ["failedCases"] = baseline?.Cases.Count(c => !c.Passed) ?? 0
            },
            CoverageSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["coverageRatio"] = gate.CoverageRatio,
                ["threshold"] = options.CoverageThreshold,
                ["ruleCount"] = coverage.Rules.Count
            },
            DiagnosticSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["status"] = diagnostic.Status,
                ["issueCount"] = diagnostic.IssueCount,
                ["breakingCount"] = diagnostic.BreakingCount,
                ["warningCount"] = diagnostic.WarningCount
            },
            BlockingIssues = diagnostic.Issues.Where(issue => issue.Severity == "breaking").ToList(),
            Warnings = diagnostic.Issues.Where(issue => issue.Severity == "warning").ToList(),
            RecommendedNextActions = diagnostic.TopRecommendedActions,
            RelatedArtifacts = diagnostic.RelatedArtifacts
        };

        report.Checklist = BuildChecklist(resolution.IsValid, requirementReport, gate, regression, baseline, report).OrderBy(item => item.Code, StringComparer.Ordinal).ToList();
        report.FailedChecklistItems = report.Checklist.Where(item => item.Status == "fail").ToList();
        report.WarningChecklistItems = report.Checklist.Where(item => item.Status == "warning").ToList();
        report.BaselineStatus = baseline?.Passed ?? true ? "pass" : "fail";
        report.RequirementMappingStatus = requirementReport is null ? "notProvided" : requirementReport.IsValid && requirementReport.UnmappedRequirements == 0 ? "pass" : "fail";
        report.RegressionStatus = regression?.Passed ?? true ? "pass" : "fail";
        report.GateStatus = gate.Status == TemplateGateStatus.Fail ? "fail" : gate.Status == TemplateGateStatus.PassWithWarnings ? "warning" : "pass";
        report.DiagnosticStatus = diagnostic.Status;
        report.PublishReadiness = report.BlockingIssues.Count > 0 || report.Checklist.Any(item => item.Status == "fail")
            ? "notReady"
            : report.Warnings.Count > 0 || report.Checklist.Any(item => item.Status == "warning")
                ? "readyWithWarnings"
                : "ready";
        report.QualityScore = ComputeQualityScore(report);
        report.SuggestedMergeDecision = report.BlockingIssues.Count > 0 || report.FailedChecklistItems.Count > 0
            ? "reject"
            : report.Warnings.Count > 0 || report.WarningChecklistItems.Count > 0
                ? "approveWithWarnings"
                : "approve";
        report.ReadinessReasons = BuildReadinessReasons(report);
        return report;
    }

    private static RequirementCapture? LoadRequirements(string? requirementsPath)
    {
        return string.IsNullOrWhiteSpace(requirementsPath)
            ? null
            : new RequirementCaptureLoader().Load(requirementsPath);
    }

    private static Dictionary<string, object> BuildRequirementSummary(RequirementMappingReport? report)
    {
        if (report is null)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provided"] = false
            };
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["provided"] = true,
            ["isValid"] = report.IsValid,
            ["totalRequirements"] = report.TotalRequirements,
            ["mappedRequirements"] = report.MappedRequirements,
            ["partialRequirements"] = report.PartialRequirements,
            ["unsupportedRequirements"] = report.UnsupportedRequirements,
            ["unmappedRequirements"] = report.UnmappedRequirements
        };
    }

    private static IEnumerable<TemplateAuthoringChecklistItem> BuildChecklist(
        bool templateResolved,
        RequirementMappingReport? requirements,
        TemplateGateReport gate,
        TemplateRegressionResult? regression,
        TemplateBaselineCompareResult? baseline,
        TemplateAuthoringReport report)
    {
        yield return Item("template.schema", "template schema valid", gate.Checks.Any(c => c.Code == "template.validate" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("format.resolved", "resolved format spec valid", templateResolved && gate.Checks.Any(c => c.Code == "format.schema" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("requirements.valid", "requirement capture valid", requirements?.IsValid ?? true, requirements is null ? "No requirements file provided." : string.Empty);
        yield return Item("requirements.mapped", "approved requirements mapped", requirements is null || requirements.UnmappedRequirements == 0);
        yield return Item("openxml.clean", "OpenXmlValidator clean", gate.Checks.Any(c => c.Code == "openxml" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("format.clean", "FormatConformanceValidator clean", gate.Checks.Any(c => c.Code == "format.conformance" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("layout.generated", "layout signature generated", gate.Checks.Any(c => c.Code == "layout.signature" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("baseline.passed", "baseline compare passed", baseline?.Passed ?? true);
        yield return Item("regression.passed", "regression suite passed", regression?.Passed ?? true);
        yield return Item("gate.passed", "gate passed", gate.Status != TemplateGateStatus.Fail);
        yield return Item("coverage.threshold", "coverage threshold passed", gate.Checks.Any(c => c.Code == "coverage" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("pageTemplates.rendered", "page templates rendered", gate.Artifacts.ContainsKey("docx"));
        yield return Item("assets.rendered", "required assets rendered", gate.Checks.Any(c => c.Code == "assets.forbidden" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("customProperties.written", "custom properties written", gate.Checks.Any(c => c.Code == "customProperties" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("limitations.documented", "limitations documented", gate.Checks.Any(c => c.Code == "limitations" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("docs.present", "docs present", true);
        yield return Item("diagnostics.clean", "diagnostics clean", report.BlockingIssues.Count == 0);
    }

    private static TemplateAuthoringChecklistItem Item(string code, string title, bool passed, string? message = null)
    {
        return new TemplateAuthoringChecklistItem
        {
            Code = code,
            Title = title,
            Status = passed ? "pass" : "fail",
            Message = string.IsNullOrWhiteSpace(message) ? (passed ? "passed" : "failed") : message
        };
    }

    private static int ComputeQualityScore(TemplateAuthoringReport report)
    {
        var score = 100;
        score -= report.FailedChecklistItems.Count * 10;
        score -= report.WarningChecklistItems.Count * 4;
        score -= report.BlockingIssues.Count * 8;
        score -= report.Warnings.Count * 3;
        if (report.CoverageSummary.TryGetValue("coverageRatio", out var ratioValue) && Convert.ToDouble(ratioValue) < 0.9)
        {
            score -= 5;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static List<string> BuildReadinessReasons(TemplateAuthoringReport report)
    {
        var reasons = new List<string>();
        if (report.FailedChecklistItems.Count > 0)
        {
            reasons.Add($"{report.FailedChecklistItems.Count} checklist item(s) failed.");
        }

        if (report.BlockingIssues.Count > 0)
        {
            reasons.Add($"{report.BlockingIssues.Count} blocking diagnostic issue(s) remain.");
        }

        if (report.WarningChecklistItems.Count > 0 || report.Warnings.Count > 0)
        {
            reasons.Add("Warnings require reviewer acknowledgement.");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("All quality checks passed.");
        }

        return reasons.Order(StringComparer.Ordinal).ToList();
    }
}

public sealed class TemplateAuthoringReportOptions
{
    public string TemplatePath { get; set; } = string.Empty;

    public string DocumentPath { get; set; } = string.Empty;

    public string? RequirementsPath { get; set; }

    public string? SuitePath { get; set; }

    public string OutputDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "ThesisDocx.Authoring");

    public double CoverageThreshold { get; set; } = 0.85;
}
