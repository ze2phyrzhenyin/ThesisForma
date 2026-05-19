using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class FormatValidatorTests
{
    [Fact]
    public void FormatValidator_ShouldReportDetailedErrorForWrongMargin()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        rendered.Format.PageSetup.TopMarginCm = 1.0;

        var result = new FormatConformanceValidator().Validate(rendered.DocxPath, rendered.Format);

        var error = Assert.Single(result.Errors, e => e.Code == "page.margin.top");
        Assert.Equal("/word/document.xml", error.PartName);
        Assert.False(string.IsNullOrWhiteSpace(error.Expected));
        Assert.False(string.IsNullOrWhiteSpace(error.Actual));
    }

    [Fact]
    public void FormatValidator_ShouldReportDetailedErrorForMissingToc()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        var copy = CopyDocx(rendered.DocxPath);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            foreach (var field in document.MainDocumentPart!.Document.Descendants<W.SimpleField>().Where(f => f.Instruction?.Value?.Contains("TOC", StringComparison.OrdinalIgnoreCase) == true).ToList())
            {
                field.Remove();
            }

            document.MainDocumentPart.Document.Save();
        }

        var result = new FormatConformanceValidator().Validate(copy, rendered.Format);

        Assert.Contains(result.Errors, error => error.Code == "fields.toc.missing" && error.PartName == "/word/document.xml");
    }

    [Fact]
    public void FormatValidator_ShouldNotRequireTocFieldWhenFormatDisablesWordFieldCode()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        var copy = CopyDocx(rendered.DocxPath);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            foreach (var field in document.MainDocumentPart!.Document.Descendants<W.SimpleField>().Where(f => f.Instruction?.Value?.Contains("TOC", StringComparison.OrdinalIgnoreCase) == true).ToList())
            {
                field.Remove();
            }

            document.MainDocumentPart.Document.Save();
        }

        rendered.Format.Toc.UseWordFieldCode = false;

        var result = new FormatConformanceValidator().Validate(copy, rendered.Format);

        Assert.DoesNotContain(result.Errors, error => error.Code == "fields.toc.missing");
    }

    [Fact]
    public void FormatValidator_ShouldReportDetailedErrorForMissingFooterPageNumber()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        var copy = CopyDocx(rendered.DocxPath);
        using (var document = WordprocessingDocument.Open(copy, true))
        {
            foreach (var footer in document.MainDocumentPart!.FooterParts)
            {
                foreach (var field in footer.Footer.Descendants<W.SimpleField>().ToList())
                {
                    field.Remove();
                }

                footer.Footer.Save();
            }
        }

        var result = new FormatConformanceValidator().Validate(copy, rendered.Format);

        Assert.Contains(result.Errors, error => error.Code == "footer.pageNumber.missing");
    }

    [Fact]
    public void Cli_Validate_ShouldSupportJsonOutput()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        var root = TestRenderHelper.LocateRepoRootForTests();

        var result = CliRunner.Run(root, "validate", "--docx", rendered.DocxPath, "--format", Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json"), "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"isValid\": true", result.StandardOutput);
        Assert.Contains("\"checkedRules\"", result.StandardOutput);
    }

    private static string CopyDocx(string source)
    {
        var copy = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", $"{Guid.NewGuid():N}.docx");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(source, copy);
        return copy;
    }
}
