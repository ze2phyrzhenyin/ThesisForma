using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Requirements;
using ThesisDocx.Core.Templates.Baselines;
using ThesisDocx.Core.Templates.Gate;
using ThesisDocx.Core.Templates.Regression;
using ThesisDocx.Core.Validation;

namespace ThesisDocx.Core.Diagnostics;

public sealed class DiagnosticReportBuilder
{
    private readonly FixHintEngine _fixHints = new();

    public DiagnosticReport Build(
        TemplateGateReport? gate = null,
        TemplateRegressionResult? regression = null,
        TemplateBaselineCompareResult? baseline = null,
        RequirementMappingReport? requirements = null,
        OpenXmlValidationResult? validation = null,
        Dictionary<string, string>? artifacts = null)
    {
        var report = new DiagnosticReport();
        if (gate is not null)
        {
            foreach (var check in gate.Checks.Where(c => c.Status != TemplateGateCheckStatus.Pass))
            {
                AddIssue(report, "TemplateGate", check.Code, check.Code, check.Status == TemplateGateCheckStatus.Fail ? "breaking" : "warning", check.Name, check.Message, null, null, null, null);
            }

            foreach (var artifact in gate.Artifacts)
            {
                report.RelatedArtifacts[$"gate.{artifact.Key}"] = artifact.Value;
            }
        }

        if (regression is not null)
        {
            foreach (var failure in regression.Cases.Where(c => !c.Passed))
            {
                foreach (var error in failure.Errors)
                {
                    AddIssue(report, "TemplateRegression", error, "baseline", "breaking", $"Regression case {failure.Id} failed", error, null, null, null, failure.Id);
                }
            }
        }

        if (baseline is not null)
        {
            foreach (var caseResult in baseline.Cases.Where(c => !c.Passed))
            {
                foreach (var diff in caseResult.Diffs.DefaultIfEmpty(new BaselineDiffSummary { Category = "baseline", Path = caseResult.CaseId }))
                {
                    AddIssue(report, "Baseline", $"baseline.{caseResult.CaseId}.{diff.Category}", diff.Category, diff.Severity.Equals("Breaking", StringComparison.OrdinalIgnoreCase) ? "breaking" : "warning", $"Baseline mismatch for {caseResult.CaseId}", string.Join("; ", caseResult.Errors), diff.Path, null, caseResult.FixtureId, caseResult.CaseId, diff.Expected, diff.Actual);
                }
            }
        }

        if (requirements is not null)
        {
            foreach (var error in requirements.Errors)
            {
                AddIssue(report, "RequirementMapping", error.Code, "requirements", "breaking", "Requirement mapping issue", error.Message, error.Path, null, null, null);
            }

            foreach (var warning in requirements.Warnings)
            {
                AddIssue(report, "RequirementMapping", warning.Code, "requirements", "warning", "Requirement mapping warning", warning.Message, warning.Path, null, null, null);
            }

            if (requirements.UnmappedRequirements > 0)
            {
                AddIssue(report, "RequirementMapping", "requirements.unmapped", "requirements", "breaking", "Approved requirements remain unmapped", $"{requirements.UnmappedRequirements} requirements are unmapped.", "$.mappings", null, null, null);
            }
        }

        if (validation is not null)
        {
            foreach (var error in validation.Errors)
            {
                AddIssue(report, "FormatConformanceValidator", error.Code, Classify(error.Code), "breaking", error.Code, error.Message, error.Path, error.PartName, null, null, error.Expected, error.Actual);
            }
        }

        if (artifacts is not null)
        {
            foreach (var artifact in artifacts.OrderBy(a => a.Key, StringComparer.Ordinal))
            {
                report.RelatedArtifacts[artifact.Key] = artifact.Value;
            }
        }

        foreach (var issue in report.Issues)
        {
            issue.FixHints = _fixHints.Suggest(issue).ToList();
            issue.RelatedDocs = issue.FixHints.Select(h => h.DocsRef).Where(d => !string.IsNullOrWhiteSpace(d)).Cast<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            issue.RelatedFixtures = issue.FixHints.Select(h => h.ExampleFixtureRef).Where(d => !string.IsNullOrWhiteSpace(d)).Cast<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        }

        report.Issues = report.Issues.OrderBy(i => i.Severity == "breaking" ? 0 : i.Severity == "warning" ? 1 : 2).ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
        report.GroupedIssues = new DiagnosticIssueGrouper().Group(report.Issues).ToList();
        report.TopRecommendedActions = report.Issues.SelectMany(i => i.FixHints.Select(h => h.SuggestedAction)).Distinct(StringComparer.Ordinal).Take(8).ToList();
        report.Status = report.BreakingCount > 0 ? "fail" : report.WarningCount > 0 ? "passWithWarnings" : "pass";
        report.Summary = new DiagnosticReportSummary
        {
            Status = report.Status,
            TotalIssues = report.IssueCount,
            BreakingIssues = report.BreakingCount,
            Warnings = report.WarningCount,
            TopCategories = report.Issues
                .GroupBy(issue => issue.Category, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Take(5)
                .Select(group => group.Key)
                .ToList(),
            TopSpecPaths = report.Issues
                .Select(issue => issue.SpecPath ?? issue.TemplatePath ?? issue.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .GroupBy(path => path, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Take(5)
                .Select(group => group.Key)
                .ToList()
        };
        report.ArtifactPaths = report.RelatedArtifacts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        report.MarkdownSummary = new DiagnosticReportMarkdownRenderer().Render(report);
        return report;
    }

    private static void AddIssue(
        DiagnosticReport report,
        string source,
        string id,
        string category,
        string severity,
        string title,
        string message,
        string? path,
        string? partName,
        string? fixtureId,
        string? baselineId,
        string? expected = null,
        string? actual = null)
    {
        report.Issues.Add(new DiagnosticIssue
        {
            Id = id,
            Source = source,
            Category = category,
            Severity = severity,
            Title = title,
            Message = message,
            Path = path,
            PartName = partName,
            FixtureId = fixtureId,
            BaselineId = baselineId,
            Expected = expected,
            Actual = actual,
            Evidence = [new DiagnosticEvidence { Kind = "message", Value = message }]
        });
    }

    private static string Classify(string code)
    {
        if (code.Contains("margin", StringComparison.OrdinalIgnoreCase)) return "pageSetup";
        if (code.Contains("toc", StringComparison.OrdinalIgnoreCase)) return "toc";
        if (code.Contains("footer", StringComparison.OrdinalIgnoreCase) || code.Contains("pageNumber", StringComparison.OrdinalIgnoreCase)) return "pageNumber";
        if (code.Contains("heading", StringComparison.OrdinalIgnoreCase)) return "heading";
        if (code.Contains("table", StringComparison.OrdinalIgnoreCase)) return "table";
        if (code.Contains("figure", StringComparison.OrdinalIgnoreCase)) return "figure";
        if (code.Contains("equation", StringComparison.OrdinalIgnoreCase)) return "equation";
        if (code.Contains("bibliography", StringComparison.OrdinalIgnoreCase)) return "bibliography";
        return "validation";
    }
}
