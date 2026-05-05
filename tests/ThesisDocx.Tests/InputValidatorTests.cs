using System.Text.Json;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class InputValidatorTests
{
    [Fact]
    public void InputValidator_ShouldCatchDuplicateSectionIds()
    {
        var (document, format, baseDir) = LoadSimple();
        document.Sections[1].Id = document.Sections[0].Id;

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "duplicate.sectionId");
    }

    [Fact]
    public void InputValidator_ShouldCatchDanglingCrossReference()
    {
        var (document, format, baseDir) = LoadSimple();
        var paragraph = (ParagraphBlock)document.Sections[3].Blocks[1];
        paragraph.Inlines.Add(new ReferenceInline { BookmarkName = "missing-bookmark" });

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "dangling.reference");
    }

    [Fact]
    public void InputValidator_ShouldCatchMissingBibliographyKey()
    {
        var (document, format, baseDir) = LoadSimple();
        var paragraph = (ParagraphBlock)document.Sections[3].Blocks[^1];
        paragraph.Inlines.OfType<CitationInline>().Single().TargetId = "missing-ref";

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "dangling.citation");
    }

    [Fact]
    public void InputValidator_ShouldCatchHeadingLevelJump()
    {
        var (document, format, baseDir) = LoadSimple();
        document.Sections[3].Blocks.Insert(0, new HeadingBlock
        {
            Level = 3,
            Inlines = [new TextInline { Text = "跳级标题" }]
        });

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "heading.levelJump");
    }

    [Fact]
    public void InputValidator_ShouldAcceptSupportedSchemaVersions()
    {
        var (simpleDocument, simpleFormat, simpleBaseDir) = LoadSimple();
        var (fullDocument, fullFormat, fullBaseDir) = LoadFull();

        var simpleResult = new ThesisInputValidator().Validate(simpleDocument, simpleFormat, simpleBaseDir);
        var fullResult = new ThesisInputValidator().Validate(fullDocument, fullFormat, fullBaseDir);

        Assert.True(simpleResult.IsValid, string.Join(Environment.NewLine, simpleResult.Errors));
        Assert.True(fullResult.IsValid, string.Join(Environment.NewLine, fullResult.Errors));
    }

    [Fact]
    public void InputValidator_ShouldRejectUnsupportedSchemaVersion()
    {
        var (document, format, baseDir) = LoadSimple();
        document.SchemaVersion = "9.9.9";

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "unsupported.documentSchemaVersion");
    }

    [Fact]
    public void InputValidator_ShouldRejectEquationSourceMismatch()
    {
        var (document, format, baseDir) = LoadFull();
        var equation = FindFirstEquation(document);
        equation.SourceType = EquationSourceType.Omml;
        equation.Omml = null;

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "equation.sourceMismatch");
    }

    [Fact]
    public void InputValidator_ShouldRejectUnsafeOmml()
    {
        var (document, format, baseDir) = LoadFull();
        var equation = document.Sections.SelectMany(s => s.Blocks).OfType<EquationBlock>().First(e => e.SourceType == EquationSourceType.Omml);
        equation.Omml = """<m:oMath xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math" xmlns:x="urn:bad"><x:payload>bad</x:payload></m:oMath>""";

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code.StartsWith("equation.omml.", StringComparison.Ordinal));
    }

    [Fact]
    public void InputValidator_ShouldWarnForUnsupportedLatexWhenFallbackEnabled()
    {
        var (document, format, baseDir) = LoadFull();
        var equation = document.Sections.SelectMany(s => s.Blocks).OfType<EquationBlock>().First(e => e.SourceType == EquationSourceType.Latex);
        equation.Latex = "\\frac{a}{b}";
        format.Equations.AllowLatexFallbackToPlain = true;

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Warnings, warning => warning.Code == "equation.latex.unsupportedFallback");
    }

    [Fact]
    public void InputValidator_ShouldRejectDanglingEquationReference()
    {
        var (document, format, baseDir) = LoadFull();
        var paragraph = document.Sections.SelectMany(s => s.Blocks).OfType<ParagraphBlock>().First(p => p.Inlines.OfType<ReferenceInline>().Any());
        paragraph.Inlines.Add(new ReferenceInline { BookmarkName = "bm-eq-missing" });

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "dangling.reference");
    }

    [Fact]
    public void InputValidator_ShouldRejectInvalidEquationNumberingFormat()
    {
        var (document, format, baseDir) = LoadFull();
        FindFirstEquation(document).Numbering!.Format = "chapter.index";

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "equation.numbering.invalidFormat");
    }

    [Fact]
    public void InputValidator_ShouldRejectInconsistentTableGrid()
    {
        var (document, format, baseDir) = LoadFull();
        var table = FindFirstTable(document);
        table.Rows[1].Cells[1].GridSpan = 3;

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "table.grid.inconsistent");
    }

    [Fact]
    public void InputValidator_ShouldRejectInvalidVerticalMergeChain()
    {
        var (document, format, baseDir) = LoadFull();
        var table = FindFirstTable(document);
        table.Rows[1].Cells[0].VerticalMerge = VerticalMergeKind.Continue;

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "table.verticalMerge.invalidChain");
    }

    [Fact]
    public void InputValidator_ShouldRejectGridSpanLessThanOne()
    {
        var (document, format, baseDir) = LoadFull();
        var table = FindFirstTable(document);
        table.Rows[0].Cells[0].GridSpan = 0;

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "table.gridSpan.invalid");
    }

    [Fact]
    public void InputValidator_ShouldRejectHeaderRowAfterBodyRow()
    {
        var (document, format, baseDir) = LoadFull();
        var table = FindFirstTable(document);
        table.Rows[1].IsHeader = false;
        table.Rows[2].IsHeader = true;

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.Contains(result.Errors, error => error.Code == "table.header.afterBody");
    }

    [Fact]
    public void Cli_Render_ShouldFailForInvalidInput()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var invalidDocument = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", $"{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(invalidDocument)!);
        var (document, _, _) = LoadSimple();
        var paragraph = (ParagraphBlock)document.Sections[3].Blocks[^1];
        paragraph.Inlines.OfType<CitationInline>().Single().TargetId = "missing-ref";
        File.WriteAllText(invalidDocument, JsonSerializer.Serialize(document, ThesisJson.Options));

        var result = CliRunner.Run(root, "render", "--document", invalidDocument, "--format", Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json"), "--out", Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx"));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("dangling.citation", result.StandardError);
    }

    [Fact]
    public void Cli_ValidateInput_ShouldReturnValidForExamples()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var simpleResult = CliRunner.Run(root, "validate-input", "--document", Path.Combine(root, "examples", "simple-thesis", "document.json"), "--format", Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json"));
        var fullResult = CliRunner.Run(root, "validate-input", "--document", Path.Combine(root, "examples", "full-thesis", "document.json"), "--format", Path.Combine(root, "examples", "format-specs", "strict-cn-thesis.json"));

        Assert.Equal(0, simpleResult.ExitCode);
        Assert.Contains("Input valid", simpleResult.StandardOutput);
        Assert.Equal(0, fullResult.ExitCode);
        Assert.Contains("Input valid", fullResult.StandardOutput);
    }

    private static (ThesisDocument Document, ThesisFormatSpec Format, string BaseDir) LoadSimple()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var documentPath = Path.Combine(root, "examples", "simple-thesis", "document.json");
        var document = JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options)!;
        var format = JsonSerializer.Deserialize<ThesisFormatSpec>(File.ReadAllText(Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json")), ThesisJson.Options)!;
        return (document, format, Path.GetDirectoryName(documentPath)!);
    }

    private static (ThesisDocument Document, ThesisFormatSpec Format, string BaseDir) LoadFull()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var documentPath = Path.Combine(root, "examples", "full-thesis", "document.json");
        var document = JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options)!;
        var format = JsonSerializer.Deserialize<ThesisFormatSpec>(File.ReadAllText(Path.Combine(root, "examples", "format-specs", "strict-cn-thesis.json")), ThesisJson.Options)!;
        return (document, format, Path.GetDirectoryName(documentPath)!);
    }

    private static EquationBlock FindFirstEquation(ThesisDocument document)
    {
        return document.Sections.SelectMany(s => s.Blocks).OfType<EquationBlock>().First();
    }

    private static TableBlock FindFirstTable(ThesisDocument document)
    {
        return document.Sections.SelectMany(s => s.Blocks).OfType<TableBlock>().First();
    }
}
