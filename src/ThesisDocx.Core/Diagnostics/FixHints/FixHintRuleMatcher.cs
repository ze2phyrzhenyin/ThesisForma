namespace ThesisDocx.Core.Diagnostics.FixHints;

public sealed class FixHintRuleMatcher
{
    public IReadOnlyList<FixHintRule> Match(DiagnosticIssue issue, IEnumerable<FixHintRule> rules)
    {
        return rules
            .Where(rule => IsMatch(issue, rule.Match))
            .OrderBy(rule => rule.HintId, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsMatch(DiagnosticIssue issue, FixHintRuleMatch match)
    {
        var codeMatched = ContainsOrEmpty($"{issue.Id} {issue.Message} {issue.Title}", match.Code);
        return ContainsOrEmpty(issue.Source, match.Source)
            && codeMatched
            && (string.IsNullOrWhiteSpace(match.Category) || ContainsOrEmpty(issue.Category, match.Category) || !string.IsNullOrWhiteSpace(match.Code))
            && ContainsOrEmpty(issue.Path ?? issue.SpecPath ?? issue.TemplatePath ?? string.Empty, match.PathPattern)
            && ContainsOrEmpty(issue.Severity, match.Severity);
    }

    private static bool ContainsOrEmpty(string value, string? pattern)
    {
        var normalizedValue = Normalize(value);
        var normalizedPattern = Normalize(pattern ?? string.Empty);
        return string.IsNullOrWhiteSpace(pattern)
            || value.Contains(pattern, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(normalizedPattern) && normalizedValue.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase))
            || Tokens(pattern).All(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }

    private static IEnumerable<string> Tokens(string value)
    {
        var split = string.Concat(value.Select((ch, index) =>
            index > 0 && char.IsUpper(ch) && char.IsLetterOrDigit(value[index - 1]) ? $" {ch}" : ch.ToString()));
        return split.Split([' ', '.', '_', '-', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 2);
    }
}
