using System.Text;
using System.Text.Json;
using ThesisDocx.Core.Extraction;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Structuring;

public sealed class StructurePromptBuilder
{
    public string Build(string extractionPath)
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
        builder.AppendLine();
        builder.AppendLine("Rules:");
        builder.AppendLine("1. Do not read or modify `input.docx` directly.");
        builder.AppendLine("2. Identify thesis structure from evidence paths such as `paragraphs[12]` and `tables[0].rows[1].cells[2]`.");
        builder.AppendLine("3. Preserve original body text. Do not rewrite, polish, summarize, or delete thesis content.");
        builder.AppendLine("4. If uncertain, add an unresolved item instead of guessing.");
        builder.AppendLine("5. Output `structured/thesis-document.draft.json`, `structured/structure-mapping-report.json`, `structured/unresolved-items.json`, and `structured/evidence-links.json`.");
        builder.AppendLine("6. Link every mapped section/block to extraction evidence where possible.");
        builder.AppendLine("7. Run `validate-input` after editing the draft.");
        builder.AppendLine();
        builder.AppendLine("Evidence summary:");
        builder.AppendLine($"- Paragraphs: {extraction.Paragraphs.Count}");
        builder.AppendLine($"- Tables: {extraction.Tables.Count}");
        builder.AppendLine($"- Figures: {extraction.Figures.Count}");
        builder.AppendLine($"- Footnotes: {extraction.Footnotes.Count}");
        builder.AppendLine($"- Endnotes: {extraction.Endnotes.Count}");
        builder.AppendLine($"- Heading candidates: {extraction.PossibleHeadings.Count}");
        builder.AppendLine($"- Bibliography candidates: {extraction.PossibleBibliography.Count}");
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
