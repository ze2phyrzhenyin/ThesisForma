namespace ThesisDocx.Core.Diagnostics;

public sealed class DiagnosticIssueGrouper
{
    public IReadOnlyList<DiagnosticIssueGroup> Group(IEnumerable<DiagnosticIssue> issues)
    {
        return issues
            .GroupBy(issue => BuildKey(issue), StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.OrderBy(issue => issue.Id, StringComparer.Ordinal).First();
                return new DiagnosticIssueGroup
                {
                    GroupKey = group.Key,
                    Category = first.Category,
                    Severity = group.Any(issue => issue.Severity == "breaking") ? "breaking" : group.Any(issue => issue.Severity == "warning") ? "warning" : "info",
                    Count = group.Count(),
                    Issues = group.OrderBy(issue => issue.Id, StringComparer.Ordinal).ToList()
                };
            })
            .OrderBy(group => group.Severity == "breaking" ? 0 : group.Severity == "warning" ? 1 : 2)
            .ThenBy(group => group.GroupKey, StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildKey(DiagnosticIssue issue)
    {
        var path = issue.SpecPath ?? issue.TemplatePath ?? issue.Path ?? issue.PartName ?? "unknown";
        return $"{issue.Category}:{issue.Severity}:{path}";
    }
}
