using System.Text;

namespace ThesisDocx.Core.Ci;

public sealed class CiQualityMarkdownRenderer
{
    public string Render(CiQualityReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# CI Template Quality Report");
        builder.AppendLine();
        builder.AppendLine($"Status: **{report.Status}**");
        builder.AppendLine($"Merge decision: **{report.MergeDecision}**");
        builder.AppendLine($"Quality score: **{report.QualityScore}/100**");
        builder.AppendLine();
        builder.AppendLine("## Checks");
        foreach (var check in report.Checks.OrderBy(check => check.Code, StringComparer.Ordinal))
        {
            builder.AppendLine($"- `{check.Status}` **{check.Name}** (`{check.Code}`): {check.Message}");
        }

        if (report.RecommendedNextActions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Recommended Actions");
            foreach (var action in report.RecommendedNextActions.Take(8))
            {
                builder.AppendLine($"- {action}");
            }
        }

        return builder.ToString();
    }
}
