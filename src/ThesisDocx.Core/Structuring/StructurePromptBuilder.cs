using System.Text;
using System.Text.Json;
using System.Globalization;
using ThesisDocx.Core.Extraction;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Structuring;

public sealed class StructurePromptBuilder
{
    public string BuildCodexReview(CodexStructureReviewOptions options)
    {
        var builder = new StringBuilder();
        builder.Append(Build(options.ExtractionPath, options.FormatCandidateReportPath));
        builder.AppendLine();
        builder.AppendLine("## Codex CLI Structure Repair Task");
        builder.AppendLine();
        builder.AppendLine("You are running in an automated Codex CLI review step. Edit only the structured intake artifacts listed below.");
        builder.AppendLine();
        builder.AppendLine("Artifacts to review and update:");
        builder.AppendLine($"- Draft ThesisDocument: `{options.DocumentPath}`");
        builder.AppendLine($"- Mapping report: `{options.MappingReportPath}`");
        builder.AppendLine($"- Unresolved items: `{options.UnresolvedPath}`");
        builder.AppendLine($"- Structure analysis report: `{options.StructureAnalysisPath}`");
        builder.AppendLine($"- Required repair plan output path: `{options.RepairPlanPath}`");
        if (!string.IsNullOrWhiteSpace(options.EvidencePath))
        {
            builder.AppendLine($"- Evidence links: `{options.EvidencePath}`");
        }

        if (!string.IsNullOrWhiteSpace(options.RepairPlanSchemaPath))
        {
            builder.AppendLine($"- Repair plan JSON Schema: `{options.RepairPlanSchemaPath}`");
        }

        if (!string.IsNullOrWhiteSpace(options.TemplatePath))
        {
            builder.AppendLine($"- Template for validation only: `{options.TemplatePath}`");
        }

        builder.AppendLine();
        builder.AppendLine("Required behavior:");
        builder.AppendLine("- Do not edit the draft, mapping report, unresolved file, or evidence links directly. Return only a JSON `StructureRepairPlan` as the final response.");
        builder.AppendLine("- Repair section and chapter boundaries when extraction evidence proves a block was grouped under the wrong heading, for example content after `第三章` incorrectly remaining under `第二章`.");
        builder.AppendLine("- Use heading sequence, numbering patterns, table-of-contents evidence, paragraph order, and evidence paths to justify moves.");
        builder.AppendLine("- Preserve original thesis text exactly. Moving an existing paragraph, table, figure, note, or preserved object is allowed; rewriting text is not.");
        builder.AppendLine("- Keep every mapped block linked to extraction evidence. If an evidence link cannot be justified, add an unresolved item instead of inventing a mapping.");
        builder.AppendLine("- Keep formatting decisions out of the draft; formatting stays in TemplatePackage or ThesisFormatSpec.");
        builder.AppendLine("- Use operations such as `moveBlock`, `ensureSection`, `addUnresolvedItem`, `removeUnresolvedItem`, and `updateHeadingLevel`.");
        builder.AppendLine("- Keep `reason` short and evidence-based for every operation.");
        builder.AppendLine();
        builder.AppendLine("Final response contract:");
        builder.AppendLine("- Return valid JSON only, with `planVersion`, `summary`, `operations`, and optional `reviewerNotes`.");
        builder.AppendLine("- If no repair is justified, return an empty `operations` array and explain that in `summary`.");
        builder.AppendLine();
        builder.AppendLine("Do not modify `input/input.docx`, do not copy thesis content into public examples or docs, and do not claim that an uncertain structure is confirmed.");
        return builder.ToString();
    }

    public string Build(string extractionPath, string? formatCandidateReportPath = null)
    {
        var extraction = JsonSerializer.Deserialize<DocxExtractionResult>(File.ReadAllText(extractionPath), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize extraction '{extractionPath}'.");
        var builder = new StringBuilder();
        builder.AppendLine("# Codex Structure Review Prompt");
        builder.AppendLine();
        builder.AppendLine("You are reviewing extracted DOCX evidence, not the original DOCX package.");
        builder.AppendLine();
        builder.AppendLine("Inputs:");
        builder.AppendLine($"- Extraction JSON: `{extractionPath}`");
        builder.AppendLine("- Extracted Markdown: `extraction/extracted.md` if present beside the extraction JSON.");
        if (!string.IsNullOrWhiteSpace(formatCandidateReportPath))
        {
            builder.AppendLine($"- Candidate format report: `{formatCandidateReportPath}`.");
        }

        builder.AppendLine();
        builder.AppendLine("Rules:");
        builder.AppendLine("1. Do not read or modify `input.docx` directly.");
        builder.AppendLine("2. Identify thesis structure from evidence paths such as `paragraphs[12]` and `tables[0].rows[1].cells[2]`.");
        builder.AppendLine("3. Preserve original body text. Do not rewrite, polish, summarize, or delete thesis content.");
        builder.AppendLine("4. If uncertain, add an unresolved item instead of guessing.");
        builder.AppendLine("5. Output `structured/thesis-document.draft.json`, `structured/structure-mapping-report.json`, `structured/unresolved-items.json`, and `structured/evidence-links.json`.");
        builder.AppendLine("6. Link every mapped section/block to extraction evidence where possible.");
        builder.AppendLine("7. Treat `candidate-format-spec.json` and `format-candidate-report.json` as review evidence only; do not accept candidate fields into a committed template without human review.");
        builder.AppendLine("8. Run `validate-input` after editing the draft.");
        builder.AppendLine();
        builder.AppendLine("Evidence summary:");
        builder.AppendLine($"- Paragraphs: {extraction.Paragraphs.Count}");
        builder.AppendLine($"- Tables: {extraction.Tables.Count}");
        builder.AppendLine($"- Figures: {extraction.Figures.Count}");
        builder.AppendLine($"- Footnotes: {extraction.Footnotes.Count}");
        builder.AppendLine($"- Endnotes: {extraction.Endnotes.Count}");
        builder.AppendLine($"- Effective format signatures: {extraction.FormatSignatures.Count}");
        builder.AppendLine($"- Format chaos: {extraction.FormatChaos.ChaosLevel} ({extraction.FormatChaos.ChaosScore.ToString("0.###", CultureInfo.InvariantCulture)})");
        builder.AppendLine($"- Format clusters: {extraction.FormatClusters.Count}");
        builder.AppendLine($"- Heading candidates: {extraction.PossibleHeadings.Count}");
        builder.AppendLine($"- Bibliography candidates: {extraction.PossibleBibliography.Count}");
        if (extraction.FormatChaos.Diagnostics.Count > 0)
        {
            builder.AppendLine("- Format diagnostics:");
            foreach (var issue in extraction.FormatChaos.Diagnostics.Take(8))
            {
                builder.AppendLine($"  - `{issue.Code}` {issue.Message}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Recommended command:");
        builder.AppendLine();
        builder.AppendLine("```bash");
        builder.AppendLine("dotnet run --project src/ThesisDocx.Cli -- validate-input \\");
        builder.AppendLine("  --document onboarding-workspaces/docx-structure-pilot/structured/thesis-document.draft.json \\");
        builder.AppendLine("  --template examples/templates/example-university-engineering");
        builder.AppendLine("```");
        return builder.ToString();
    }
}
