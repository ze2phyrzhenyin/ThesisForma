using System.Text;

namespace ThesisDocx.Core.Templates.Authoring;

public sealed class TemplateAuthoringMarkdownRenderer
{
    public string Render(TemplateAuthoringReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Template Authoring Report");
        builder.AppendLine();
        builder.AppendLine($"Template: **{report.TemplateId}** `{report.TemplateVersion}`");
        builder.AppendLine($"Readiness: **{report.PublishReadiness}**");
        builder.AppendLine($"Merge decision: **{report.SuggestedMergeDecision}**");
        builder.AppendLine($"Quality score: **{report.QualityScore}/100**");
        builder.AppendLine();

        if (report.ReadinessReasons.Count > 0)
        {
            builder.AppendLine("## Readiness Reasons");
            foreach (var reason in report.ReadinessReasons)
            {
                builder.AppendLine($"- {reason}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Checklist");
        foreach (var item in report.Checklist.OrderBy(item => item.Code, StringComparer.Ordinal))
        {
            builder.AppendLine($"- `{item.Status}` **{item.Title}** (`{item.Code}`)");
        }

        if (report.RecommendedNextActions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Next Actions");
            foreach (var action in report.RecommendedNextActions.Take(8))
            {
                builder.AppendLine($"- {action}");
            }
        }

        return builder.ToString();
    }
}
