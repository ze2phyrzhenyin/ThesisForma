using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;
using ThesisDocx.Core.Diff;
using ThesisDocx.Tests.Fixtures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class DocxCliDiffTests
{
    [Fact]
    public void Cli_DocxDiff_ShouldReturnEqualForSameDocx()
    {
        var docx = TestRenderHelper.RenderFullThesis().DocxPath;

        var result = CliRunner.Run(RepoRoot(), "docx", "diff", "--base", docx, "--target", docx, "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"isEqual\": true", result.StandardOutput);
    }

    [Fact]
    public void Cli_DocxDiff_ShouldWriteJson()
    {
        var docx = TestRenderHelper.RenderFullThesis().DocxPath;
        var output = TempPath("diff.json");

        var result = CliRunner.Run(RepoRoot(), "docx", "diff", "--base", docx, "--target", docx, "--json", "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.True(json["isEqual"]!.GetValue<bool>());
    }

    [Fact]
    public void Cli_LayoutSignature_ShouldWriteJson()
    {
        var output = TempPath("layout.json");

        var result = CliRunner.Run(RepoRoot(), "docx", "layout-signature", "--docx", TestRenderHelper.RenderFullThesis().DocxPath, "--out", output);
        var json = JsonNode.Parse(File.ReadAllText(output))!;

        Assert.Equal(0, result.ExitCode);
        Assert.True(json["sections"]!.AsArray().Count >= 3);
    }

    [Fact]
    public void Cli_LayoutCompare_ShouldApplyThreshold()
    {
        var signature = TempPath("layout.json");
        var compare = TempPath("compare.json");
        var docx = TestRenderHelper.RenderFullThesis().DocxPath;
        Assert.Equal(0, CliRunner.Run(RepoRoot(), "docx", "layout-signature", "--docx", docx, "--out", signature).ExitCode);

        var result = CliRunner.Run(RepoRoot(), "docx", "layout-compare", "--base", signature, "--target", signature, "--threshold", "0.99", "--out", compare);
        var json = JsonNode.Parse(File.ReadAllText(compare))!;

        Assert.Equal(0, result.ExitCode);
        Assert.True(json["meetsThreshold"]!.GetValue<bool>());
        Assert.Equal(1.0, json["similarityScore"]!.GetValue<double>());
    }

    [Fact]
    public void Cli_InvalidDocxDiffArgs_ShouldReturnNonZero()
    {
        var result = CliRunner.Run(RepoRoot(), "docx", "diff", "--base", TestRenderHelper.RenderFullThesis().DocxPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("target", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocxDiff_ShouldDetectNotePartChanges()
    {
        var source = StructuralDocxFixtureFactory.RenderNotesDocx();
        var changed = Mutate(source, document =>
        {
            document.MainDocumentPart!.FootnotesPart!.Footnotes!.Elements<W.Footnote>().First(note => note.Id?.Value > 0).Remove();
            document.MainDocumentPart.FootnotesPart.Footnotes.Save();
        });

        var result = new DocxStructureDiffEngine().Compare(source, changed);

        Assert.Contains(result.Changes, change => change.Category == "notes" && change.Path.Contains("footnotes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DocxDiff_ShouldDetectCustomPropertyChanges()
    {
        var source = StructuralDocxFixtureFactory.RenderCustomPropertiesDocx();
        var changed = Mutate(source, document =>
        {
            var property = document.CustomFilePropertiesPart!.Properties!.Elements<DocumentFormat.OpenXml.CustomProperties.CustomDocumentProperty>()
                .First(property => property.Name?.Value == "ThesisDocx.TemplateId");
            property.RemoveAllChildren();
            property.AppendChild(new VTLPWSTR("changed-template"));
            document.CustomFilePropertiesPart.Properties.Save();
        });

        var result = new DocxStructureDiffEngine().Compare(source, changed);

        Assert.Contains(result.Changes, change => change.Category == "customProperties" && change.Path.Contains("ThesisDocx.TemplateId", StringComparison.Ordinal));
    }

    [Fact]
    public void DocxDiff_ShouldDetectAdvancedTableStructureChanges()
    {
        var source = StructuralDocxFixtureFactory.RenderAdvancedTableDocx();
        var changed = Mutate(source, document =>
        {
            document.MainDocumentPart!.Document.Descendants<W.GridSpan>().First().Remove();
            document.MainDocumentPart.Document.Save();
        });

        var result = new DocxStructureDiffEngine().Compare(source, changed);

        Assert.Contains(result.Changes, change => change.Category == "table" && change.Path.Contains("gridSpan", StringComparison.Ordinal));
    }

    private static string RepoRoot()
    {
        return TestRenderHelper.LocateRepoRootForTests();
    }

    private static string TempPath(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private static string Mutate(string source, Action<WordprocessingDocument> mutate)
    {
        var copy = TempPath("mutated.docx");
        File.Copy(source, copy, overwrite: true);
        using var document = WordprocessingDocument.Open(copy, true);
        mutate(document);
        return copy;
    }
}
