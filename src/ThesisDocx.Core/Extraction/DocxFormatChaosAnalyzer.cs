using System.Globalization;
using System.Text.RegularExpressions;

namespace ThesisDocx.Core.Extraction;

internal sealed class DocxFormatChaosAnalysis
{
    public ExtractedFormatChaosReport Report { get; set; } = new();
    public List<ExtractedFormatCluster> Clusters { get; set; } = [];
}

internal sealed class DocxFormatChaosAnalyzer
{
    public DocxFormatChaosAnalysis Analyze(DocxExtractionResult result)
    {
        var nonEmpty = result.Paragraphs
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph.Text))
            .ToList();
        var signatureByValue = result.FormatSignatures
            .Where(signature => !string.IsNullOrWhiteSpace(signature.Signature))
            .ToDictionary(signature => signature.Signature, signature => signature.Id, StringComparer.Ordinal);
        var clusters = BuildClusters(nonEmpty, signatureByValue);
        var report = BuildReport(result, nonEmpty);
        AddDiagnostics(report);

        return new DocxFormatChaosAnalysis
        {
            Report = report,
            Clusters = clusters
        };
    }

    private static ExtractedFormatChaosReport BuildReport(DocxExtractionResult result, IReadOnlyList<ExtractedParagraph> nonEmpty)
    {
        var bodyCandidates = nonEmpty.Where(paragraph => RoleHint(paragraph) == "body").ToList();
        var headingCandidates = nonEmpty.Where(paragraph => RoleHint(paragraph) == "heading").ToList();
        var signatureDensity = Ratio(result.FormatSignatures.Count, Math.Max(1, nonEmpty.Count));
        var directParagraphRatio = Ratio(nonEmpty.Count(paragraph => paragraph.EffectiveFormat.HasDirectParagraphFormatting), nonEmpty.Count);
        var directRunRatio = Ratio(nonEmpty.Count(paragraph => paragraph.EffectiveFormat.HasDirectRunFormatting), nonEmpty.Count);
        var unstyledRatio = Ratio(nonEmpty.Count(paragraph => string.IsNullOrWhiteSpace(paragraph.EffectiveFormat.StyleId)), nonEmpty.Count);
        var bodySignatureCount = bodyCandidates
            .Select(paragraph => paragraph.EffectiveFormat.Signature)
            .Where(signature => !string.IsNullOrWhiteSpace(signature))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var bodyFragmentation = Ratio(bodySignatureCount, Math.Max(1, bodyCandidates.Count));
        var chaosScore = Clamp01(
            signatureDensity * 0.30
            + directParagraphRatio * 0.25
            + directRunRatio * 0.20
            + unstyledRatio * 0.15
            + bodyFragmentation * 0.10);

        return new ExtractedFormatChaosReport
        {
            ChaosScore = RoundRatio(chaosScore),
            ChaosLevel = ChaosLevel(chaosScore),
            NonEmptyParagraphCount = nonEmpty.Count,
            FormatSignatureCount = result.FormatSignatures.Count,
            FormatSignatureDensity = RoundRatio(signatureDensity),
            DirectParagraphFormattingRatio = RoundRatio(directParagraphRatio),
            DirectRunFormattingRatio = RoundRatio(directRunRatio),
            UnstyledParagraphRatio = RoundRatio(unstyledRatio),
            BodyCandidateParagraphCount = bodyCandidates.Count,
            BodyCandidateSignatureCount = bodySignatureCount,
            HeadingCandidateCount = headingCandidates.Count,
            HeadingWithoutHeadingStyleCount = headingCandidates.Count(HeadingLacksHeadingStyle),
            EmptyParagraphCount = result.Paragraphs.Count(paragraph => string.IsNullOrWhiteSpace(paragraph.Text))
        };
    }

    private static List<ExtractedFormatCluster> BuildClusters(
        IReadOnlyList<ExtractedParagraph> paragraphs,
        IReadOnlyDictionary<string, string> signatureByValue)
    {
        return paragraphs
            .GroupBy(ClusterKey, StringComparer.Ordinal)
            .Select((group, index) => BuildCluster($"cluster-{index}", group.ToList(), signatureByValue))
            .OrderByDescending(cluster => cluster.UsageCount)
            .ThenBy(cluster => cluster.RoleHint, StringComparer.Ordinal)
            .ThenBy(cluster => cluster.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static ExtractedFormatCluster BuildCluster(
        string id,
        IReadOnlyList<ExtractedParagraph> paragraphs,
        IReadOnlyDictionary<string, string> signatureByValue)
    {
        var representative = paragraphs
            .OrderByDescending(paragraph => paragraph.Text.Length)
            .ThenBy(paragraph => paragraph.Index)
            .First();
        var signatureIds = paragraphs
            .Select(paragraph => paragraph.EffectiveFormat.Signature)
            .Where(signature => !string.IsNullOrWhiteSpace(signature) && signatureByValue.ContainsKey(signature))
            .Select(signature => signatureByValue[signature])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var roleHint = RoleHint(representative);
        var variance = Variance(paragraphs);
        var cluster = new ExtractedFormatCluster
        {
            Id = id,
            RoleHint = roleHint,
            Confidence = ClusterConfidence(roleHint, paragraphs.Count, variance.Count, representative),
            UsageCount = paragraphs.Count,
            SignatureIds = signatureIds,
            EvidencePaths = paragraphs.Select(paragraph => paragraph.EvidencePath).Take(25).ToList(),
            RepresentativeFormat = CloneFormat(representative.EffectiveFormat),
            Variance = variance
        };

        if (signatureIds.Count >= 4)
        {
            cluster.Diagnostics.Add(Issue(
                "format.cluster.fragmented",
                roleHint == "body" ? "warning" : "info",
                $"Cluster '{id}' groups {signatureIds.Count} exact format signatures; review before using it as a template rule.",
                cluster.EvidencePaths.FirstOrDefault() ?? "document"));
        }

        if (variance.Count >= 3)
        {
            cluster.Diagnostics.Add(Issue(
                "format.cluster.mixedEvidence",
                "info",
                $"Cluster '{id}' contains mixed {string.Join(", ", variance)} evidence.",
                cluster.EvidencePaths.FirstOrDefault() ?? "document"));
        }

        return cluster;
    }

    private static void AddDiagnostics(ExtractedFormatChaosReport report)
    {
        if (report.NonEmptyParagraphCount == 0)
        {
            report.Diagnostics.Add(Issue("format.document.empty", "warning", "No non-empty paragraphs were available for format analysis.", "document"));
            return;
        }

        if (report.FormatSignatureDensity >= 0.35 && report.NonEmptyParagraphCount >= 10)
        {
            report.Diagnostics.Add(Issue("format.signatures.fragmented", "warning", "The document has many distinct effective paragraph formats relative to its paragraph count.", "document"));
        }

        if (report.DirectParagraphFormattingRatio >= 0.35)
        {
            report.Diagnostics.Add(Issue("format.directParagraph.high", "warning", "Many paragraphs use direct paragraph formatting; inferred template rules should be reviewed.", "document"));
        }

        if (report.DirectRunFormattingRatio >= 0.30)
        {
            report.Diagnostics.Add(Issue("format.directRun.high", "warning", "Many paragraphs use direct run formatting; mixed emphasis may be content, not template formatting.", "document"));
        }

        if (report.UnstyledParagraphRatio >= 0.40)
        {
            report.Diagnostics.Add(Issue("format.styles.unstyled", "warning", "Many paragraphs have no paragraph style id, so structure mapping must rely on text and effective formatting evidence.", "document"));
        }

        if (report.BodyCandidateSignatureCount >= 4 && report.BodyCandidateParagraphCount >= 8)
        {
            report.Diagnostics.Add(Issue("format.body.fragmented", "warning", "Body-like paragraphs are split across several exact signatures; use clusters instead of exact signatures for draft template inference.", "document"));
        }

        if (report.HeadingCandidateCount > 0
            && Ratio(report.HeadingWithoutHeadingStyleCount, report.HeadingCandidateCount) >= 0.50)
        {
            report.Diagnostics.Add(Issue("format.heading.styleWeak", "warning", "Many heading candidates do not use a heading-style name, so heading levels need review.", "document"));
        }

        if (report.EmptyParagraphCount >= Math.Max(5, report.NonEmptyParagraphCount / 5))
        {
            report.Diagnostics.Add(Issue("format.emptyParagraphs.frequent", "info", "The document contains many empty paragraphs; some may be visual spacing rather than content.", "document"));
        }
    }

    private static string ClusterKey(ExtractedParagraph paragraph)
    {
        var format = paragraph.EffectiveFormat;
        var role = RoleHint(paragraph);
        var parts = new[]
        {
            role,
            FontKey(format.EastAsiaFont),
            FontKey(format.Font),
            NumberBucket(format.FontSizePt, 0.5),
            BoolKey(format.Bold),
            BoolKey(format.Italic),
            format.Alignment ?? string.Empty,
            TwipsBucket(format.FirstLineIndentTwips, 120),
            TwipsBucket(format.HangingIndentTwips, 120),
            TwipsBucket(format.LeftIndentTwips, 120),
            format.LineSpacingRule ?? string.Empty,
            TwipsBucket(ParseTwips(format.LineSpacing), 20),
            role == "heading" ? (format.OutlineLevel?.ToString(CultureInfo.InvariantCulture) ?? string.Empty) : string.Empty,
            role == "heading" ? NumberBucket(format.NumberingLevel, 1) : string.Empty,
            role == "bibliography" ? TwipsBucket(format.HangingIndentTwips, 120) : string.Empty
        };

        return string.Join("|", parts);
    }

    private static List<string> Variance(IReadOnlyList<ExtractedParagraph> paragraphs)
    {
        var variance = new List<string>();
        AddVariance(variance, "styleId", paragraphs.Select(paragraph => paragraph.EffectiveFormat.StyleId));
        AddVariance(variance, "lineSpacing", paragraphs.Select(paragraph => paragraph.EffectiveFormat.LineSpacing));
        AddVariance(variance, "fontSizePt", paragraphs.Select(paragraph => paragraph.EffectiveFormat.FontSizePt?.ToString("0.###", CultureInfo.InvariantCulture)));
        AddVariance(variance, "directParagraphFormatting", paragraphs.Select(paragraph => paragraph.EffectiveFormat.HasDirectParagraphFormatting.ToString(CultureInfo.InvariantCulture)));
        AddVariance(variance, "directRunFormatting", paragraphs.Select(paragraph => paragraph.EffectiveFormat.HasDirectRunFormatting.ToString(CultureInfo.InvariantCulture)));
        return variance;
    }

    private static void AddVariance(List<string> variance, string name, IEnumerable<string?> values)
    {
        if (values.Select(value => value ?? string.Empty).Distinct(StringComparer.Ordinal).Take(2).Count() > 1)
        {
            variance.Add(name);
        }
    }

    private static string RoleHint(ExtractedParagraph paragraph)
    {
        var text = paragraph.Text.Trim();
        if (Regex.IsMatch(text, @"^(图|表|Figure|Table)\s*[0-9一二三四五六七八九十]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return "caption";
        }

        if (paragraph.PossibleRole == "bibliographyCandidate"
            || Regex.IsMatch(text, @"^\[[0-9]+\]", RegexOptions.CultureInvariant))
        {
            return "bibliography";
        }

        if (paragraph.PossibleRole is "abstractCandidate" or "keywordsCandidate" or "appendixCandidate")
        {
            return "frontMatter";
        }

        if (paragraph.PossibleRole == "headingCandidate"
            || paragraph.EffectiveFormat.OutlineLevel is >= 0 and <= 5
            || IsHeadingStyle(paragraph.EffectiveFormat.StyleName))
        {
            return "heading";
        }

        return "body";
    }

    private static bool HeadingLacksHeadingStyle(ExtractedParagraph paragraph)
    {
        return !IsHeadingStyle(paragraph.StyleName) && !IsHeadingStyle(paragraph.EffectiveFormat.StyleName);
    }

    private static bool IsHeadingStyle(string? styleName)
    {
        return styleName?.Contains("heading", StringComparison.OrdinalIgnoreCase) == true
            || styleName?.Contains("标题", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static double ClusterConfidence(string roleHint, int usageCount, int varianceCount, ExtractedParagraph representative)
    {
        var confidence = roleHint switch
        {
            "heading" => representative.EffectiveFormat.OutlineLevel is not null || IsHeadingStyle(representative.EffectiveFormat.StyleName) ? 0.86 : 0.68,
            "body" => usageCount >= 3 ? 0.78 : 0.62,
            "bibliography" => 0.74,
            "caption" => 0.76,
            "frontMatter" => 0.70,
            _ => 0.55
        };

        confidence -= Math.Min(0.18, varianceCount * 0.04);
        return RoundRatio(Math.Clamp(confidence, 0.1, 0.95));
    }

    private static ExtractedEffectiveFormat CloneFormat(ExtractedEffectiveFormat source)
    {
        return new ExtractedEffectiveFormat
        {
            Signature = source.Signature,
            StyleId = source.StyleId,
            StyleName = source.StyleName,
            StyleChain = source.StyleChain.ToList(),
            Font = source.Font,
            EastAsiaFont = source.EastAsiaFont,
            FontSizePt = source.FontSizePt,
            Bold = source.Bold,
            Italic = source.Italic,
            Alignment = source.Alignment,
            LeftIndentTwips = source.LeftIndentTwips,
            RightIndentTwips = source.RightIndentTwips,
            FirstLineIndentTwips = source.FirstLineIndentTwips,
            HangingIndentTwips = source.HangingIndentTwips,
            SpaceBeforeTwips = source.SpaceBeforeTwips,
            SpaceAfterTwips = source.SpaceAfterTwips,
            LineSpacing = source.LineSpacing,
            LineSpacingRule = source.LineSpacingRule,
            OutlineLevel = source.OutlineLevel,
            NumberingId = source.NumberingId,
            NumberingLevel = source.NumberingLevel,
            NumberingFormat = source.NumberingFormat,
            NumberingText = source.NumberingText,
            HasDirectParagraphFormatting = source.HasDirectParagraphFormatting,
            HasDirectRunFormatting = source.HasDirectRunFormatting,
            Sources = source.Sources.ToList()
        };
    }

    private static ExtractionIssue Issue(string code, string severity, string message, string evidencePath)
    {
        return new ExtractionIssue
        {
            Code = code,
            Severity = severity,
            Message = message,
            EvidencePath = evidencePath
        };
    }

    private static string FontKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    }

    private static string BoolKey(bool? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string NumberBucket(double? value, double step)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        return (Math.Round(value.Value / step, MidpointRounding.AwayFromZero) * step).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string NumberBucket(int? value, int step)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        return (Math.Round(value.Value / (double)step, MidpointRounding.AwayFromZero) * step).ToString("0", CultureInfo.InvariantCulture);
    }

    private static string TwipsBucket(int? value, int step)
    {
        return NumberBucket(value, step);
    }

    private static int? ParseTwips(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static double Ratio(int numerator, int denominator)
    {
        return denominator <= 0 ? 0 : numerator / (double)denominator;
    }

    private static double RoundRatio(double value)
    {
        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0, 1);
    }

    private static string ChaosLevel(double score)
    {
        if (score >= 0.60) return "high";
        if (score >= 0.30) return "medium";
        return "low";
    }
}
