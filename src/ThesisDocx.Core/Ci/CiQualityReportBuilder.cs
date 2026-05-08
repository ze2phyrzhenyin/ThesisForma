using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Requirements;
using ThesisDocx.Core.Templates.Authoring;
using ThesisDocx.Core.Templates.Baselines;
using ThesisDocx.Core.Templates.Gate;
using ThesisDocx.Core.Templates.Regression;
using ThesisDocx.Core.Testing.NegativeFixtures;

namespace ThesisDocx.Core.Ci;

public sealed class CiQualityReportBuilder
{
    public CiQualityReport Build(CiQualityReportOptions options)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        var report = new CiQualityReport();

        var requirements = new RequirementMappingReporter().Build(new RequirementCaptureLoader().Load(options.RequirementsPath), options.TemplatePath);
        AddCheck(report, "requirements", "Requirements mapping", requirements.IsValid, $"{requirements.MappedRequirements} mapped, {requirements.UnmappedRequirements} unmapped", ("requirementsReport", Path.Combine(options.OutputDirectory, "requirements-report.json")));

        var baseline = new TemplateBaselineManager().CompareSuite(options.SuitePath, Path.Combine(options.OutputDirectory, "baseline"));
        AddCheck(report, "baseline", "Baseline compare", baseline.Passed, $"{baseline.Cases.Count} case(s)", ("baselineReport", Path.Combine(options.OutputDirectory, "baseline-compare-report.json")));

        var regression = new TemplateRegressionRunner().Run(options.SuitePath, Path.Combine(options.OutputDirectory, "regression"));
        AddCheck(report, "regression", "Template regression", regression.Passed, $"{regression.Cases.Count} case(s)", ("regressionReport", Path.Combine(options.OutputDirectory, "template-regression-report.json")));

        var gate = new TemplateGateService().Run(new TemplateGateOptions
        {
            TemplatePath = options.TemplatePath,
            DocumentPath = options.DocumentPath,
            OutputDirectory = Path.Combine(options.OutputDirectory, "gate"),
            CoverageThreshold = options.Threshold
        });
        AddCheck(report, "gate", "Template gate", gate.Status != TemplateGateStatus.Fail, gate.Status.ToString(), ("gateReport", Path.Combine(options.OutputDirectory, "template-gate-report.json")));

        var diagnostic = new DiagnosticReportBuilder().Build(gate, regression, baseline, requirements, artifacts: gate.Artifacts);
        AddCheck(report, "diagnose", "Template diagnostics", diagnostic.BreakingCount == 0, $"{diagnostic.IssueCount} issue(s)", ("diagnosticReport", Path.Combine(options.OutputDirectory, "template-diagnostic-report.json")));

        var authoring = new TemplateAuthoringReportBuilder().Build(new TemplateAuthoringReportOptions
        {
            TemplatePath = options.TemplatePath,
            DocumentPath = options.DocumentPath,
            RequirementsPath = options.RequirementsPath,
            SuitePath = options.SuitePath,
            OutputDirectory = Path.Combine(options.OutputDirectory, "authoring"),
            CoverageThreshold = options.Threshold
        });
        AddCheck(report, "authoring", "Template authoring report", authoring.PublishReadiness != "notReady", authoring.PublishReadiness, ("authoringReport", Path.Combine(options.OutputDirectory, "template-authoring-report.json")));
        report.VersionReport.MergeFrom(gate.VersionReport);
        report.VersionReport.MergeFrom(authoring.VersionReport);

        var negative = new NegativeFixtureRunner().Run(options.NegativeFixturesPath);
        AddCheck(report, "negativeFixtures", "Negative fixtures", negative.Passed, $"{negative.Cases.Count} negative case(s)", ("negativeFixturesReport", Path.Combine(options.OutputDirectory, "negative-fixtures-report.json")));

        WriteJson(Path.Combine(options.OutputDirectory, "requirements-report.json"), requirements);
        WriteJson(Path.Combine(options.OutputDirectory, "baseline-compare-report.json"), baseline);
        WriteJson(Path.Combine(options.OutputDirectory, "template-regression-report.json"), regression);
        WriteJson(Path.Combine(options.OutputDirectory, "template-gate-report.json"), gate);
        WriteJson(Path.Combine(options.OutputDirectory, "template-diagnostic-report.json"), diagnostic);
        WriteJson(Path.Combine(options.OutputDirectory, "template-authoring-report.json"), authoring);
        WriteJson(Path.Combine(options.OutputDirectory, "negative-fixtures-report.json"), negative);

        report.BlockingIssues.AddRange(diagnostic.Issues.Where(issue => UnifiedDiagnosticMapper.IsError(issue.Severity)));
        report.Warnings.AddRange(diagnostic.Issues.Where(issue => UnifiedDiagnosticMapper.IsWarning(issue.Severity)));
        if (!negative.Passed)
        {
            report.BlockingIssues.Add(new DiagnosticIssue
            {
                Id = "ci.negativeFixtures.failed",
                Source = "CI",
                Category = "negativeFixtures",
                Severity = DiagnosticSeverity.Error,
                Title = "Negative fixtures failed",
                Message = "At least one expected failure was not observed."
            });
        }

        report.Artifacts = report.Checks
            .SelectMany(check => check.Artifacts)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        report.RecommendedNextActions = diagnostic.TopRecommendedActions
            .Concat(authoring.RecommendedNextActions)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Take(8)
            .ToList();
        report.QualityScore = Math.Min(authoring.QualityScore, negative.Passed ? authoring.QualityScore : Math.Max(0, authoring.QualityScore - 20));
        report.MergeDecision = report.BlockingIssues.Count > 0 || report.Checks.Any(check => check.Status == "fail")
            ? "reject"
            : report.Warnings.Count > 0 || report.Checks.Any(check => check.Status == "warning")
                ? "approveWithWarnings"
                : "approve";
        report.Status = report.MergeDecision == "reject" ? "fail" : report.MergeDecision == "approveWithWarnings" ? "warning" : "pass";
        report.Checks = report.Checks.OrderBy(check => check.Code, StringComparer.Ordinal).ToList();
        return report;
    }

    private static void AddCheck(CiQualityReport report, string code, string name, bool passed, string message, params (string Key, string Path)[] artifacts)
    {
        report.Checks.Add(new CiQualityCheck
        {
            Code = code,
            Name = name,
            Status = passed ? "pass" : "fail",
            Message = message,
            Artifacts = artifacts.ToDictionary(pair => pair.Key, pair => pair.Path, StringComparer.Ordinal)
        });
    }

    private static void WriteJson<T>(string path, T value)
    {
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(value, ThesisDocx.Core.Utilities.ThesisJson.Options));
    }
}

public sealed class CiQualityReportOptions
{
    public string TemplatePath { get; set; } = string.Empty;

    public string DocumentPath { get; set; } = string.Empty;

    public string RequirementsPath { get; set; } = string.Empty;

    public string SuitePath { get; set; } = string.Empty;

    public string NegativeFixturesPath { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "ThesisDocx.Ci");

    public double Threshold { get; set; } = 0.85;
}
