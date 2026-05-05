using ThesisDocx.Core.Diagnostics.FixHints;

namespace ThesisDocx.Core.Diagnostics;

public sealed class FixHintEngine
{
    private readonly FixHintRuleCatalog _catalog;
    private readonly FixHintRuleMatcher _matcher = new();

    public FixHintEngine()
        : this(FixHintRuleCatalog.LoadDefault())
    {
    }

    public FixHintEngine(FixHintRuleCatalog catalog)
    {
        _catalog = catalog;
    }

    public IReadOnlyList<FixHint> Suggest(DiagnosticIssue issue)
    {
        var matches = _matcher.Match(issue, _catalog.Rules);
        if (matches.Count > 0)
        {
            return matches.Select(ToHint).ToList();
        }

        return
        [
            new FixHint
            {
                HintId = "fix.review",
                Title = "Review diagnostic",
                Description = "Inspect the issue path and related artifacts.",
                SuggestedSpecPath = issue.SpecPath,
                SuggestedTemplatePath = issue.TemplatePath,
                SuggestedAction = "Open the related artifact and update the mapped rule or baseline after review.",
                Confidence = "low",
                DocsRef = "docs/19-template-diagnostics.md"
            }
        ];
    }

    private static FixHint ToHint(FixHintRule rule)
    {
        return new FixHint
        {
            HintId = rule.HintId,
            Title = rule.Title,
            Description = rule.Description,
            SuggestedSpecPath = rule.SuggestedSpecPath,
            SuggestedTemplatePath = rule.SuggestedTemplatePath,
            SuggestedAction = rule.SuggestedAction,
            Confidence = rule.Confidence,
            DocsRef = rule.DocsRef,
            ExampleFixtureRef = rule.ExampleFixtureRef
        };
    }
}
