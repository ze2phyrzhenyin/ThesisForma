using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Extraction;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Templates.Authoring;

public sealed class TemplateCandidateProposalService
{
    public TemplateCandidateProposalResult Propose(TemplateCandidateProposalOptions options)
    {
        var report = new TemplateCandidateProposalReport
        {
            SourceTemplate = options.TemplatePath,
            ProposedTemplate = options.OutputTemplatePath,
            SourceCandidateFormatSpec = options.CandidateFormatSpecPath,
            SourceCandidateReport = options.CandidateReportPath,
            DecisionsPath = options.DecisionsPath
        };

        if (!ValidateRequiredPaths(options, report))
        {
            return Finish(report);
        }

        if (PathsOverlap(options.TemplatePath, options.OutputTemplatePath))
        {
            AddIssue(
                report,
                "templateCandidateProposal.output.overlapsSource",
                "$.outputTemplatePath",
                "Output template directory must not be the source template directory or inside it.",
                "Choose a fresh output directory under an onboarding or out workspace.");
            return Finish(report);
        }

        if (Directory.Exists(options.OutputTemplatePath) || File.Exists(options.OutputTemplatePath))
        {
            AddIssue(
                report,
                "templateCandidateProposal.output.exists",
                "$.outputTemplatePath",
                "Output template path already exists.",
                "Choose a new output path; this command does not overwrite existing templates.");
            return Finish(report);
        }

        TemplateResolutionResult resolution;
        TemplatePackage sourceTemplate;
        JsonObject candidateSpecNode;
        DocxFormatCandidateReport candidateReport;
        FormatCandidateDecisionSet decisions;
        try
        {
            resolution = new TemplateResolver().Resolve(options.TemplatePath);
            sourceTemplate = new TemplateLoader().Load(options.TemplatePath);
            candidateSpecNode = ReadJsonObject(options.CandidateFormatSpecPath);
            candidateReport = ReadJson<DocxFormatCandidateReport>(options.CandidateReportPath);
            decisions = ReadJson<FormatCandidateDecisionSet>(options.DecisionsPath);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            AddIssue(
                report,
                "templateCandidateProposal.input.readFailed",
                "$",
                "Could not read template, candidate, or decision input.",
                ex.Message);
            return Finish(report);
        }

        report.ChaosLevel = candidateReport.ChaosLevel;
        report.ChaosScore = candidateReport.ChaosScore;
        report.RiskAccepted = decisions.RiskAccepted;
        report.CandidateFieldCount = candidateReport.GeneratedFields.Count;

        if (!resolution.IsValid || resolution.FormatSpec is null)
        {
            foreach (var error in resolution.Errors)
            {
                AddIssue(report, error.Code, error.Path, error.Message, "Validate and fix the source template before proposing candidate changes.");
            }

            return Finish(report);
        }

        var candidateFields = candidateReport.GeneratedFields.ToDictionary(field => field.Path, StringComparer.Ordinal);
        var decisionsByPath = new Dictionary<string, FormatCandidateDecision>(StringComparer.Ordinal);
        foreach (var decision in decisions.Decisions)
        {
            if (string.IsNullOrWhiteSpace(decision.FieldPath))
            {
                AddIssue(report, "templateCandidateProposal.decision.pathMissing", "$.decisions", "Decision fieldPath is required.", "Use scaffold-candidate-decisions and review every generated field.");
                continue;
            }

            if (decisionsByPath.ContainsKey(decision.FieldPath))
            {
                AddIssue(report, "templateCandidateProposal.decision.duplicate", decision.FieldPath, "Decision file contains duplicate field decisions.", "Keep exactly one decision for each generated candidate field.");
                continue;
            }

            decisionsByPath[decision.FieldPath] = decision;
        }

        foreach (var field in candidateReport.GeneratedFields)
        {
            if (!decisionsByPath.ContainsKey(field.Path))
            {
                AddIssue(report, "templateCandidateProposal.decision.missing", field.Path, "Candidate field has no explicit accept, reject, or modify decision.", "Scaffold decisions from the candidate report and review every field.");
            }
        }

        foreach (var path in decisionsByPath.Keys.Where(path => !candidateFields.ContainsKey(path)).Order(StringComparer.Ordinal))
        {
            AddIssue(report, "templateCandidateProposal.decision.unknownField", path, "Decision references a field that is not in the candidate report.", "Remove the decision or regenerate it from the current format-candidate-report.json.");
        }

        if (HasErrors(report))
        {
            return Finish(report);
        }

        var proposedNode = JsonSerializer.SerializeToNode(resolution.FormatSpec, ThesisJson.Options)?.AsObject()
            ?? throw new InvalidOperationException("Could not serialize resolved format spec.");

        foreach (var field in candidateReport.GeneratedFields.OrderBy(field => field.Path, StringComparer.Ordinal))
        {
            var decision = decisionsByPath[field.Path];
            var effectiveReviewer = string.IsNullOrWhiteSpace(decision.Reviewer) ? decisions.Reviewer : decision.Reviewer;
            ValidateDecision(report, decision, effectiveReviewer, field);
            switch (decision.Decision)
            {
                case FormatCandidateDecisionKind.Accept:
                    if (TryGetNode(candidateSpecNode, field.Path, out var candidateValue))
                    {
                        TrySetNode(proposedNode, field.Path, candidateValue?.DeepClone(), report);
                        AddAppliedField(report, decision, effectiveReviewer, candidateValue, "accept");
                    }
                    else
                    {
                        AddIssue(report, "templateCandidateProposal.candidate.valueMissing", field.Path, "Candidate spec does not contain the generated field value.", "Regenerate candidate-format-spec.json and format-candidate-report.json together.");
                    }

                    break;
                case FormatCandidateDecisionKind.Modify:
                    if (decision.Value is null)
                    {
                        AddIssue(report, "templateCandidateProposal.decision.modifyValueMissing", field.Path, "Modified decisions must provide a value.", "Set decision.value to the reviewed target value.");
                    }
                    else
                    {
                        TrySetNode(proposedNode, field.Path, decision.Value.DeepClone(), report);
                        AddAppliedField(report, decision, effectiveReviewer, decision.Value, "modify");
                    }

                    break;
                case FormatCandidateDecisionKind.Reject:
                    report.RejectedCount++;
                    break;
                default:
                    AddIssue(report, "templateCandidateProposal.decision.unsupported", field.Path, "Unsupported decision value.", "Use accept, reject, or modify.");
                    break;
            }
        }

        if (string.Equals(candidateReport.ChaosLevel, "high", StringComparison.OrdinalIgnoreCase)
            && (report.AcceptedCount > 0 || report.ModifiedCount > 0))
        {
            if (!decisions.RiskAccepted)
            {
                AddIssue(
                    report,
                    "templateCandidateProposal.riskAcceptance.required",
                    "$.riskAccepted",
                    "High-chaos candidates require explicit risk acceptance before any field can be accepted or modified.",
                    "Set riskAccepted to true and document riskAcceptanceReason after human review.");
            }
            else if (string.IsNullOrWhiteSpace(decisions.RiskAcceptanceReason))
            {
                AddIssue(
                    report,
                    "templateCandidateProposal.riskAcceptance.reasonMissing",
                    "$.riskAcceptanceReason",
                    "Risk acceptance reason is required for high-chaos candidate changes.",
                    "Document why the reviewed candidate fields are acceptable despite high format chaos.");
            }
        }

        if (HasErrors(report))
        {
            return Finish(report);
        }

        ThesisFormatSpec proposedSpec;
        try
        {
            proposedSpec = proposedNode.Deserialize<ThesisFormatSpec>(ThesisJson.Options)
                ?? throw new InvalidOperationException("Could not deserialize proposed format spec.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            AddIssue(
                report,
                "templateCandidateProposal.proposedSpec.invalid",
                "$.formatSpec",
                "Proposed format spec could not be deserialized after applying decisions.",
                ex.Message);
            return Finish(report);
        }

        CopyDirectory(Path.GetFullPath(options.TemplatePath), Path.GetFullPath(options.OutputTemplatePath));
        WriteProposedFormatSpec(sourceTemplate, options.OutputTemplatePath, proposedSpec, report);
        report.SkippedCount = Math.Max(0, candidateReport.GeneratedFields.Count - report.AcceptedCount - report.ModifiedCount - report.RejectedCount);
        report.Artifacts.Add(Path.Combine(options.OutputTemplatePath, "template.json"));
        report.Status = HasErrors(report) ? "fail" : "pass";
        return Finish(report);
    }

    public static string ToMarkdown(TemplateCandidateProposalReport report)
    {
        var lines = new List<string>
        {
            "# Template Candidate Proposal Report",
            string.Empty,
            $"- Status: `{report.Status}`",
            $"- Chaos: `{report.ChaosLevel}` ({report.ChaosScore.ToString("0.###", CultureInfo.InvariantCulture)})",
            $"- Candidate fields: `{report.CandidateFieldCount}`",
            $"- Accepted: `{report.AcceptedCount}`",
            $"- Modified: `{report.ModifiedCount}`",
            $"- Rejected: `{report.RejectedCount}`",
            $"- Skipped: `{report.SkippedCount}`",
            $"- Proposed template: `{report.ProposedTemplate}`",
            string.Empty,
            "## Applied Fields"
        };
        lines.AddRange(report.AppliedFields.Count == 0
            ? ["- None"]
            : report.AppliedFields.Select(field => $"- `{field.FieldPath}` {field.Decision} by {field.Reviewer}: {field.ValuePreview}"));
        lines.Add(string.Empty);
        lines.Add("## Issues");
        lines.AddRange(report.Issues.Count == 0
            ? ["- None"]
            : report.Issues.Select(issue => $"- `{issue.Severity}` `{issue.Code}` at `{issue.Path}`: {issue.Message}"));
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static bool ValidateRequiredPaths(TemplateCandidateProposalOptions options, TemplateCandidateProposalReport report)
    {
        var valid = true;
        valid &= RequireDirectory(options.TemplatePath, "$.templatePath", "Source template directory is required.", report);
        valid &= RequireFile(options.CandidateFormatSpecPath, "$.candidateFormatSpecPath", "Candidate format spec is required.", report);
        valid &= RequireFile(options.CandidateReportPath, "$.candidateReportPath", "Candidate report is required.", report);
        valid &= RequireFile(options.DecisionsPath, "$.decisionsPath", "Decision file is required.", report);
        if (string.IsNullOrWhiteSpace(options.OutputTemplatePath))
        {
            AddIssue(report, "templateCandidateProposal.output.missing", "$.outputTemplatePath", "Output template path is required.", "Pass --out with a fresh directory path.");
            valid = false;
        }

        return valid;
    }

    private static bool RequireDirectory(string path, string jsonPath, string message, TemplateCandidateProposalReport report)
    {
        if (Directory.Exists(path))
        {
            return true;
        }

        AddIssue(report, "templateCandidateProposal.input.missing", jsonPath, message, "Check the path and rerun the command.");
        return false;
    }

    private static bool RequireFile(string path, string jsonPath, string message, TemplateCandidateProposalReport report)
    {
        if (File.Exists(path))
        {
            return true;
        }

        AddIssue(report, "templateCandidateProposal.input.missing", jsonPath, message, "Check the path and rerun the command.");
        return false;
    }

    private static void ValidateDecision(
        TemplateCandidateProposalReport report,
        FormatCandidateDecision decision,
        string effectiveReviewer,
        DocxFormatCandidateField field)
    {
        if (decision.Decision is FormatCandidateDecisionKind.Accept or FormatCandidateDecisionKind.Modify)
        {
            if (string.IsNullOrWhiteSpace(effectiveReviewer))
            {
                AddIssue(report, "templateCandidateProposal.decision.reviewerMissing", decision.FieldPath, "Accepted or modified fields require a reviewer.", "Set decisions.reviewer or decision.reviewer.");
            }

            if (string.IsNullOrWhiteSpace(decision.Reason))
            {
                AddIssue(report, "templateCandidateProposal.decision.reasonMissing", decision.FieldPath, "Accepted or modified fields require a review reason.", "Document why this field is accepted or modified.");
            }

            if (decision.EvidencePaths.Count == 0)
            {
                AddIssue(report, "templateCandidateProposal.decision.evidenceMissing", decision.FieldPath, "Accepted or modified fields require evidence paths.", "Copy evidence paths from the candidate field and any reviewed requirement evidence.");
            }
            else if (!decision.EvidencePaths.Any(path => field.EvidencePaths.Contains(path, StringComparer.Ordinal)))
            {
                AddIssue(report, "templateCandidateProposal.decision.evidenceMismatch", decision.FieldPath, "Decision evidence must reference at least one candidate evidence path.", "Use evidence paths from the candidate report for this field.");
            }
        }
        else if (string.IsNullOrWhiteSpace(decision.Reason))
        {
            AddIssue(report, "templateCandidateProposal.decision.rejectReasonMissing", decision.FieldPath, "Rejected fields require a reason.", "Document why the candidate field was rejected.");
        }
    }

    private static void AddAppliedField(TemplateCandidateProposalReport report, FormatCandidateDecision decision, string reviewer, JsonNode? value, string kind)
    {
        if (kind == "accept")
        {
            report.AcceptedCount++;
        }
        else
        {
            report.ModifiedCount++;
        }

        report.AppliedFields.Add(new TemplateCandidateProposalAppliedField
        {
            FieldPath = decision.FieldPath,
            Decision = kind,
            Reviewer = reviewer,
            Reason = decision.Reason,
            EvidencePaths = decision.EvidencePaths.ToList(),
            ValuePreview = value?.ToJsonString(ThesisJson.Options) ?? "null"
        });
    }

    private static JsonObject ReadJsonObject(string path)
    {
        return JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidOperationException($"Expected JSON object at '{path}'.");
    }

    private static T ReadJson<T>(string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize '{path}'.");
    }

    private static bool TryGetNode(JsonObject root, string path, out JsonNode? value)
    {
        value = root;
        foreach (var segment in SplitPath(path))
        {
            if (value is not JsonObject current || !current.TryGetPropertyValue(segment, out value))
            {
                value = null;
                return false;
            }
        }

        return true;
    }

    private static void TrySetNode(JsonObject root, string path, JsonNode? value, TemplateCandidateProposalReport report)
    {
        var segments = SplitPath(path).ToArray();
        if (segments.Length == 0)
        {
            AddIssue(report, "templateCandidateProposal.path.invalid", path, "Field path must point to a format spec property.", "Use candidate report field paths such as $.bodyParagraph.lineSpacingMultiple.");
            return;
        }

        var current = root;
        foreach (var segment in segments.Take(segments.Length - 1))
        {
            if (current[segment] is not JsonObject child)
            {
                child = new JsonObject();
                current[segment] = child;
            }

            current = child;
        }

        current[segments[^1]] = value;
    }

    private static IEnumerable<string> SplitPath(string path)
    {
        if (!path.StartsWith("$.", StringComparison.Ordinal) || path.Length <= 2)
        {
            return [];
        }

        return path[2..].Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void WriteProposedFormatSpec(TemplatePackage sourceTemplate, string outputTemplatePath, ThesisFormatSpec proposedSpec, TemplateCandidateProposalReport report)
    {
        var outputTemplateJson = Path.Combine(outputTemplatePath, "template.json");
        var templateNode = ReadJsonObject(outputTemplateJson);
        if (!string.IsNullOrWhiteSpace(sourceTemplate.FormatSpecRef) && !Path.IsPathRooted(sourceTemplate.FormatSpecRef))
        {
            var formatPath = Path.GetFullPath(Path.Combine(outputTemplatePath, sourceTemplate.FormatSpecRef));
            Directory.CreateDirectory(Path.GetDirectoryName(formatPath)!);
            File.WriteAllText(formatPath, JsonSerializer.Serialize(proposedSpec, ThesisJson.Options));
            report.Artifacts.Add(formatPath);
            return;
        }

        templateNode["formatSpec"] = JsonSerializer.SerializeToNode(proposedSpec, ThesisJson.Options);
        templateNode.Remove("formatSpecRef");
        File.WriteAllText(outputTemplateJson, templateNode.ToJsonString(ThesisJson.Options));
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), overwrite: false);
        }
    }

    private static bool PathsOverlap(string source, string target)
    {
        var sourceFull = NormalizeDirectory(source);
        var targetFull = NormalizeDirectory(target);
        return string.Equals(sourceFull, targetFull, StringComparison.Ordinal)
            || IsSubPath(targetFull, sourceFull)
            || IsSubPath(sourceFull, targetFull);
    }

    private static bool IsSubPath(string candidate, string parent)
    {
        var relative = Path.GetRelativePath(parent, candidate);
        return relative != "."
            && !relative.StartsWith("..", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    private static string NormalizeDirectory(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static TemplateCandidateProposalResult Finish(TemplateCandidateProposalReport report)
    {
        report.Status = HasErrors(report) ? "fail" : report.Status;
        return new TemplateCandidateProposalResult { Report = report };
    }

    private static bool HasErrors(TemplateCandidateProposalReport report)
    {
        return report.Issues.Any(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddIssue(TemplateCandidateProposalReport report, string code, string path, string message, string fixHint)
    {
        report.Issues.Add(new TemplateCandidateProposalIssue
        {
            Code = code,
            Path = path,
            Message = message,
            FixHint = fixHint
        });
    }
}
