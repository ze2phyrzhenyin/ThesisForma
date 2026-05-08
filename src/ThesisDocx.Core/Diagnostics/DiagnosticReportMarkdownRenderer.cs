using System.Text;

namespace ThesisDocx.Core.Diagnostics;

public sealed class DiagnosticReportMarkdownRenderer
{
    public string Render(DiagnosticReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Template Diagnostic Report");
        builder.AppendLine();
        builder.AppendLine($"Status: **{report.Status}**");
        builder.AppendLine($"Issues: {report.IssueCount} total, {report.BreakingCount} errors, {report.WarningCount} warnings");
        builder.AppendLine();

        if (report.TopRecommendedActions.Count > 0)
        {
            builder.AppendLine("## Top Actions");
            foreach (var action in report.TopRecommendedActions.Take(8))
            {
                builder.AppendLine($"- {action}");
            }

            builder.AppendLine();
        }

        if (report.Issues.Count == 0)
        {
        builder.AppendLine("No error-level diagnostic issues were found.");
            return builder.ToString();
        }

        builder.AppendLine("## Errors");
        foreach (var issue in report.Issues.Where(issue => UnifiedDiagnosticMapper.IsError(issue.Severity)).Take(12))
        {
            RenderIssue(builder, issue);
        }

        var warnings = report.Issues.Where(issue => UnifiedDiagnosticMapper.IsWarning(issue.Severity)).Take(8).ToList();
        if (warnings.Count > 0)
        {
            builder.AppendLine("## Warnings");
            foreach (var warning in warnings)
            {
                RenderIssue(builder, warning);
            }
        }

        return builder.ToString();
    }

    private static void RenderIssue(StringBuilder builder, DiagnosticIssue issue)
    {
        builder.AppendLine($"- **{issue.Title}** (`{issue.Id}`)");
        builder.AppendLine($"  - Category: `{issue.Category}`; severity: `{issue.Severity}`");
        if (!string.IsNullOrWhiteSpace(issue.Path))
        {
            builder.AppendLine($"  - Path: `{issue.Path}`");
        }

        if (issue.FixHints.Count > 0)
        {
            builder.AppendLine($"  - Suggested fix: {issue.FixHints[0].SuggestedAction}");
            builder.AppendLine($"  - Hint: `{issue.FixHints[0].HintId}`");
        }
    }
}
