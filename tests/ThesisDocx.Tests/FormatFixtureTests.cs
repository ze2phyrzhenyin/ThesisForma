using System.Text.Json;
using ThesisDocx.Core.Diff.Layout;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class FormatFixtureTests
{
    [Theory]
    [InlineData("pagination-edge-cases")]
    [InlineData("header-footer-edge-cases")]
    [InlineData("heading-numbering-edge-cases")]
    [InlineData("table-edge-cases")]
    [InlineData("figure-equation-crossref-edge-cases")]
    [InlineData("bibliography-citation-edge-cases")]
    [InlineData("cover-declaration-edge-cases")]
    public void FormatFixture_ShouldRenderAndValidate(string fixtureName)
    {
        var rendered = RenderFixture(fixtureName);

        Assert.True(new OpenXmlPackageValidator().Validate(rendered.DocxPath).IsValid);
        Assert.True(rendered.Validation.IsValid, string.Join(Environment.NewLine, rendered.Validation.Errors));
    }

    [Fact]
    public void FormatFixtures_ShouldProduceLayoutSignatures()
    {
        foreach (var fixture in FixtureNames())
        {
            var rendered = RenderFixture(fixture);
            var signature = new DocxLayoutSignatureExtractor().Extract(rendered.DocxPath);

            Assert.NotEmpty(signature.Sections);
            Assert.NotEmpty(signature.Styles);
        }
    }

    [Fact]
    public void FormatFixture_PaginationEdgeCases_ShouldRenderAndValidate() => FormatFixture_ShouldRenderAndValidate("pagination-edge-cases");

    [Fact]
    public void FormatFixture_HeaderFooterEdgeCases_ShouldRenderAndValidate() => FormatFixture_ShouldRenderAndValidate("header-footer-edge-cases");

    [Fact]
    public void FormatFixture_HeadingNumberingEdgeCases_ShouldRenderAndValidate() => FormatFixture_ShouldRenderAndValidate("heading-numbering-edge-cases");

    [Fact]
    public void FormatFixture_TableEdgeCases_ShouldRenderAndValidate() => FormatFixture_ShouldRenderAndValidate("table-edge-cases");

    [Fact]
    public void FormatFixture_FigureEquationCrossRefEdgeCases_ShouldRenderAndValidate() => FormatFixture_ShouldRenderAndValidate("figure-equation-crossref-edge-cases");

    [Fact]
    public void FormatFixture_BibliographyCitationEdgeCases_ShouldRenderAndValidate() => FormatFixture_ShouldRenderAndValidate("bibliography-citation-edge-cases");

    [Fact]
    public void FormatFixture_CoverDeclarationEdgeCases_ShouldRenderAndValidate() => FormatFixture_ShouldRenderAndValidate("cover-declaration-edge-cases");

    private static RenderedFixture RenderFixture(string fixtureName)
    {
        var fixtureDirectory = Path.Combine(RepoRoot(), "examples", "format-fixtures", fixtureName);
        var documentPath = Path.Combine(fixtureDirectory, "document.json");
        var document = JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options)!;
        ResolveRelativeImagePaths(document, Path.GetDirectoryName(documentPath)!);
        var output = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"), $"{fixtureName}.docx");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        ThesisFormatSpec format;
        DocxRenderContext? context = null;
        if (File.Exists(Path.Combine(fixtureDirectory, "template.json")))
        {
            var resolution = new TemplateResolver().Resolve(fixtureDirectory, document, new Dictionary<string, string> { ["variables.defenseDate"] = "2026-06-01" });
            format = resolution.FormatSpec!;
            context = new DocxRenderContext
            {
                TemplateId = resolution.Template?.Id,
                TemplateVersion = resolution.Template?.Version,
                TemplateSchool = resolution.Template?.School,
                TemplateCollege = resolution.Template?.College,
                ResolvedFormatSpecVersion = resolution.FormatSpec?.SchemaVersion,
                PageTemplates = resolution.PageTemplates,
                Variables = resolution.Variables.Where(v => v.Value is not null).ToDictionary(v => v.Name, v => v.Value!, StringComparer.Ordinal),
                Assets = resolution.Assets.ToDictionary(a => a.Id, StringComparer.Ordinal)
            };
            new DocxRenderer().Render(document, format, output, context);
            return new RenderedFixture(output, new FormatConformanceValidator().Validate(output, fixtureDirectory));
        }

        format = JsonSerializer.Deserialize<ThesisFormatSpec>(File.ReadAllText(Path.Combine(fixtureDirectory, "format-spec.json")), ThesisJson.Options)!;
        new DocxRenderer().Render(document, format, output);
        return new RenderedFixture(output, new FormatConformanceValidator().Validate(output, format));
    }

    private static IEnumerable<string> FixtureNames()
    {
        return Directory.EnumerateDirectories(Path.Combine(RepoRoot(), "examples", "format-fixtures"))
            .Select(Path.GetFileName)
            .Cast<string>()
            .Where(name => name != "baselines")
            .Order(StringComparer.Ordinal);
    }

    private static void ResolveRelativeImagePaths(ThesisDocument document, string baseDirectory)
    {
        foreach (var figure in document.Sections.SelectMany(s => s.Blocks).OfType<FigureBlock>())
        {
            if (!string.IsNullOrWhiteSpace(figure.ImagePath) && !Path.IsPathRooted(figure.ImagePath))
            {
                figure.ImagePath = Path.GetFullPath(Path.Combine(baseDirectory, figure.ImagePath));
            }
        }
    }

    private static string RepoRoot() => TestRenderHelper.LocateRepoRootForTests();

    private sealed record RenderedFixture(string DocxPath, OpenXmlValidationResult Validation);
}
