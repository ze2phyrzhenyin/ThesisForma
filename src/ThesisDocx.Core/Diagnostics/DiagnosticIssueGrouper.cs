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
                    Severity = group.Any(issue => UnifiedDiagnosticMapper.IsError(issue.Severity))
                        ? DiagnosticSeverity.Error
                        : group.Any(issue => UnifiedDiagnosticMapper.IsWarning(issue.Severity))
                            ? DiagnosticSeverity.Warning
                            : DiagnosticSeverity.Info,
                    Count = group.Count(),
                    Issues = group.OrderBy(issue => issue.Id, StringComparer.Ordinal).ToList()
                };
            })
            .OrderBy(group => UnifiedDiagnosticMapper.SeveritySortRank(group.Severity))
            .ThenBy(group => group.GroupKey, StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildKey(DiagnosticIssue issue)
    {
        var path = issue.SpecPath ?? issue.TemplatePath ?? issue.Path ?? issue.PartName ?? "unknown";
        return $"{issue.Category}:{UnifiedDiagnosticMapper.NormalizeSeverity(issue.Severity)}:{path}";
    }
}
