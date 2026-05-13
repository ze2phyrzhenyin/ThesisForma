using System.Globalization;
using System.Text.RegularExpressions;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Extraction;

public sealed class DocxFormatCandidateGenerator
{
    public DocxFormatCandidateResult Generate(DocxExtractionResult extraction, string sourceExtraction = "")
    {
        var report = new DocxFormatCandidateReport
        {
            SourceExtraction = sourceExtraction,
            CandidateFormatSpecName = CandidateName(extraction),
            ChaosLevel = extraction.FormatChaos.ChaosLevel,
            ChaosScore = extraction.FormatChaos.ChaosScore,
            Warnings = extraction.FormatChaos.Diagnostics.Select(issue => $"{issue.Code}: {issue.Message}").ToList(),
            RecommendedReviewSteps =
            [
                "Review generated fields against extraction evidence before treating this as a template.",
                "Use format clusters as hints only; do not accept exact signatures from messy documents without human review.",
                "Confirm body, heading, bibliography, note, table, and page setup rules against institution requirements.",
                "Run template validate, render, format conformance, and regression gates after copying accepted fields into a TemplatePackage."
            ]
        };
        if (extraction.FormatChaos.ChaosLevel == "high")
        {
            AddUnresolved(report, "format.chaos.highReviewRequired", "warning", "Extraction has high format chaos; generated fields are draft evidence only.", ["document"], "Review clusters and source requirements before accepting any candidate field.");
        }

        var spec = new ThesisFormatSpec
        {
            SchemaVersion = ThesisSchemaVersions.Version120,
            Name = report.CandidateFormatSpecName
        };

        ApplyPageSetup(extraction, spec, report);
        var bodyCluster = ChooseBodyCluster(extraction.FormatClusters);
        if (bodyCluster is null)
        {
            AddUnresolved(report, "format.body.noCluster", "warning", "No body-like format cluster was available.", ["document"], "Manually set $.defaultFont and $.bodyParagraph from source requirements.");
        }
        else
        {
            UseCluster(report, bodyCluster, "$.bodyParagraph");
            ApplyDefaultFont(spec.DefaultFont, bodyCluster, report);
            ApplyParagraph(spec.BodyParagraph, bodyCluster, spec.DefaultFont, "$.bodyParagraph", report);
        }

        ApplyHeadingCandidates(extraction, spec, report);
        ApplyBibliographyCandidate(extraction, spec, report);
        AddUnsupportedSurfaceUnresolved(extraction, report);

        report.GeneratedFieldCount = report.GeneratedFields.Count;
        report.CandidateStatus = report.UnresolvedItems.Any(item => item.Severity == "warning")
            ? "needsReview"
            : "draft";

        return new DocxFormatCandidateResult
        {
            CandidateFormatSpec = spec,
            Report = report
        };
    }

    public static string ToMarkdown(DocxFormatCandidateReport report)
    {
        var lines = new List<string>
        {
            "# DOCX Format Candidate Report",
            string.Empty,
            $"- Source extraction: `{report.SourceExtraction}`",
            $"- Candidate spec: `{report.CandidateFormatSpecName}`",
            $"- Status: `{report.CandidateStatus}`",
            $"- Format chaos: `{report.ChaosLevel}` ({report.ChaosScore.ToString("0.###", CultureInfo.InvariantCulture)})",
            $"- Generated fields: {report.GeneratedFieldCount}",
            $"- Unresolved items: {report.UnresolvedItems.Count}",
            string.Empty,
            "## Generated Fields"
        };

        foreach (var field in report.GeneratedFields)
        {
            lines.Add($"- `{field.Path}` = `{field.Value}` from `{field.SourceClusterId}` confidence={field.Confidence.ToString("0.###", CultureInfo.InvariantCulture)}");
        }

        lines.Add(string.Empty);
        lines.Add("## Unresolved");
        foreach (var item in report.UnresolvedItems)
        {
            lines.Add($"- `{item.Severity}` `{item.Code}` {item.Message}");
        }

        lines.Add(string.Empty);
        lines.Add("## Review Steps");
        foreach (var step in report.RecommendedReviewSteps)
        {
            lines.Add($"- {step}");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void ApplyPageSetup(DocxExtractionResult extraction, ThesisFormatSpec spec, DocxFormatCandidateReport report)
    {
        var section = extraction.Sections.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.PageSize) || !string.IsNullOrWhiteSpace(s.Margins));
        if (section is null)
        {
            AddUnresolved(report, "format.pageSetup.noEvidence", "info", "No section page setup evidence was available.", ["document"], "Confirm page setup manually.");
            return;
        }

        var evidence = new[] { section.EvidencePath };
        var width = ParseNamedInt(section.PageSize, "w");
        var height = ParseNamedInt(section.PageSize, "h");
        if (width.HasValue && height.HasValue)
        {
            spec.PageSetup.PaperSize = IsA4(width.Value, height.Value) ? PaperSizeKind.A4 : spec.PageSetup.PaperSize;
            spec.PageSetup.Orientation = width.Value > height.Value ? PageOrientationKind.Landscape : PageOrientationKind.Portrait;
            AddField(report, "$.pageSetup.paperSize", spec.PageSetup.PaperSize.ToString(), "section-page-setup", 0.65, evidence, "Page size inferred from section properties.");
            AddField(report, "$.pageSetup.orientation", spec.PageSetup.Orientation.ToString(), "section-page-setup", 0.65, evidence, "Orientation inferred from section properties.");
        }
        else
        {
            AddUnresolved(report, "format.pageSetup.sizeUnresolved", "info", "Page size could not be inferred from section evidence.", evidence, "Confirm paper size manually.");
        }

        ApplyMargin(section.Margins, "top", value => spec.PageSetup.TopMarginCm = value, "$.pageSetup.topMarginCm", report, evidence);
        ApplyMargin(section.Margins, "bottom", value => spec.PageSetup.BottomMarginCm = value, "$.pageSetup.bottomMarginCm", report, evidence);
        ApplyMargin(section.Margins, "left", value => spec.PageSetup.LeftMarginCm = value, "$.pageSetup.leftMarginCm", report, evidence);
        ApplyMargin(section.Margins, "right", value => spec.PageSetup.RightMarginCm = value, "$.pageSetup.rightMarginCm", report, evidence);
    }

    private static void ApplyDefaultFont(FontFormatSpec font, ExtractedFormatCluster cluster, DocxFormatCandidateReport report)
    {
        var format = cluster.RepresentativeFormat;
        if (!string.IsNullOrWhiteSpace(format.EastAsiaFont))
        {
            font.EastAsia = format.EastAsiaFont!;
            AddField(report, "$.defaultFont.eastAsia", font.EastAsia, cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Default East Asia font inferred from body cluster.");
        }
        else
        {
            AddUnresolved(report, "format.defaultFont.eastAsiaUnresolved", "info", "Body cluster did not expose an East Asia font.", cluster.EvidencePaths, "Confirm East Asia body font manually.");
        }

        if (LooksLikeLatinFont(format.Font))
        {
            font.Latin = format.Font!;
            AddField(report, "$.defaultFont.latin", font.Latin, cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Default Latin font inferred from body cluster.");
        }
        else if (!string.IsNullOrWhiteSpace(format.Font))
        {
            AddUnresolved(report, "format.defaultFont.latinUnresolved", "info", "Body cluster font evidence does not look like a Latin font.", cluster.EvidencePaths, "Confirm Latin body font manually.");
        }

        if (format.FontSizePt is >= 4 and <= 72)
        {
            font.SizePt = Round(format.FontSizePt.Value);
            AddField(report, "$.defaultFont.sizePt", FormatNumber(font.SizePt), cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Default font size inferred from body cluster.");
        }
        else
        {
            AddUnresolved(report, "format.defaultFont.sizeUnresolved", "info", "Body cluster did not expose a reliable font size.", cluster.EvidencePaths, "Confirm body font size manually.");
        }

        font.Bold = format.Bold == true;
        font.Italic = format.Italic == true;
    }

    private static void ApplyParagraph(ParagraphFormatSpec paragraph, ExtractedFormatCluster cluster, FontFormatSpec font, string path, DocxFormatCandidateReport report)
    {
        var format = cluster.RepresentativeFormat;
        ApplyLineSpacing(paragraph, format, path, cluster, report);
        ApplyTwipsAsPoints(format.SpaceBeforeTwips, value => paragraph.SpaceBeforePt = value, $"{path}.spaceBeforePt", cluster, report, "Paragraph space before inferred from cluster.");
        ApplyTwipsAsPoints(format.SpaceAfterTwips, value => paragraph.SpaceAfterPt = value, $"{path}.spaceAfterPt", cluster, report, "Paragraph space after inferred from cluster.");

        if (format.FirstLineIndentTwips.HasValue && font.SizePt > 0)
        {
            paragraph.FirstLineIndentChars = Round(format.FirstLineIndentTwips.Value / (font.SizePt * 20.0));
            AddField(report, $"{path}.firstLineIndentChars", FormatNumber(paragraph.FirstLineIndentChars), cluster.Id, cluster.Confidence, cluster.EvidencePaths, "First-line indent converted from twips using inferred font size.");
        }

        if (format.HangingIndentTwips.HasValue)
        {
            paragraph.HangingIndentCm = Round(UnitConverter.TwipsToCentimeters(format.HangingIndentTwips.Value));
            AddField(report, $"{path}.hangingIndentCm", FormatNumber(paragraph.HangingIndentCm), cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Hanging indent converted from twips.");
        }

        var alignment = ToAlignment(format.Alignment);
        if (alignment.HasValue)
        {
            paragraph.Alignment = alignment.Value;
            AddField(report, $"{path}.alignment", paragraph.Alignment.ToString(), cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Paragraph alignment inferred from cluster.");
        }
    }

    private static void ApplyHeadingCandidates(DocxExtractionResult extraction, ThesisFormatSpec spec, DocxFormatCandidateReport report)
    {
        var candidates = extraction.FormatClusters
            .Where(cluster => cluster.RoleHint == "heading")
            .OrderBy(cluster => HeadingLevel(cluster) ?? 99)
            .ThenByDescending(cluster => cluster.RepresentativeFormat.FontSizePt ?? 0)
            .ThenByDescending(cluster => cluster.UsageCount)
            .ToList();
        if (candidates.Count == 0)
        {
            AddUnresolved(report, "format.headings.noClusters", "warning", "No heading-like format clusters were available.", ["document"], "Manually set heading styles and numbering.");
            return;
        }

        var assignedLevels = new HashSet<int>();
        foreach (var cluster in candidates)
        {
            if (assignedLevels.Count >= 3)
            {
                break;
            }

            var level = HeadingLevel(cluster) ?? NextAvailableHeadingLevel(assignedLevels);
            if (level is < 1 or > 3 || !assignedLevels.Add(level))
            {
                continue;
            }

            if (!spec.Headings.TryGetValue(level, out var heading))
            {
                heading = new HeadingFormatSpec { Level = level };
                spec.Headings[level] = heading;
            }

            UseCluster(report, cluster, $"$.headings.{level}");
            heading.Level = level;
            heading.OutlineLevel = Math.Max(0, level - 1);
            ApplyFont(heading.Font, cluster, $"$.headings.{level}.font", report);
            ApplyTwipsAsPoints(cluster.RepresentativeFormat.SpaceBeforeTwips, value => heading.SpaceBeforePt = value, $"$.headings.{level}.spaceBeforePt", cluster, report, "Heading space before inferred from cluster.");
            ApplyTwipsAsPoints(cluster.RepresentativeFormat.SpaceAfterTwips, value => heading.SpaceAfterPt = value, $"$.headings.{level}.spaceAfterPt", cluster, report, "Heading space after inferred from cluster.");
            var alignment = ToAlignment(cluster.RepresentativeFormat.Alignment);
            if (alignment.HasValue)
            {
                heading.Alignment = alignment.Value;
                AddField(report, $"$.headings.{level}.alignment", heading.Alignment.ToString(), cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Heading alignment inferred from cluster.");
            }

            heading.Numbered = !string.IsNullOrWhiteSpace(cluster.RepresentativeFormat.NumberingText);
            AddField(report, $"$.headings.{level}.numbered", heading.Numbered.ToString(CultureInfo.InvariantCulture), cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Heading numbering inferred from numbering evidence.");
        }

        foreach (var level in Enumerable.Range(1, 3).Where(level => !assignedLevels.Contains(level)))
        {
            AddUnresolved(report, $"format.headings.level{level}.unresolved", "info", $"Heading level {level} was not confidently inferred.", ["document"], $"Review and set $.headings.{level} manually if required.");
        }
    }

    private static void ApplyBibliographyCandidate(DocxExtractionResult extraction, ThesisFormatSpec spec, DocxFormatCandidateReport report)
    {
        var cluster = extraction.FormatClusters
            .Where(candidate => candidate.RoleHint == "bibliography")
            .OrderByDescending(candidate => candidate.UsageCount)
            .ThenByDescending(candidate => candidate.Confidence)
            .FirstOrDefault();
        if (cluster is null)
        {
            AddUnresolved(report, "format.bibliography.noCluster", "info", "No bibliography-like format cluster was available.", ["document"], "Confirm bibliography entry paragraph style manually.");
            return;
        }

        UseCluster(report, cluster, "$.bibliography.entryParagraph");
        ApplyParagraph(spec.Bibliography.EntryParagraph, cluster, spec.DefaultFont, "$.bibliography.entryParagraph", report);
    }

    private static void AddUnsupportedSurfaceUnresolved(DocxExtractionResult extraction, DocxFormatCandidateReport report)
    {
        if (extraction.Footnotes.Count > 0 || extraction.Endnotes.Count > 0)
        {
            AddUnresolved(report, "format.notes.notInferred", "info", "Footnote/endnote content was extracted, but note formatting is not inferred from note parts yet.", ["document"], "Review note styles and set $.notes manually.");
        }

        if (extraction.Tables.Count > 0)
        {
            AddUnresolved(report, "format.tables.notInferred", "info", "Table content was extracted, but table formatting is not inferred into candidate spec yet.", extraction.Tables.Select(table => table.EvidencePath).Take(5), "Review table borders, width, and caption requirements manually.");
        }

        if (extraction.Figures.Count > 0)
        {
            AddUnresolved(report, "format.figures.notInferred", "info", "Figure evidence was extracted, but figure sizing and caption formatting are not inferred into candidate spec yet.", extraction.Figures.Select(figure => figure.EvidencePath).Take(5), "Review figure size and caption requirements manually.");
        }
    }

    private static ExtractedFormatCluster? ChooseBodyCluster(IEnumerable<ExtractedFormatCluster> clusters)
    {
        return clusters
            .Where(cluster => cluster.RoleHint == "body")
            .OrderByDescending(BodyClusterScore)
            .FirstOrDefault();
    }

    private static double BodyClusterScore(ExtractedFormatCluster cluster)
    {
        var format = cluster.RepresentativeFormat;
        var score = cluster.UsageCount + cluster.Confidence * 4;
        if (format.FontSizePt is >= 9 and <= 14) score += 4;
        if (format.FirstLineIndentTwips is >= 240 and <= 960) score += 3;
        if (format.Alignment is "center") score -= 5;
        if (format.Bold == true) score -= 2;
        if (!string.IsNullOrWhiteSpace(format.LineSpacing)) score += 1;
        return score;
    }

    private static void ApplyFont(FontFormatSpec font, ExtractedFormatCluster cluster, string path, DocxFormatCandidateReport report)
    {
        var format = cluster.RepresentativeFormat;
        if (!string.IsNullOrWhiteSpace(format.EastAsiaFont))
        {
            font.EastAsia = format.EastAsiaFont!;
            AddField(report, $"{path}.eastAsia", font.EastAsia, cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Font inferred from cluster.");
        }

        if (LooksLikeLatinFont(format.Font))
        {
            font.Latin = format.Font!;
            AddField(report, $"{path}.latin", font.Latin, cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Latin font inferred from cluster.");
        }

        if (format.FontSizePt is >= 4 and <= 72)
        {
            font.SizePt = Round(format.FontSizePt.Value);
            AddField(report, $"{path}.sizePt", FormatNumber(font.SizePt), cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Font size inferred from cluster.");
        }

        font.Bold = format.Bold == true;
        font.Italic = format.Italic == true;
        AddField(report, $"{path}.bold", font.Bold.ToString(CultureInfo.InvariantCulture), cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Bold flag inferred from cluster.");
    }

    private static void ApplyLineSpacing(ParagraphFormatSpec paragraph, ExtractedEffectiveFormat format, string path, ExtractedFormatCluster cluster, DocxFormatCandidateReport report)
    {
        if (!int.TryParse(format.LineSpacing, NumberStyles.Integer, CultureInfo.InvariantCulture, out var line))
        {
            return;
        }

        if (string.Equals(format.LineSpacingRule, "exact", StringComparison.OrdinalIgnoreCase))
        {
            paragraph.LineSpacingExactPt = Round(UnitConverter.TwipsToPoints(line));
            AddField(report, $"{path}.lineSpacingExactPt", FormatNumber(paragraph.LineSpacingExactPt.Value), cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Exact line spacing converted from twips.");
        }
        else
        {
            paragraph.LineSpacingExactPt = null;
            paragraph.LineSpacingMultiple = Round(line / 240.0);
            AddField(report, $"{path}.lineSpacingMultiple", FormatNumber(paragraph.LineSpacingMultiple), cluster.Id, cluster.Confidence, cluster.EvidencePaths, "Automatic line spacing converted from Word 240-based multiple.");
        }
    }

    private static void ApplyTwipsAsPoints(int? twips, Action<double> assign, string path, ExtractedFormatCluster cluster, DocxFormatCandidateReport report, string reason)
    {
        if (!twips.HasValue)
        {
            return;
        }

        var points = Round(UnitConverter.TwipsToPoints(twips.Value));
        assign(points);
        AddField(report, path, FormatNumber(points), cluster.Id, cluster.Confidence, cluster.EvidencePaths, reason);
    }

    private static void ApplyMargin(string margins, string name, Action<double> assign, string path, DocxFormatCandidateReport report, IEnumerable<string> evidencePaths)
    {
        var value = ParseNamedInt(margins, name);
        if (!value.HasValue)
        {
            return;
        }

        var centimeters = Round(UnitConverter.TwipsToCentimeters(value.Value));
        assign(centimeters);
        AddField(report, path, FormatNumber(centimeters), "section-page-setup", 0.65, evidencePaths, "Page margin converted from twips.");
    }

    private static int? HeadingLevel(ExtractedFormatCluster cluster)
    {
        if (cluster.RepresentativeFormat.OutlineLevel is >= 0 and <= 2)
        {
            return cluster.RepresentativeFormat.OutlineLevel.Value + 1;
        }

        if (cluster.RepresentativeFormat.NumberingLevel is >= 0 and <= 2)
        {
            return cluster.RepresentativeFormat.NumberingLevel.Value + 1;
        }

        return null;
    }

    private static int NextAvailableHeadingLevel(HashSet<int> assigned)
    {
        return Enumerable.Range(1, 3).First(level => !assigned.Contains(level));
    }

    private static TextAlignment? ToAlignment(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "left" => TextAlignment.Left,
            "center" => TextAlignment.Center,
            "right" => TextAlignment.Right,
            "both" or "distribute" or "thaiDistribute" => TextAlignment.Both,
            _ => null
        };
    }

    private static void UseCluster(DocxFormatCandidateReport report, ExtractedFormatCluster cluster, string targetPath)
    {
        if (report.ClustersUsed.Any(item => item.ClusterId == cluster.Id && item.TargetPath == targetPath))
        {
            return;
        }

        report.ClustersUsed.Add(new DocxFormatCandidateClusterUse
        {
            ClusterId = cluster.Id,
            RoleHint = cluster.RoleHint,
            TargetPath = targetPath,
            Confidence = cluster.Confidence,
            UsageCount = cluster.UsageCount,
            EvidencePaths = cluster.EvidencePaths.Take(10).ToList(),
            Variance = cluster.Variance.ToList()
        });
    }

    private static void AddField(DocxFormatCandidateReport report, string path, string value, string sourceClusterId, double confidence, IEnumerable<string> evidencePaths, string reason)
    {
        report.GeneratedFields.Add(new DocxFormatCandidateField
        {
            Path = path,
            Value = value,
            SourceClusterId = sourceClusterId,
            Confidence = Round(confidence),
            EvidencePaths = evidencePaths.Take(10).ToList(),
            Reason = reason
        });
    }

    private static void AddUnresolved(DocxFormatCandidateReport report, string code, string severity, string message, IEnumerable<string> evidencePaths, string action)
    {
        report.UnresolvedItems.Add(new DocxFormatCandidateUnresolvedItem
        {
            Id = $"unresolved-{report.UnresolvedItems.Count + 1}",
            Code = code,
            Severity = severity,
            Message = message,
            EvidencePaths = evidencePaths.Take(10).ToList(),
            RecommendedAction = action
        });
    }

    private static string CandidateName(DocxExtractionResult extraction)
    {
        var baseName = Path.GetFileNameWithoutExtension(extraction.InputFileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "docx-extraction";
        }

        var safe = Regex.Replace(baseName.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(safe) || safe.Length < 4)
        {
            safe = "docx-extraction";
        }

        return $"candidate-{safe}";
    }

    private static bool IsA4(int widthTwips, int heightTwips)
    {
        var shortSide = Math.Min(widthTwips, heightTwips);
        var longSide = Math.Max(widthTwips, heightTwips);
        return Math.Abs(shortSide - 11906) <= 80 && Math.Abs(longSide - 16838) <= 80;
    }

    private static int? ParseNamedInt(string text, string name)
    {
        var match = Regex.Match(text, $@"(?:^|\s){Regex.Escape(name)}=(?<value>-?[0-9]+)", RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static bool LooksLikeLatinFont(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Any(char.IsAsciiLetter);
    }

    private static double Round(double value)
    {
        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
