using System.Text.Json;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Tests.Fixtures;

internal static class TestRenderHelper
{
    public static RenderedDocx RenderSimpleThesis()
    {
        return Render("simple-thesis", "basic-cn-thesis");
    }

    public static RenderedDocx RenderFullThesis()
    {
        return Render("full-thesis", "strict-cn-thesis");
    }

    public static string LocateRepoRootForTests()
    {
        return LocateRepoRoot();
    }

    public static RenderedDocx RenderDocument(ThesisDocument document, ThesisFormatSpec format, DocxRenderContext? context = null)
    {
        var root = LocateRepoRoot();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "custom.docx");
        new DocxRenderer().Render(document, format, outputPath, context);
        return new RenderedDocx(root, outputPath, format);
    }

    private static RenderedDocx Render(string documentFixtureName, string formatFixtureName)
    {
        var root = LocateRepoRoot();
        var documentPath = Path.Combine(root, "examples", documentFixtureName, "document.json");
        var formatPath = Path.Combine(root, "examples", "format-specs", $"{formatFixtureName}.json");
        var document = JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options)
            ?? throw new InvalidOperationException("Could not read simple thesis document fixture.");
        var format = JsonSerializer.Deserialize<ThesisFormatSpec>(File.ReadAllText(formatPath), ThesisJson.Options)
            ?? throw new InvalidOperationException("Could not read basic thesis format fixture.");

        var outputDirectory = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, $"{documentFixtureName}.docx");
        new DocxRenderer().Render(document, format, outputPath);

        return new RenderedDocx(root, outputPath, format);
    }

    private static string LocateRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ThesisDocx.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}

internal sealed record RenderedDocx(string RepoRoot, string DocxPath, ThesisFormatSpec Format);
