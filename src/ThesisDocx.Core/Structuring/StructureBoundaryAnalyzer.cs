using System.Text.RegularExpressions;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Extraction;

namespace ThesisDocx.Core.Structuring;

public sealed class StructureBoundaryAnalyzer
{
    private static readonly Regex ChineseChapterPattern = new(@"^第(?<number>[一二三四五六七八九十百千万两0-9]+)(?<unit>[章节篇])\s*(?<title>.*)$", RegexOptions.CultureInvariant);

    public StructureAnalysisReport Analyze(DocxExtractionResult extraction, ThesisStructuringResult structured)
    {
        var report = new StructureAnalysisReport();
        if (string.Equals(extraction.FormatChaos.ChaosLevel, "high", StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(report, "structure.formatChaos.high", DiagnosticSeverity.Warning, "document", "Source formatting is highly fragmented; chapter and heading detection is less reliable.", "Run Codex review or perform manual structure review.", 0.85);
        }

        foreach (var unresolved in structured.UnresolvedItems.Where(item => item.Code.Contains("heading", StringComparison.OrdinalIgnoreCase)))
        {
            AddIssue(report, "structure.heading.unresolved", unresolved.Severity, unresolved.EvidencePath, unresolved.Message, unresolved.RecommendedAction, 0.8);
        }

        AnalyzeChapterSequence(extraction, structured, report);
        AnalyzeUnmappedHeadingCandidates(extraction, structured, report);
        Finish(report);
        return report;
    }

    private static void AnalyzeChapterSequence(DocxExtractionResult extraction, ThesisStructuringResult structured, StructureAnalysisReport report)
    {
        var chapters = extraction.Paragraphs
            .Select(paragraph => (Paragraph: paragraph, Match: ChineseChapterPattern.Match(paragraph.Text.Trim())))
            .Where(item => item.Match.Success)
            .Select(item => new ChapterCandidate(
                item.Paragraph.EvidencePath,
                item.Paragraph.Text.Trim(),
                ToChapterNumber(item.Match.Groups["number"].Value),
                IsMappedAsHeading(structured, item.Paragraph.EvidencePath)))
            .Where(candidate => candidate.Number > 0)
            .OrderBy(candidate => EvidenceIndex(candidate.EvidencePath))
            .ToList();

        if (chapters.Count == 0)
        {
            return;
        }

        var previous = 0;
        var seen = new HashSet<int>();
        foreach (var chapter in chapters)
        {
            if (!chapter.MappedAsHeading)
            {
                AddIssue(report, "structure.chapterHeading.notMapped", DiagnosticSeverity.Warning, chapter.EvidencePath, $"Chapter heading '{chapter.Text}' was not mapped as a heading block.", "Move this evidence into a heading block or add an unresolved item explaining why it is not a heading.", 0.86);
            }

            if (!seen.Add(chapter.Number))
            {
                AddIssue(report, "structure.chapterSequence.duplicate", DiagnosticSeverity.Warning, chapter.EvidencePath, $"Chapter number {chapter.Number} appears more than once.", "Confirm whether this is an appendix/title duplicate or a structure split error.", 0.82);
            }

            if (previous > 0 && chapter.Number < previous)
            {
                AddIssue(report, "structure.chapterSequence.decrease", DiagnosticSeverity.Error, chapter.EvidencePath, $"Chapter number {chapter.Number} appears after chapter {previous}.", "Repair chapter ordering or move the affected blocks to the correct heading boundary.", 0.9);
            }

            if (previous > 0 && chapter.Number > previous + 1)
            {
                AddIssue(report, "structure.chapterSequence.gap", DiagnosticSeverity.Warning, chapter.EvidencePath, $"Chapter number jumps from {previous} to {chapter.Number}.", "Check whether one or more chapter headings were missed or mapped as body text.", 0.78);
            }

            previous = Math.Max(previous, chapter.Number);
        }
    }

    private static void AnalyzeUnmappedHeadingCandidates(DocxExtractionResult extraction, ThesisStructuringResult structured, StructureAnalysisReport report)
    {
        var mappedHeadingEvidence = structured.EvidenceLinks
            .Where(link => link.Reason.Contains("heading", StringComparison.OrdinalIgnoreCase))
            .Select(link => link.EvidencePath)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var candidate in extraction.PossibleHeadings
            .Where(candidate => candidate.Confidence >= 0.65)
            .Where(candidate => !mappedHeadingEvidence.Contains(candidate.EvidencePath)))
        {
            AddIssue(report, "structure.headingCandidate.unmapped", DiagnosticSeverity.Warning, candidate.EvidencePath, $"Heading candidate '{candidate.Text}' is not mapped as a heading.", "Review the candidate and either map it as a heading or record uncertainty.", candidate.Confidence);
        }
    }

    private static bool IsMappedAsHeading(ThesisStructuringResult structured, string evidencePath)
    {
        return structured.EvidenceLinks.Any(link =>
            string.Equals(link.EvidencePath, evidencePath, StringComparison.Ordinal)
            && link.Reason.Contains("heading", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddIssue(StructureAnalysisReport report, string code, string severity, string evidencePath, string message, string action, double confidence)
    {
        report.Issues.Add(new StructureAnalysisIssue
        {
            Code = code,
            Severity = string.IsNullOrWhiteSpace(severity) ? DiagnosticSeverity.Warning : severity,
            EvidencePath = string.IsNullOrWhiteSpace(evidencePath) ? "document" : evidencePath,
            Message = message,
            RecommendedAction = action,
            Confidence = Math.Round(Math.Clamp(confidence, 0, 1), 3)
        });
    }

    private static void Finish(StructureAnalysisReport report)
    {
        report.IssueCount = report.Issues.Count;
        var hasError = report.Issues.Any(issue => UnifiedDiagnosticMapper.IsError(issue.Severity));
        var hasHighSignal = report.Issues.Any(issue => issue.Code.Contains("chapter", StringComparison.OrdinalIgnoreCase)
            || issue.Code == "structure.formatChaos.high");
        report.RiskLevel = hasError || hasHighSignal ? "high" : report.Issues.Count > 0 ? "medium" : "low";
        report.Status = hasError ? "fail" : report.Issues.Count > 0 ? "passWithWarnings" : "pass";
        report.QualityScore = QualityScore(report);
        report.RecommendCodexReview = report.RiskLevel is "high" or "medium";
        report.RecommendedActions = report.RecommendCodexReview
            ? ["Run Codex structure review or manually repair evidence-backed structure boundaries before final rendering."]
            : ["Continue with normal validation and rendering."];
    }

    private static int QualityScore(StructureAnalysisReport report)
    {
        var score = 100;
        score -= report.Issues.Count(issue => UnifiedDiagnosticMapper.IsError(issue.Severity)) * 20;
        score -= report.Issues.Count(issue => UnifiedDiagnosticMapper.IsWarning(issue.Severity)) * 8;
        score -= report.Issues.Count(issue => issue.Code.Contains("chapter", StringComparison.OrdinalIgnoreCase)) * 10;
        score -= report.Issues.Count(issue => issue.Code.Contains("heading", StringComparison.OrdinalIgnoreCase)) * 5;
        return Math.Clamp(score, 0, 100);
    }

    private static int EvidenceIndex(string evidencePath)
    {
        var match = Regex.Match(evidencePath, @"\[(?<index>[0-9]+)\]", RegexOptions.CultureInvariant);
        return match.Success ? int.Parse(match.Groups["index"].Value, System.Globalization.CultureInfo.InvariantCulture) : int.MaxValue;
    }

    private static int ToChapterNumber(string value)
    {
        if (int.TryParse(value, out var numeric))
        {
            return numeric;
        }

        var total = 0;
        var current = 0;
        foreach (var ch in value)
        {
            var digit = ch switch
            {
                '一' => 1,
                '二' or '两' => 2,
                '三' => 3,
                '四' => 4,
                '五' => 5,
                '六' => 6,
                '七' => 7,
                '八' => 8,
                '九' => 9,
                _ => 0
            };
            if (digit > 0)
            {
                current = digit;
                continue;
            }

            if (ch == '十')
            {
                total += current == 0 ? 10 : current * 10;
                current = 0;
            }
            else if (ch == '百')
            {
                total += current == 0 ? 100 : current * 100;
                current = 0;
            }
        }

        return total + current;
    }

    private sealed record ChapterCandidate(string EvidencePath, string Text, int Number, bool MappedAsHeading);
}
