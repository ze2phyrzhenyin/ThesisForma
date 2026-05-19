using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Extraction;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Core.Validation.ContentPreservation;

namespace ThesisDocx.Core.Structuring;

public sealed class CodexStructureReviewRunner
{
    private const int ExcerptLimit = 8_000;

    public CodexStructureReviewReport Run(CodexStructureReviewOptions options)
    {
        ValidateOptions(options);
        NormalizeDerivedPaths(options);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.PromptPath))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.ReviewReportPath))!);

        var report = CreateReport(options);
        try
        {
            EnsureWorkspacePath(options.WorkspacePath, options.ExtractionPath, "$.extraction");
            EnsureWorkspacePath(options.WorkspacePath, options.DocumentPath, "$.document");
            EnsureWorkspacePath(options.WorkspacePath, options.MappingReportPath, "$.report");
            EnsureWorkspacePath(options.WorkspacePath, options.UnresolvedPath, "$.unresolved");
            EnsureWorkspacePath(options.WorkspacePath, options.PromptPath, "$.prompt");
            EnsureWorkspacePath(options.WorkspacePath, options.ReviewReportPath, "$.reviewReport");
            EnsureWorkspacePath(options.WorkspacePath, options.StructureAnalysisPath, "$.structureAnalysis");
            EnsureWorkspacePath(options.WorkspacePath, options.RepairPlanPath, "$.repairPlan");
            EnsureWorkspacePath(options.WorkspacePath, options.RepairApplyReportPath, "$.repairApplyReport");
            if (!string.IsNullOrWhiteSpace(options.EvidencePath))
            {
                EnsureWorkspacePath(options.WorkspacePath, options.EvidencePath!, "$.evidence");
            }

            EnsureInputFile(options.ExtractionPath, "$.extraction");
            EnsureInputFile(options.DocumentPath, "$.document");
            EnsureInputFile(options.MappingReportPath, "$.report");
            EnsureInputFile(options.UnresolvedPath, "$.unresolved");

            var extraction = ReadJson<DocxExtractionResult>(options.ExtractionPath);
            var document = ReadJson<ThesisDocument>(options.DocumentPath);
            var mapping = ReadJson<ThesisStructureMappingReport>(options.MappingReportPath);
            var unresolvedItems = ReadJson<List<ThesisStructureUnresolvedItem>>(options.UnresolvedPath);
            var evidenceLinks = !string.IsNullOrWhiteSpace(options.EvidencePath) && File.Exists(options.EvidencePath)
                ? ReadJson<List<ThesisStructureEvidenceLink>>(options.EvidencePath!)
                : mapping.EvidenceLinks;
            var structured = new ThesisStructuringResult
            {
                Document = document,
                Report = mapping,
                UnresolvedItems = unresolvedItems,
                EvidenceLinks = evidenceLinks
            };
            var analysis = new StructureBoundaryAnalyzer().Analyze(extraction, structured);
            WriteJson(options.StructureAnalysisPath, analysis);
            report.StructureAnalysisPath = options.StructureAnalysisPath;
            report.StructureAnalysisRiskLevel = analysis.RiskLevel;
            report.StructureQualityScore = analysis.QualityScore;
            report.StructureAnalysisRecommendedCodexReview = analysis.RecommendCodexReview;
            report.StructureAnalysisIssueCount = analysis.IssueCount;
            report.Artifacts.Add(options.StructureAnalysisPath);

            var documentJsonBefore = File.ReadAllText(options.DocumentPath);
            var mappingJsonBefore = File.ReadAllText(options.MappingReportPath);
            var unresolvedJsonBefore = File.ReadAllText(options.UnresolvedPath);
            var evidenceJsonBefore = !string.IsNullOrWhiteSpace(options.EvidencePath) && File.Exists(options.EvidencePath)
                ? File.ReadAllText(options.EvidencePath)
                : string.Empty;
            report.DraftHashBefore = FileHash(options.DocumentPath);
            report.StructuredArtifactHashBefore = StructuredArtifactsHash(options);
            var prompt = new StructurePromptBuilder().BuildCodexReview(options);
            File.WriteAllText(options.PromptPath, prompt);
            report.Artifacts.Add(options.PromptPath);

            var lastMessagePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.ReviewReportPath))!, "structure-codex-last-message.txt");
            report.LastMessagePath = lastMessagePath;
            var processResult = RunCodexProcess(options, prompt, lastMessagePath);
            report.CodexInvoked = true;
            report.CodexArguments = processResult.Arguments;
            report.CodexExitCode = processResult.ExitCode;
            report.TimedOut = processResult.TimedOut;
            report.StandardOutputExcerpt = Excerpt(processResult.StandardOutput);
            report.StandardErrorExcerpt = Excerpt(processResult.StandardError);
            if (File.Exists(lastMessagePath))
            {
                report.LastMessageExcerpt = Excerpt(File.ReadAllText(lastMessagePath));
                report.Artifacts.Add(lastMessagePath);
            }

            if (processResult.TimedOut)
            {
                AddError(report, "structure.codex.timeout", "$.codex", "Codex structure review timed out before producing a trusted result.", "Increase --timeout-seconds or rerun the review in a smaller private workspace.");
            }
            else if (processResult.ExitCode != 0)
            {
                AddError(report, "structure.codex.failed", "$.codex", "Codex structure review returned a non-zero exit code.", "Inspect structure-codex-review.json and rerun with a working codex command.");
            }
            else
            {
                report.DraftHashAfterCodex = FileHash(options.DocumentPath);
                report.StructuredArtifactHashAfterCodex = StructuredArtifactsHash(options);
                if (report.StructuredArtifactHashAfterCodex != report.StructuredArtifactHashBefore)
                {
                    RestoreStructuredArtifacts(options, documentJsonBefore, mappingJsonBefore, unresolvedJsonBefore, evidenceJsonBefore);
                    report.DirectArtifactEditDetected = true;
                    AddError(report, "structure.codex.directArtifactEdit", "$.codex", "Codex edited structured artifacts directly instead of returning a repair plan.", "Rerun Codex review with the structured repair plan schema and do not trust direct file edits.");
                }
                else if (TryLoadRepairPlan(report, out var repairPlan))
                {
                    WriteJson(options.RepairPlanPath, repairPlan);
                    report.RepairPlanPath = options.RepairPlanPath;
                    report.RepairPlanSchemaPath = options.RepairPlanSchemaPath;
                    report.PlannedOperationCount = repairPlan.Operations.Count;
                    report.Artifacts.Add(options.RepairPlanPath);
                    if (ValidateRepairPlan(options, report))
                    {
                        ApplyRepairPlan(options, report, extraction, document, mapping, unresolvedItems, evidenceLinks, repairPlan);
                    }
                }
            }
        }
        catch (Win32Exception ex)
        {
            AddError(report, "structure.codex.commandNotFound", "$.codexCommand", $"Could not start Codex command '{options.CodexCommand}'.", "Install Codex CLI or pass --codex-command with a valid executable.");
            report.StandardErrorExcerpt = Excerpt(ex.Message);
        }
        catch (IOException ex)
        {
            AddError(report, "structure.codex.ioFailed", "$", "Codex structure review could not read or write one of its workspace artifacts.", "Check private workspace paths and permissions, then rerun the review.");
            report.StandardErrorExcerpt = Excerpt(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            AddError(report, "structure.codex.permissionDenied", "$", "Codex structure review was denied access to a required workspace artifact.", "Move the intake workspace to a writable private directory and rerun.");
            report.StandardErrorExcerpt = Excerpt(ex.Message);
        }
        catch (JsonException ex)
        {
            AddError(report, "structure.codex.invalidJson", "$.document", "Codex structure review left a JSON artifact in an invalid shape.", "Open the referenced JSON artifact, repair syntax, and rerun validate-input.");
            report.StandardErrorExcerpt = Excerpt(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            AddError(report, "structure.codex.invalidState", "$", ex.Message, "Regenerate the rule-based draft, then rerun Codex structure review.");
        }

        report.DraftHashAfter = File.Exists(options.DocumentPath) ? FileHash(options.DocumentPath) : string.Empty;
        if (!string.IsNullOrWhiteSpace(report.DraftHashBefore)
            && report.DraftHashBefore == report.DraftHashAfter
            && report.CodexExitCode == 0
            && report.BlockingIssues.Count == 0)
        {
            AddWarning(report, "structure.codex.noDraftChange", "$.document", "Codex review completed without changing the draft document.", "Review unresolved-items.json; the rule-based draft may already be acceptable or Codex may need a narrower prompt.");
        }

        report.Status = report.BlockingIssues.Count == 0 ? "pass" : "fail";
        WriteReport(options.ReviewReportPath, report);
        return report;
    }

    private static bool ValidateRepairPlan(CodexStructureReviewOptions options, CodexStructureReviewReport report)
    {
        if (string.IsNullOrWhiteSpace(options.RepairPlanSchemaPath) || !File.Exists(options.RepairPlanSchemaPath))
        {
            AddError(report, "structure.codex.repairPlanSchemaMissing", "$.repairPlanSchema", "Structure repair plan schema was not found.", "Pass --repair-plan-schema with a valid schema path or restore schemas/structure-repair-plan.schema.json.");
            return false;
        }

        var validation = new ThesisSchemaValidator().ValidateStructureRepairPlanFile(options.RepairPlanPath, options.RepairPlanSchemaPath);
        report.Diagnostics.AddRange(validation.Diagnostics);
        foreach (var error in validation.Errors)
        {
            report.BlockingIssues.Add($"{error.Code}: {error.Message}");
        }

        return validation.IsValid;
    }

    private static void ApplyRepairPlan(
        CodexStructureReviewOptions options,
        CodexStructureReviewReport report,
        DocxExtractionResult extraction,
        ThesisDocument document,
        ThesisStructureMappingReport mapping,
        List<ThesisStructureUnresolvedItem> unresolvedItems,
        List<ThesisStructureEvidenceLink> evidenceLinks,
        StructureRepairPlan repairPlan)
    {
        var documentCopy = Clone(document);
        var mappingCopy = Clone(mapping);
        var unresolvedCopy = Clone(unresolvedItems);
        var evidenceCopy = Clone(evidenceLinks);
        var applyReport = new StructureRepairPatchApplier().Apply(documentCopy, mappingCopy, unresolvedCopy, evidenceCopy, repairPlan);
        WriteJson(options.RepairApplyReportPath, applyReport);
        report.RepairApplyReportPath = options.RepairApplyReportPath;
        report.AppliedOperationCount = applyReport.AppliedOperationCount;
        report.RejectedOperationCount = applyReport.RejectedOperationCount;
        report.MovedBlockCount = applyReport.MovedBlockCount;
        report.Artifacts.Add(options.RepairApplyReportPath);
        report.Diagnostics.AddRange(applyReport.Diagnostics);
        if (applyReport.Status != "pass")
        {
            report.BlockingIssues.AddRange(applyReport.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
            return;
        }

        WriteJson(options.DocumentPath, documentCopy);
        WriteJson(options.MappingReportPath, mappingCopy);
        WriteJson(options.UnresolvedPath, unresolvedCopy);
        if (!string.IsNullOrWhiteSpace(options.EvidencePath))
        {
            WriteJson(options.EvidencePath!, evidenceCopy);
        }

        AuditReviewedDraft(extraction, options, report);
    }

    private static CodexProcessResult RunCodexProcess(CodexStructureReviewOptions options, string prompt, string lastMessagePath)
    {
        var arguments = BuildCodexArguments(options, lastMessagePath);
        var processStartInfo = new ProcessStartInfo(options.CodexCommand)
        {
            WorkingDirectory = Path.GetFullPath(options.WorkspacePath),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Could not start Codex process.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.Write(prompt);
        process.StandardInput.Close();

        var timeoutMilliseconds = (int)Math.Clamp(options.TimeoutSeconds, 1, 86_400) * 1000;
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
            return new CodexProcessResult(-1, true, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult(), arguments);
        }

        return new CodexProcessResult(process.ExitCode, false, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult(), arguments);
    }

    private static List<string> BuildCodexArguments(CodexStructureReviewOptions options, string lastMessagePath)
    {
        var arguments = new List<string>
        {
            "exec",
            "--cd",
            Path.GetFullPath(options.WorkspacePath),
            "--sandbox",
            options.CodexSandbox,
            "--ask-for-approval",
            options.CodexApprovalPolicy,
            "--output-last-message",
            lastMessagePath
        };

        if (options.SkipGitRepoCheck)
        {
            arguments.Add("--skip-git-repo-check");
        }

        if (!string.IsNullOrWhiteSpace(options.Model))
        {
            arguments.Add("--model");
            arguments.Add(options.Model!);
        }

        if (!string.IsNullOrWhiteSpace(options.Profile))
        {
            arguments.Add("--profile");
            arguments.Add(options.Profile!);
        }

        if (!string.IsNullOrWhiteSpace(options.RepairPlanSchemaPath) && File.Exists(options.RepairPlanSchemaPath))
        {
            arguments.Add("--output-schema");
            arguments.Add(options.RepairPlanSchemaPath);
        }

        arguments.AddRange(options.ExtraArguments.Where(argument => !string.IsNullOrWhiteSpace(argument)));
        arguments.Add("-");
        return arguments;
    }

    private static void AuditReviewedDraft(DocxExtractionResult extraction, CodexStructureReviewOptions options, CodexStructureReviewReport report)
    {
        var document = JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(options.DocumentPath), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize draft document '{options.DocumentPath}'.");
        var audit = new ContentPreservationAuditor().AuditDraft(extraction, document);
        report.DraftContentPreservationStatus = audit.Status;
        report.DraftContentMissingSegments = audit.MissingSegments.Count;
        report.DraftContentBlockingIssues = audit.BlockingIssues.Count;
        report.Warnings.AddRange(audit.Warnings.Select(issue => $"{issue.Code}: {issue.Message}"));
        report.BlockingIssues.AddRange(audit.BlockingIssues.Select(issue => $"{issue.Code}: {issue.Message}"));
        foreach (var issue in audit.BlockingIssues)
        {
            report.Diagnostics.Add(new UnifiedDiagnostic
            {
                Code = issue.Code,
                Severity = DiagnosticSeverity.Error,
                Path = issue.EvidencePath,
                Message = issue.Message,
                FixHint = "Restore original thesis text from extraction evidence before using the draft.",
                Category = DiagnosticCategory.Intake,
                Source = nameof(CodexStructureReviewRunner)
            });
        }

        if (!string.IsNullOrWhiteSpace(options.EvidencePath) && File.Exists(options.EvidencePath))
        {
            var evidence = JsonSerializer.Deserialize<List<ThesisStructureEvidenceLink>>(File.ReadAllText(options.EvidencePath), ThesisJson.Options);
            report.EvidenceLinkCount = evidence?.Count ?? 0;
        }

        report.Artifacts.Add(options.DocumentPath);
        report.Artifacts.Add(options.MappingReportPath);
        report.Artifacts.Add(options.UnresolvedPath);
        if (!string.IsNullOrWhiteSpace(options.EvidencePath))
        {
            report.Artifacts.Add(options.EvidencePath!);
        }
    }

    private static bool TryLoadRepairPlan(CodexStructureReviewReport report, out StructureRepairPlan repairPlan)
    {
        repairPlan = new StructureRepairPlan();
        var candidate = File.Exists(report.LastMessagePath)
            ? File.ReadAllText(report.LastMessagePath)
            : report.StandardOutputExcerpt;
        var json = ExtractJsonObject(candidate);
        if (string.IsNullOrWhiteSpace(json))
        {
            AddError(report, "structure.codex.repairPlanMissing", "$.codex", "Codex did not return a structure repair plan JSON object.", "Rerun Codex review; the final response must match structure-repair-plan.schema.json.");
            return false;
        }

        try
        {
            repairPlan = JsonSerializer.Deserialize<StructureRepairPlan>(json, ThesisJson.Options) ?? new StructureRepairPlan();
            return true;
        }
        catch (JsonException ex)
        {
            AddError(report, "structure.codex.repairPlanInvalidJson", "$.codex", "Codex returned invalid structure repair plan JSON.", "Rerun Codex review with --output-schema or repair the plan manually.");
            report.StandardErrorExcerpt = Excerpt(ex.Message);
            return false;
        }
    }

    private static string ExtractJsonObject(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n', StringComparison.Ordinal);
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
            {
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
            }
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{', StringComparison.Ordinal);
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : string.Empty;
    }

    private static void ValidateOptions(CodexStructureReviewOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.WorkspacePath)) throw new ArgumentException("WorkspacePath is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ExtractionPath)) throw new ArgumentException("ExtractionPath is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.DocumentPath)) throw new ArgumentException("DocumentPath is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.MappingReportPath)) throw new ArgumentException("MappingReportPath is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.UnresolvedPath)) throw new ArgumentException("UnresolvedPath is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.PromptPath)) throw new ArgumentException("PromptPath is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ReviewReportPath)) throw new ArgumentException("ReviewReportPath is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.CodexCommand)) throw new ArgumentException("CodexCommand is required.", nameof(options));
    }

    private static void NormalizeDerivedPaths(CodexStructureReviewOptions options)
    {
        var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(options.ReviewReportPath)) ?? Directory.GetCurrentDirectory();
        if (string.IsNullOrWhiteSpace(options.StructureAnalysisPath))
        {
            options.StructureAnalysisPath = Path.Combine(reportDirectory, "structure-analysis.json");
        }

        if (string.IsNullOrWhiteSpace(options.RepairPlanPath))
        {
            options.RepairPlanPath = Path.Combine(reportDirectory, "structure-repair-plan.json");
        }

        if (string.IsNullOrWhiteSpace(options.RepairApplyReportPath))
        {
            options.RepairApplyReportPath = Path.Combine(reportDirectory, "structure-repair-apply-report.json");
        }
    }

    private static void EnsureInputFile(string path, string jsonPath)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Required Codex structure review artifact is missing at {jsonPath}: {path}");
        }
    }

    private static void EnsureWorkspacePath(string workspacePath, string path, string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var workspace = Path.GetFullPath(workspacePath);
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(workspace, fullPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException($"Codex structure review path at {jsonPath} must stay inside the private workspace: {path}");
        }
    }

    private static CodexStructureReviewReport CreateReport(CodexStructureReviewOptions options)
    {
        return new CodexStructureReviewReport
        {
            Status = "running",
            CodexCommand = options.CodexCommand,
            WorkspacePath = options.WorkspacePath,
            ExtractionPath = options.ExtractionPath,
            DocumentPath = options.DocumentPath,
            MappingReportPath = options.MappingReportPath,
            UnresolvedPath = options.UnresolvedPath,
            EvidencePath = options.EvidencePath,
            PromptPath = options.PromptPath,
            StructureAnalysisPath = options.StructureAnalysisPath,
            RepairPlanPath = options.RepairPlanPath,
            RepairPlanSchemaPath = options.RepairPlanSchemaPath,
            RepairApplyReportPath = options.RepairApplyReportPath
        };
    }

    private static void AddError(CodexStructureReviewReport report, string code, string path, string message, string fixHint)
    {
        report.BlockingIssues.Add($"{code}: {message}");
        report.Diagnostics.Add(new UnifiedDiagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Error,
            Path = path,
            Message = message,
            FixHint = fixHint,
            Category = DiagnosticCategory.Intake,
            Source = nameof(CodexStructureReviewRunner)
        });
    }

    private static void AddWarning(CodexStructureReviewReport report, string code, string path, string message, string fixHint)
    {
        report.Warnings.Add($"{code}: {message}");
        report.Diagnostics.Add(new UnifiedDiagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Warning,
            Path = path,
            Message = message,
            FixHint = fixHint,
            Category = DiagnosticCategory.Intake,
            Source = nameof(CodexStructureReviewRunner)
        });
    }

    private static string FileHash(string path)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private static string StructuredArtifactsHash(CodexStructureReviewOptions options)
    {
        var builder = new StringBuilder();
        foreach (var path in new[] { options.DocumentPath, options.MappingReportPath, options.UnresolvedPath, options.EvidencePath ?? string.Empty })
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                builder.Append(path).Append(':').Append(FileHash(path)).AppendLine();
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static void RestoreStructuredArtifacts(CodexStructureReviewOptions options, string documentJson, string mappingJson, string unresolvedJson, string evidenceJson)
    {
        File.WriteAllText(options.DocumentPath, documentJson);
        File.WriteAllText(options.MappingReportPath, mappingJson);
        File.WriteAllText(options.UnresolvedPath, unresolvedJson);
        if (!string.IsNullOrWhiteSpace(options.EvidencePath) && !string.IsNullOrWhiteSpace(evidenceJson))
        {
            File.WriteAllText(options.EvidencePath, evidenceJson);
        }
    }

    private static string Excerpt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= ExcerptLimit ? value : value[..ExcerptLimit];
    }

    private static void WriteReport(string path, CodexStructureReviewReport report)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(report, ThesisJson.Options));
    }

    private static T ReadJson<T>(string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize JSON artifact '{path}'.");
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, ThesisJson.Options));
    }

    private static T Clone<T>(T value)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, ThesisJson.Options), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not clone {typeof(T).Name}.");
    }

    private sealed record CodexProcessResult(int ExitCode, bool TimedOut, string StandardOutput, string StandardError, List<string> Arguments);
}
