using ThesisDocx.Core.Extraction;

namespace ThesisDocx.Core.Templates.Authoring;

public sealed class FormatCandidateDecisionScaffolder
{
    public FormatCandidateDecisionSet Scaffold(
        DocxFormatCandidateReport candidateReport,
        string candidateFormatSpecPath,
        string candidateReportPath,
        string reviewer,
        FormatCandidateDecisionKind defaultDecision = FormatCandidateDecisionKind.Reject)
    {
        return new FormatCandidateDecisionSet
        {
            SourceCandidateFormatSpec = candidateFormatSpecPath,
            SourceCandidateReport = candidateReportPath,
            Reviewer = reviewer,
            ReviewedAt = string.Empty,
            RiskAccepted = false,
            RiskAcceptanceReason = string.Empty,
            Decisions = candidateReport.GeneratedFields
                .OrderBy(field => field.Path, StringComparer.Ordinal)
                .Select(field => new FormatCandidateDecision
                {
                    FieldPath = field.Path,
                    Decision = defaultDecision,
                    Reason = defaultDecision == FormatCandidateDecisionKind.Reject
                        ? "Review required before accepting this candidate field."
                        : "Accepted after reviewing the candidate evidence.",
                    EvidencePaths = field.EvidencePaths.ToList()
                })
                .ToList()
        };
    }
}
