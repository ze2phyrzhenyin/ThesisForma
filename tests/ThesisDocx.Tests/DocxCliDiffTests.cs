using System.Text.Json.Nodes;
using ThesisDocx.Tests.Fixtures;

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
}
