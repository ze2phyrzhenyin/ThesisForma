using System.Text.Json;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Extraction;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Templates.Authoring;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Core.Validation.FormatRuleCoverage;
using ThesisDocx.Tests.Fixtures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class TemplateSystemTests
{
    [Fact]
    public void Schema_ShouldValidateFormatSpecVersion120()
    {
        var root = RepoRoot();
        var result = new ThesisSchemaValidator().ValidateFormatFile(
            TemplateFormatPath("example-university-engineering"),
            Path.Combine(root, "schemas", "thesis-format-spec.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldValidateTemplatePackageVersion100()
    {
        var root = RepoRoot();
        var result = ValidateTemplateSchema(TemplatePath("example-university-engineering"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldValidateTemplatePackage()
    {
        foreach (var template in Directory.EnumerateDirectories(Path.Combine(RepoRoot(), "examples", "templates")))
        {
            var result = ValidateTemplateSchema(template);
            Assert.True(result.IsValid, $"{template}:{Environment.NewLine}{string.Join(Environment.NewLine, result.Errors)}");
        }
    }

    [Fact]
    public void Schema_ShouldRejectTemplateWithAbsoluteAssetPath()
    {
        var path = WriteMutatedTemplate(node => node["assets"]![0]!["path"] = "/tmp/logo.png");

        var result = ValidateTemplateSchema(path);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectTemplateWithInvalidSemver()
    {
        var path = WriteMutatedTemplate(node => node["version"] = "v1");

        var result = ValidateTemplateSchema(path);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectUnknownLayoutBlockType()
    {
        var path = WriteMutatedTemplate(node => node["pageTemplates"]![0]!["blocks"]![0]!["type"] = "absolutePosition");

        var result = ValidateTemplateSchema(path);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectInvalidVariableType()
    {
        var path = WriteMutatedTemplate(node => node["variables"]![0]!["type"] = "secret");

        var result = ValidateTemplateSchema(path);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectInvalidTargetSectionType()
    {
        var path = WriteMutatedTemplate(node => node["pageTemplates"]![0]!["targetSectionType"] = "preface");

        var result = ValidateTemplateSchema(path);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void InputValidator_ShouldAcceptFormatSpecVersion120()
    {
        var (document, baseDir) = LoadFullDocument();
        var format = new TemplateResolver().Resolve(TemplatePath("example-university-engineering"), document).FormatSpec!;

        var result = new ThesisInputValidator().Validate(document, format, baseDir);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void TemplateValidator_ShouldRejectUnsupportedTemplateSchemaVersion()
    {
        var path = WriteMutatedTemplate(node => node["templateSchemaVersion"] = "9.9.9");

        var result = new TemplateValidationService().Validate(path);

        Assert.Contains(result.Errors, error => error.Code == "template.schemaVersion.unsupported");
    }

    [Fact]
    public void TemplateValidator_ShouldRejectDuplicateVariableNames()
    {
        var path = WriteMutatedTemplate(node =>
        {
            node["variables"]!.AsArray().Add(JsonNode.Parse("""{"name":"defenseDate","label":"Duplicate","type":"date"}"""));
        });

        var result = new TemplateValidationService().Validate(path, Path.Combine(RepoRoot(), "schemas", "template-package.schema.json"));

        Assert.Contains(result.Errors, error => error.Code == "template.variable.duplicate");
    }

    [Fact]
    public void TemplateValidator_ShouldRejectMissingPageTemplateVariableReference()
    {
        var path = WriteMutatedTemplate(node => node["pageTemplates"]![0]!["blocks"]![2]!["value"] = "{{variables.missingDefenseDate}}");

        var result = new TemplateValidationService().Validate(path, Path.Combine(RepoRoot(), "schemas", "template-package.schema.json"));

        Assert.Contains(result.Errors, error => error.Code == "template.pageTemplate.variable.missing");
    }

    [Fact]
    public void TemplateValidator_ShouldRejectMissingPageTemplateImageAssetReference()
    {
        var path = WriteMutatedTemplate(node => node["pageTemplates"]![0]!["blocks"]![1]!["assetId"] = "missingLogoAsset");

        var result = new TemplateValidationService().Validate(path, Path.Combine(RepoRoot(), "schemas", "template-package.schema.json"));

        Assert.Contains(result.Errors, error => error.Code == "template.pageTemplate.image.asset.missing");
    }

    [Fact]
    public void TemplateValidator_ShouldRejectIllegalPageSetupOverrideMargin()
    {
        var path = WriteMutatedTemplate(node => node["pageTemplates"]![0]!["pageSetupOverride"]!["leftMarginCm"] = -1);

        var result = new TemplateValidationService().Validate(path, Path.Combine(RepoRoot(), "schemas", "template-package.schema.json"));

        Assert.Contains(result.Errors, error => error.Code == "template.pageTemplate.pageSetup.margin.invalid");
    }

    [Fact]
    public void TemplateLoader_ShouldLoadTemplatePackage()
    {
        var template = new TemplateLoader().Load(TemplatePath("example-university-engineering"));

        Assert.Equal("example-university-engineering", template.Id);
        Assert.Equal("Example Engineering College", template.College);
        Assert.NotNull(template.TemplateDirectory);
    }

    [Fact]
    public void TemplateRegistry_ShouldListTemplates()
    {
        var templates = new TemplateRegistry().ListTemplates(Path.Combine(RepoRoot(), "examples", "templates"));

        Assert.Contains(templates, template => template.Id == "basic-cn-thesis");
        Assert.Contains(templates, template => template.Id == "example-university-engineering-variant");
        Assert.Equal(templates.OrderBy(template => template.Id, StringComparer.Ordinal).Select(t => t.Id), templates.Select(t => t.Id));
    }

    [Fact]
    public void TemplateResolver_ShouldResolveInlineFormatSpec()
    {
        var format = LoadFormat(TemplateFormatPath("example-university-engineering"));
        var path = WriteTemplateDirectory(new TemplatePackage
        {
            Id = "inline-template",
            Name = "Inline Template",
            Version = "1.0.0",
            Locale = "zh-CN",
            FormatSpec = format
        });

        var resolution = new TemplateResolver().Resolve(path);

        Assert.True(resolution.IsValid, string.Join(Environment.NewLine, resolution.Errors));
        Assert.Equal("1.2.0", resolution.FormatSpec!.SchemaVersion);
    }

    [Fact]
    public void TemplateResolver_ShouldResolveFormatSpecRef()
    {
        var resolution = new TemplateResolver().Resolve(TemplatePath("example-university-engineering"));

        Assert.True(resolution.IsValid, string.Join(Environment.NewLine, resolution.Errors));
        Assert.Equal("example-university-engineering", resolution.FormatSpec!.Name);
    }

    [Fact]
    public void TemplateResolver_ShouldResolveHumanitiesTemplate()
    {
        var resolution = new TemplateResolver().Resolve(TemplatePath("example-university-humanities"));

        Assert.True(resolution.IsValid, string.Join(Environment.NewLine, resolution.Errors));
        Assert.Equal("example-university-humanities", resolution.FormatSpec!.Name);
        Assert.Equal("仿宋", resolution.FormatSpec.DefaultFont.EastAsia);
        Assert.Equal(PageNumberStyle.UpperRoman, resolution.FormatSpec.Sections["frontMatter"].PageNumberStyle);
        Assert.Contains(resolution.PageTemplates, layout => layout.Id == "humanities-abstract-note");
    }

    [Fact]
    public void TemplateResolver_ShouldMergeParentAndChildTemplate()
    {
        var resolution = new TemplateResolver().Resolve(TemplatePath("example-university-engineering-variant"));

        Assert.True(resolution.IsValid, string.Join(Environment.NewLine, resolution.Errors));
        Assert.Equal("example-university-engineering-variant", resolution.Template!.Id);
        Assert.Contains(resolution.PageTemplates, layout => layout.Id == "engineering-cover");
        Assert.Equal(3.2, resolution.FormatSpec!.PageSetup.LeftMarginCm);
        Assert.Equal(15, resolution.FormatSpec.Headings[1].Font.SizePt);
    }

    [Fact]
    public void TemplateResolver_ShouldRejectCircularInheritance()
    {
        var root = NewTempDirectory();
        var a = Path.Combine(root, "a");
        var b = Path.Combine(root, "b");
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);
        WriteTemplateJson(a, new TemplatePackage { Id = "a", Name = "A", Version = "1.0.0", Locale = "zh-CN", Extends = new TemplateInheritance { TemplateId = "b" } });
        WriteTemplateJson(b, new TemplatePackage { Id = "b", Name = "B", Version = "1.0.0", Locale = "zh-CN", Extends = new TemplateInheritance { TemplateId = "a" } });

        var result = new TemplateResolver().Resolve(a);

        Assert.Contains(result.Errors, error => error.Code == "template.inheritance.circular");
    }

    [Fact]
    public void FormatSpecMerger_ShouldOverrideScalarValues()
    {
        var merged = new FormatSpecMerger().Merge(new ThesisFormatSpec { Name = "parent" }, JsonNode.Parse("""{"name":"child"}"""));

        Assert.Equal("child", merged.Name);
    }

    [Fact]
    public void FormatSpecMerger_ShouldMergeNestedObjects()
    {
        var parent = new ThesisFormatSpec();

        var merged = new FormatSpecMerger().Merge(parent, JsonNode.Parse("""{"pageSetup":{"topMarginCm":3.3}}"""));

        Assert.Equal(3.3, merged.PageSetup.TopMarginCm);
        Assert.Equal(parent.PageSetup.BottomMarginCm, merged.PageSetup.BottomMarginCm);
    }

    [Fact]
    public void FormatSpecMerger_ShouldReplaceArraysByDefault()
    {
        var merged = new FormatSpecMerger().Merge(new ThesisFormatSpec(), JsonNode.Parse("""{"coverPage":{"fieldOrder":["title"]}}"""));

        Assert.Equal(["title"], merged.CoverPage.FieldOrder);
    }

    [Fact]
    public void FormatSpecMerger_ShouldAllowExplicitNullClearWhenValid()
    {
        var merged = new FormatSpecMerger().Merge(new ThesisFormatSpec(), JsonNode.Parse("""{"headerFooter":{"headerText":null}}"""));

        Assert.Null(merged.HeaderFooter.HeaderText);
    }

    [Fact]
    public void TemplateResolver_ShouldProduceDeterministicResolvedSpec()
    {
        var resolver = new TemplateResolver();

        var first = JsonSerializer.Serialize(resolver.Resolve(TemplatePath("example-university-engineering-variant")).FormatSpec, ThesisJson.Options);
        var second = JsonSerializer.Serialize(resolver.Resolve(TemplatePath("example-university-engineering-variant")).FormatSpec, ThesisJson.Options);

        Assert.Equal(first, second);
    }

    [Fact]
    public void TemplateVariableResolver_ShouldResolveFromMetadata()
    {
        var package = new TemplatePackage
        {
            Variables = [new TemplateVariable { Name = "authorName", Label = "Author", Required = true, SourcePath = "metadata.author" }]
        };
        var result = ResolveVariables(package, new Dictionary<string, string>());

        Assert.Equal("metadata", result.Resolutions.Single().Source);
        Assert.Equal(LoadFullDocument().Document.Metadata.Author, result.Resolutions.Single().Value);
    }

    [Fact]
    public void TemplateVariableResolver_ShouldResolveFromDefaults()
    {
        var package = new TemplatePackage
        {
            Variables = [new TemplateVariable { Name = "visibility", Label = "Visibility", DefaultValue = JsonValue.Create("公开") }]
        };

        var result = ResolveVariables(package, new Dictionary<string, string>());

        Assert.Equal("default", result.Resolutions.Single().Source);
        Assert.Equal("公开", result.Resolutions.Single().Value);
    }

    [Fact]
    public void TemplateVariableResolver_ShouldLetCliVarsOverrideMetadata()
    {
        var package = new TemplatePackage
        {
            Variables = [new TemplateVariable { Name = "authorName", Label = "Author", SourcePath = "metadata.author" }]
        };

        var result = ResolveVariables(package, new Dictionary<string, string> { ["variables.authorName"] = "CLI Author" });

        Assert.Equal("cli", result.Resolutions.Single().Source);
        Assert.Equal("CLI Author", result.Resolutions.Single().Value);
    }

    [Fact]
    public void TemplateVariableResolver_ShouldRejectMissingRequiredVariable()
    {
        var package = new TemplatePackage
        {
            Variables = [new TemplateVariable { Name = "requiredField", Label = "Required", Required = true }]
        };

        var result = ResolveVariables(package, new Dictionary<string, string>());

        Assert.Contains(result.Errors, error => error.Code == "template.variable.requiredMissing");
    }

    [Fact]
    public void TemplateVariableResolver_ShouldWarnForMissingOptionalVariable()
    {
        var package = new TemplatePackage
        {
            Variables = [new TemplateVariable { Name = "optionalField", Label = "Optional" }]
        };

        var result = ResolveVariables(package, new Dictionary<string, string>());

        Assert.Contains(result.Warnings, warning => warning.Code == "template.variable.optionalMissing");
    }

    [Fact]
    public void TemplateVariableResolver_ShouldWarnForUnknownSuppliedVariable()
    {
        var package = new TemplatePackage
        {
            Variables = [new TemplateVariable { Name = "known", Label = "Known", DefaultValue = JsonValue.Create("default") }]
        };

        var result = ResolveVariables(package, new Dictionary<string, string> { ["variables.unknown"] = "value" });

        Assert.Contains(result.Warnings, warning => warning.Code == "template.variable.unknownSupplied");
    }

    [Fact]
    public void TemplateVariableResolver_ShouldRejectDefaultTypeMismatch()
    {
        var package = new TemplatePackage
        {
            Variables = [new TemplateVariable { Name = "count", Label = "Count", Type = TemplateVariableType.Number, DefaultValue = JsonValue.Create("not-a-number") }]
        };

        var result = ResolveVariables(package, new Dictionary<string, string>());

        Assert.Contains(result.Errors, error => error.Code == "template.variable.defaultTypeMismatch");
    }

    [Fact]
    public void TemplateVariableResolver_ShouldEscapeXmlSensitiveText()
    {
        var package = new TemplatePackage
        {
            Variables = [new TemplateVariable { Name = "unsafe", Label = "Unsafe", Required = true }]
        };

        var result = ResolveVariables(package, new Dictionary<string, string> { ["variables.unsafe"] = "<tag>&value" });

        Assert.Equal("&lt;tag&gt;&amp;value", result.Resolutions.Single().Value);
    }

    [Fact]
    public void TemplateVariableResolver_ShouldFormatDate()
    {
        var package = new TemplatePackage
        {
            Variables =
            [
                new TemplateVariable
                {
                    Name = "defenseDate",
                    Label = "Date",
                    Type = TemplateVariableType.Date,
                    Required = true,
                    Format = "yyyy/MM/dd"
                }
            ]
        };

        var result = ResolveVariables(package, new Dictionary<string, string> { ["variables.defenseDate"] = "2026-06-01" });

        Assert.Equal("2026/06/01", result.Resolutions.Single().Value);
    }

    [Fact]
    public void RenderCoverTemplate_ShouldReplaceCoverSectionContent()
    {
        var rendered = RenderTemplateFull();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);
        var text = BodyText(document);

        Assert.Contains("Example University", text);
        Assert.DoesNotContain("作者：", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderCoverTemplate_ShouldRenderMetadataFieldTable()
    {
        var rendered = RenderTemplateFull();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);

        Assert.Contains(document.MainDocumentPart!.Document.Descendants<W.Table>(),
            table => BodyText(table).Contains("论文题目", StringComparison.Ordinal)
                && BodyText(table).Contains("结构化毕业论文 DOCX 渲染引擎设计与实现", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderCoverTemplate_ShouldRenderLogoAsset()
    {
        var rendered = RenderTemplateFull();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);

        Assert.True(document.MainDocumentPart!.ImageParts.Any());
        Assert.True(document.MainDocumentPart.Document.Descendants<W.Drawing>().Any());
    }

    [Fact]
    public void RenderCoverTemplate_ShouldApplyPageSetupOverride()
    {
        var (document, baseDir) = LoadFullDocument();
        ResolveFigurePaths(document, baseDir);
        var format = LoadFormat(Path.Combine(RepoRoot(), "examples", "format-specs", "strict-cn-thesis.json"));
        var layout = new TemplatePageLayout
        {
            Id = "cover-override",
            TargetSectionType = PageTemplateTargetSectionType.Cover,
            InsertPosition = PageTemplateInsertPosition.ReplaceSectionContent,
            PageSetupOverride = new PageSetupSpec { TopMarginCm = 1.1, BottomMarginCm = 1.2, LeftMarginCm = 1.3, RightMarginCm = 1.4 },
            Blocks = [new TextLayoutBlock { Value = "Cover override" }, new PageBreakLayoutBlock()]
        };
        var context = new DocxRenderContext { TemplateId = "override-template", TemplateVersion = "1.0.0", PageTemplates = [layout] };
        var output = TempDocxPath();

        new DocxRenderer().Render(document, format, output, context);

        using var package = WordprocessingDocument.Open(output, false);
        var margin = package.MainDocumentPart!.Document.Body!.Descendants<W.SectionProperties>().First().GetFirstChild<W.PageMargin>()!;
        Assert.Equal(UnitConverter.CentimetersToTwips(1.1).ToString(), margin.Top!.Value.ToString());
    }

    [Fact]
    public void RenderDeclarationTemplate_ShouldRenderDeclarationText()
    {
        var rendered = RenderTemplateFull();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);

        Assert.Contains("原创性声明", BodyText(document), StringComparison.Ordinal);
        Assert.Contains("独立完成", BodyText(document), StringComparison.Ordinal);
    }

    [Fact]
    public void RenderDeclarationTemplate_ShouldRenderSignatureFields()
    {
        var rendered = RenderTemplateFull();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);

        Assert.Contains("签名", BodyText(document), StringComparison.Ordinal);
        Assert.Contains("2026-06-01", BodyText(document), StringComparison.Ordinal);
    }

    [Fact]
    public void RenderPageTemplate_ShouldResolveVariables()
    {
        var rendered = RenderTemplateFull(new Dictionary<string, string> { ["variables.confidentiality"] = "内部" });
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);

        Assert.Contains("内部", BodyText(document), StringComparison.Ordinal);
    }

    [Fact]
    public void RenderPageTemplate_ShouldRejectMissingRequiredAsset()
    {
        var (document, baseDir) = LoadFullDocument();
        ResolveFigurePaths(document, baseDir);
        var format = LoadFormat(TemplateFormatPath("example-university-engineering"));
        var context = new DocxRenderContext
        {
            TemplateId = "bad-asset",
            TemplateVersion = "1.0.0",
            PageTemplates =
            [
                new TemplatePageLayout
                {
                    Id = "cover-with-missing-asset",
                    TargetSectionType = PageTemplateTargetSectionType.Cover,
                    InsertPosition = PageTemplateInsertPosition.ReplaceSectionContent,
                    Blocks = [new ImageLayoutBlock { AssetId = "missingLogo" }]
                }
            ]
        };

        Assert.Throws<InvalidOperationException>(() => new DocxRenderer().Render(document, format, TempDocxPath(), context));
    }

    [Fact]
    public void Render_WithTemplatePackage_ShouldProduceValidOpenXml()
    {
        var rendered = RenderTemplateFull();

        var result = new OpenXmlPackageValidator().Validate(rendered.DocxPath);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void RenderHumanitiesTemplate_ShouldApplyXmlLevelContracts()
    {
        var rendered = RenderTemplateDocument(
            "example-university-humanities",
            Path.Combine(RepoRoot(), "examples", "template-regression", "documents", "example-university-humanities-full", "document.json"),
            new Dictionary<string, string>
            {
                ["variables.submissionDate"] = "2026-06-15",
                ["variables.researchField"] = "数字人文",
                ["variables.reviewStatus"] = "公开评阅"
            });

        var validation = new OpenXmlPackageValidator().Validate(rendered.DocxPath);
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);
        var inspect = new DocxInspector().Inspect(rendered.DocxPath);
        var bodyStyle = document.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<W.Style>().Single(style => style.StyleId?.Value == StyleIds.ThesisBody);
        var heading1 = document.MainDocumentPart.StyleDefinitionsPart.Styles.Elements<W.Style>().Single(style => style.StyleId?.Value == StyleIds.Heading1);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.Contains("Example Humanities College", BodyText(document), StringComparison.Ordinal);
        Assert.Contains("学术诚信声明", BodyText(document), StringComparison.Ordinal);
        Assert.Contains("数字人文", BodyText(document), StringComparison.Ordinal);
        Assert.Contains("公开评阅", BodyText(document), StringComparison.Ordinal);
        Assert.Contains(inspect.SectionPageNumberFormats, value => value == "upperRoman");
        Assert.Contains(inspect.TemplateRendering.RenderedPageTemplates, value => value == "humanities-cover");
        Assert.Contains(inspect.TemplateRendering.RenderedPageTemplates, value => value == "humanities-declaration");
        Assert.Contains(inspect.TemplateRendering.RenderedPageTemplates, value => value == "humanities-abstract-note");
        Assert.Contains(inspect.TemplateRendering.RenderedAssets, value => value == "humanitiesSeal");
        Assert.Contains(document.MainDocumentPart.Document.Descendants<W.SimpleField>(), field => field.Instruction?.Value == "TOC \\o \"1-2\" \\h \\z \\u");
        Assert.Equal("仿宋", bodyStyle.GetFirstChild<W.StyleRunProperties>()!.GetFirstChild<W.RunFonts>()!.EastAsia!.Value);
        Assert.Equal("300", bodyStyle.GetFirstChild<W.StyleParagraphProperties>()!.GetFirstChild<W.SpacingBetweenLines>()!.Line!.Value);
        Assert.Equal("30", heading1.GetFirstChild<W.StyleRunProperties>()!.GetFirstChild<W.FontSize>()!.Val!.Value);
        Assert.NotNull(heading1.GetFirstChild<W.StyleParagraphProperties>()!.GetFirstChild<W.PageBreakBefore>());
    }

    [Fact]
    public void Cli_TemplateList_ShouldListExampleTemplates()
    {
        var result = CliRunner.Run(RepoRoot(), "template", "list", "--templates", Path.Combine(RepoRoot(), "examples", "templates"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("example-university-engineering", result.StandardOutput);
        Assert.Contains("example-university-humanities", result.StandardOutput);
    }

    [Fact]
    public void Cli_TemplateInspect_ShouldOutputTemplateSummary()
    {
        var result = CliRunner.Run(RepoRoot(), "template", "inspect", "--template", TemplatePath("example-university-engineering"));
        var json = JsonNode.Parse(result.StandardOutput)!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("example-university-engineering", json["id"]!.GetValue<string>());
        Assert.True(json["pageTemplates"]!.AsArray().Count >= 2);
    }

    [Fact]
    public void Cli_TemplateValidate_ShouldReturnValidForExamples()
    {
        var result = CliRunner.Run(RepoRoot(), "template", "validate", "--template", TemplatePath("example-university-engineering"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Template valid", result.StandardOutput);
    }

    [Fact]
    public void Cli_TemplateValidate_ShouldSupportJsonOutput()
    {
        var result = CliRunner.Run(RepoRoot(), "template", "validate", "--template", TemplatePath("example-university-engineering"), "--json");
        var json = JsonNode.Parse(result.StandardOutput)!;

        Assert.Equal(0, result.ExitCode);
        Assert.True(json["isValid"]!.GetValue<bool>());
        Assert.NotNull(json["errors"]);
        Assert.NotNull(json["warnings"]);
    }

    [Fact]
    public void Cli_TemplateResolve_ShouldWriteResolvedFormatSpec()
    {
        var outPath = Path.Combine(NewTempDirectory(), "resolved.json");

        var result = CliRunner.Run(RepoRoot(), "template", "resolve", "--template", TemplatePath("example-university-engineering-variant"), "--out", outPath);
        var json = JsonNode.Parse(File.ReadAllText(outPath))!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("1.2.0", json["schemaVersion"]!.GetValue<string>());
        Assert.Equal(3.2, json["pageSetup"]!["leftMarginCm"]!.GetValue<double>());
    }

    [Fact]
    public void Cli_TemplateResolve_ShouldSupportJsonReport()
    {
        var result = CliRunner.Run(RepoRoot(), "template", "resolve", "--template", TemplatePath("example-university-engineering-variant"), "--json");
        var json = JsonNode.Parse(result.StandardOutput)!;

        Assert.Equal(0, result.ExitCode);
        Assert.True(json["success"]!.GetValue<bool>());
        Assert.Equal("example-university-engineering-variant", json["templateId"]!.GetValue<string>());
        Assert.Equal("1.0.0", json["versionReport"]!["reportVersion"]!.GetValue<string>());
        Assert.Contains(json["versionReport"]!["checks"]!.AsArray(), check => check!["kind"]!.GetValue<string>() == "templatePackage");
        Assert.Contains(json["versionReport"]!["checks"]!.AsArray(), check => check!["kind"]!.GetValue<string>() == "thesisFormatSpec");
    }

    [Fact]
    public void Schema_ShouldValidateFormatCandidateDecisionsAndProposalReport()
    {
        var directory = NewTempDirectory();
        var inputs = WriteCandidateInputs(directory, CandidateField("$.bodyParagraph.lineSpacingMultiple", "1.25"));
        var decisionsPath = WriteDecisions(directory, ReviewDecisions(false, Decision("$.bodyParagraph.lineSpacingMultiple", FormatCandidateDecisionKind.Accept)));
        var proposalReportPath = Path.Combine(directory, "template-candidate-proposal-report.json");
        File.WriteAllText(proposalReportPath, JsonSerializer.Serialize(new TemplateCandidateProposalReport
        {
            Status = "pass",
            SourceTemplate = TemplatePath("example-university-engineering"),
            ProposedTemplate = Path.Combine(directory, "proposed-template"),
            SourceCandidateFormatSpec = inputs.CandidateFormatPath,
            SourceCandidateReport = inputs.CandidateReportPath,
            DecisionsPath = decisionsPath,
            ChaosLevel = "low",
            CandidateFieldCount = 1,
            AcceptedCount = 1,
            AppliedFields =
            [
                new TemplateCandidateProposalAppliedField
                {
                    FieldPath = "$.bodyParagraph.lineSpacingMultiple",
                    Decision = "accept",
                    Reviewer = "reviewer-a",
                    Reason = "Reviewed line spacing evidence.",
                    ValuePreview = "1.25",
                    EvidencePaths = ["paragraphs[0]"]
                }
            ]
        }, ThesisJson.Options));

        var validator = new ThesisSchemaValidator();
        var decisions = validator.ValidateFormatCandidateDecisionsFile(decisionsPath, SchemaPath("format-candidate-decisions.schema.json"));
        var report = validator.ValidateTemplateCandidateProposalReportFile(proposalReportPath, SchemaPath("template-candidate-proposal-report.schema.json"));

        Assert.True(decisions.IsValid, string.Join(Environment.NewLine, decisions.Errors));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Errors));
    }

    [Fact]
    public void Cli_TemplateScaffoldCandidateDecisions_ShouldWriteEveryGeneratedField()
    {
        var directory = NewTempDirectory();
        var inputs = WriteCandidateInputs(
            directory,
            CandidateField("$.bodyParagraph.lineSpacingMultiple", "1.25"),
            CandidateField("$.defaultFont.sizePt", "11"));
        var decisionsPath = Path.Combine(directory, "format-candidate-decisions.json");

        var result = CliRunner.Run(
            RepoRoot(),
            "template",
            "scaffold-candidate-decisions",
            "--candidate-format",
            inputs.CandidateFormatPath,
            "--candidate-report",
            inputs.CandidateReportPath,
            "--reviewer",
            "reviewer-a",
            "--out",
            decisionsPath);
        var decisions = JsonSerializer.Deserialize<FormatCandidateDecisionSet>(File.ReadAllText(decisionsPath), ThesisJson.Options)!;
        var schema = new ThesisSchemaValidator().ValidateFormatCandidateDecisionsFile(decisionsPath, SchemaPath("format-candidate-decisions.schema.json"));

        Assert.Equal(0, result.ExitCode);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Equal(2, decisions.Decisions.Count);
        Assert.All(decisions.Decisions, decision => Assert.Equal(FormatCandidateDecisionKind.Reject, decision.Decision));
    }

    [Fact]
    public void Cli_TemplateScaffoldCandidateDecisions_ShouldRejectModifyAsDefaultDecision()
    {
        var directory = NewTempDirectory();
        var inputs = WriteCandidateInputs(directory, CandidateField("$.bodyParagraph.lineSpacingMultiple", "1.25"));

        var result = CliRunner.Run(
            RepoRoot(),
            "template",
            "scaffold-candidate-decisions",
            "--candidate-format",
            inputs.CandidateFormatPath,
            "--candidate-report",
            inputs.CandidateReportPath,
            "--reviewer",
            "reviewer-a",
            "--default-decision",
            "modify",
            "--out",
            Path.Combine(directory, "decisions.json"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Use accept or reject", result.StandardError);
    }

    [Fact]
    public void Cli_TemplateProposeFromCandidate_ShouldApplyAcceptedAndModifiedFieldsToCopiedTemplate()
    {
        var directory = NewTempDirectory();
        var proposedTemplate = Path.Combine(directory, "proposed-template");
        var reportPath = Path.Combine(directory, "template-candidate-proposal-report.json");
        var markdownPath = Path.Combine(directory, "template-candidate-proposal-report.md");
        var inputs = WriteCandidateInputs(
            directory,
            CandidateField("$.bodyParagraph.lineSpacingMultiple", "1.25"),
            CandidateField("$.defaultFont.sizePt", "11"));
        var decisionsPath = WriteDecisions(
            directory,
            ReviewDecisions(
                false,
                Decision("$.bodyParagraph.lineSpacingMultiple", FormatCandidateDecisionKind.Accept),
                Decision("$.defaultFont.sizePt", FormatCandidateDecisionKind.Modify, JsonValue.Create(10.5))));

        var result = CliRunner.Run(
            RepoRoot(),
            "template",
            "propose-from-candidate",
            "--template",
            TemplatePath("example-university-engineering"),
            "--candidate-format",
            inputs.CandidateFormatPath,
            "--candidate-report",
            inputs.CandidateReportPath,
            "--decisions",
            decisionsPath,
            "--out",
            proposedTemplate,
            "--report",
            reportPath,
            "--markdown",
            markdownPath);
        var proposed = LoadFormat(Path.Combine(proposedTemplate, "format-spec.json"));
        var report = JsonSerializer.Deserialize<TemplateCandidateProposalReport>(File.ReadAllText(reportPath), ThesisJson.Options)!;
        var validation = CliRunner.Run(RepoRoot(), "template", "validate", "--template", proposedTemplate);
        var schema = new ThesisSchemaValidator().ValidateTemplateCandidateProposalReportFile(reportPath, SchemaPath("template-candidate-proposal-report.schema.json"));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, validation.ExitCode);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Equal(1.25, proposed.BodyParagraph.LineSpacingMultiple);
        Assert.Equal(10.5, proposed.DefaultFont.SizePt);
        Assert.Equal("pass", report.Status);
        Assert.Equal(1, report.AcceptedCount);
        Assert.Equal(1, report.ModifiedCount);
        Assert.Contains("Template Candidate Proposal Report", File.ReadAllText(markdownPath));
    }

    [Fact]
    public void Cli_TemplateProposeFromCandidate_ShouldLeaveRejectedFieldsUnchanged()
    {
        var directory = NewTempDirectory();
        var proposedTemplate = Path.Combine(directory, "proposed-template");
        var reportPath = Path.Combine(directory, "proposal.json");
        var original = LoadFormat(TemplateFormatPath("example-university-engineering"));
        var inputs = WriteCandidateInputs(directory, CandidateField("$.bodyParagraph.lineSpacingMultiple", "1.25"));
        var decisionsPath = WriteDecisions(directory, ReviewDecisions(false, Decision("$.bodyParagraph.lineSpacingMultiple", FormatCandidateDecisionKind.Reject)));

        var result = CliRunner.Run(
            RepoRoot(),
            "template",
            "propose-from-candidate",
            "--template",
            TemplatePath("example-university-engineering"),
            "--candidate-format",
            inputs.CandidateFormatPath,
            "--candidate-report",
            inputs.CandidateReportPath,
            "--decisions",
            decisionsPath,
            "--out",
            proposedTemplate,
            "--report",
            reportPath);
        var proposed = LoadFormat(Path.Combine(proposedTemplate, "format-spec.json"));
        var report = JsonSerializer.Deserialize<TemplateCandidateProposalReport>(File.ReadAllText(reportPath), ThesisJson.Options)!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(original.BodyParagraph.LineSpacingMultiple, proposed.BodyParagraph.LineSpacingMultiple);
        Assert.Equal(1, report.RejectedCount);
        Assert.Empty(report.AppliedFields);
    }

    [Fact]
    public void Cli_TemplateProposeFromCandidate_ShouldRejectHighChaosAcceptedFieldWithoutRiskAcceptance()
    {
        var directory = NewTempDirectory();
        var proposedTemplate = Path.Combine(directory, "proposed-template");
        var reportPath = Path.Combine(directory, "proposal.json");
        var inputs = WriteCandidateInputs(directory, CandidateField("$.bodyParagraph.lineSpacingMultiple", "1.25"), chaosLevel: "high", chaosScore: 0.82);
        var decisionsPath = WriteDecisions(directory, ReviewDecisions(false, Decision("$.bodyParagraph.lineSpacingMultiple", FormatCandidateDecisionKind.Accept)));

        var result = CliRunner.Run(
            RepoRoot(),
            "template",
            "propose-from-candidate",
            "--template",
            TemplatePath("example-university-engineering"),
            "--candidate-format",
            inputs.CandidateFormatPath,
            "--candidate-report",
            inputs.CandidateReportPath,
            "--decisions",
            decisionsPath,
            "--out",
            proposedTemplate,
            "--report",
            reportPath);
        var report = JsonSerializer.Deserialize<TemplateCandidateProposalReport>(File.ReadAllText(reportPath), ThesisJson.Options)!;

        Assert.Equal(2, result.ExitCode);
        Assert.False(Directory.Exists(proposedTemplate));
        Assert.Equal("fail", report.Status);
        Assert.Contains(report.Issues, issue => issue.Code == "templateCandidateProposal.riskAcceptance.required");
    }

    [Fact]
    public void Cli_TemplateProposeFromCandidate_ShouldRequireDecisionForEveryGeneratedField()
    {
        var directory = NewTempDirectory();
        var proposedTemplate = Path.Combine(directory, "proposed-template");
        var reportPath = Path.Combine(directory, "proposal.json");
        var inputs = WriteCandidateInputs(
            directory,
            CandidateField("$.bodyParagraph.lineSpacingMultiple", "1.25"),
            CandidateField("$.defaultFont.sizePt", "11"));
        var decisionsPath = WriteDecisions(directory, ReviewDecisions(false, Decision("$.bodyParagraph.lineSpacingMultiple", FormatCandidateDecisionKind.Accept)));

        var result = CliRunner.Run(
            RepoRoot(),
            "template",
            "propose-from-candidate",
            "--template",
            TemplatePath("example-university-engineering"),
            "--candidate-format",
            inputs.CandidateFormatPath,
            "--candidate-report",
            inputs.CandidateReportPath,
            "--decisions",
            decisionsPath,
            "--out",
            proposedTemplate,
            "--report",
            reportPath);
        var report = JsonSerializer.Deserialize<TemplateCandidateProposalReport>(File.ReadAllText(reportPath), ThesisJson.Options)!;

        Assert.Equal(2, result.ExitCode);
        Assert.False(Directory.Exists(proposedTemplate));
        Assert.Contains(report.Issues, issue => issue.Code == "templateCandidateProposal.decision.missing");
    }

    [Fact]
    public void Cli_TemplateDiff_ShouldReportChangedRules()
    {
        var result = CliRunner.Run(RepoRoot(), "template", "diff", "--base", TemplatePath("example-university-engineering"), "--target", TemplatePath("example-university-engineering-variant"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("$.formatSpec.pageSetup.leftMarginCm", result.StandardOutput);
        Assert.Contains("headerFooter", result.StandardOutput);
    }

    [Fact]
    public void Cli_TemplateDiff_ShouldSupportJsonOutput()
    {
        var result = CliRunner.Run(RepoRoot(), "template", "diff", "--base", TemplatePath("example-university-engineering"), "--target", TemplatePath("example-university-engineering-variant"), "--json");
        var json = JsonNode.Parse(result.StandardOutput)!;

        Assert.Equal(0, result.ExitCode);
        Assert.True(json["changes"]!.AsArray().Count > 0);
    }

    [Fact]
    public void Cli_Render_ShouldRejectFormatAndTemplateTogether()
    {
        var result = CliRunner.Run(RepoRoot(),
            "render",
            "--document", FullDocumentPath(),
            "--format", Path.Combine(RepoRoot(), "examples", "format-specs", "strict-cn-thesis.json"),
            "--template", TemplatePath("example-university-engineering"),
            "--out", TempDocxPath());

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("exactly one", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cli_Render_WithTemplate_ShouldProduceDocx()
    {
        var output = TempDocxPath();

        var result = CliRunner.Run(RepoRoot(), "render", "--document", FullDocumentPath(), "--template", TemplatePath("example-university-engineering"), "--out", output);
        var validation = new OpenXmlPackageValidator().Validate(output);

        Assert.Equal(0, result.ExitCode);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.Equal("example-university-engineering", new DocxInspector().Inspect(output).TemplateRendering.TemplateId);
    }

    [Fact]
    public void Cli_Render_WithTemplateVars_ShouldOverrideMetadata()
    {
        var output = TempDocxPath();

        var result = CliRunner.Run(RepoRoot(),
            "render",
            "--document", FullDocumentPath(),
            "--template", TemplatePath("example-university-engineering"),
            "--var", "variables.confidentiality=内部",
            "--out", output);
        using var document = WordprocessingDocument.Open(output, false);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("内部", BodyText(document), StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateDiff_ShouldCompareResolvedSpecs()
    {
        var diff = CompareExampleTemplates();

        Assert.Equal("example-university-engineering", diff.BaseTemplateId);
        Assert.Equal("example-university-engineering-variant", diff.TargetTemplateId);
        Assert.NotEmpty(diff.Changes);
    }

    [Fact]
    public void TemplateDiff_ShouldDetectMarginChange()
    {
        var diff = CompareExampleTemplates();

        Assert.Contains(diff.Changes, change => change.Path == "$.formatSpec.pageSetup.leftMarginCm");
    }

    [Fact]
    public void TemplateDiff_ShouldDetectHeadingStyleChange()
    {
        var diff = CompareExampleTemplates();

        Assert.Contains(diff.Changes, change => change.Path.Contains(".headings.1.font.sizePt", StringComparison.Ordinal));
    }

    [Fact]
    public void TemplateDiff_ShouldDetectHeaderTextChange()
    {
        var diff = CompareExampleTemplates();

        Assert.Contains(diff.Changes, change => change.Path == "$.formatSpec.headerFooter.headerText");
    }

    [Fact]
    public void TemplateDiff_ShouldDetectBibliographyIndentChange()
    {
        var diff = CompareExampleTemplates();

        Assert.Contains(diff.Changes, change => change.Path.EndsWith(".bibliography.entryParagraph.hangingIndentCm", StringComparison.Ordinal));
    }

    [Fact]
    public void TemplateDiff_ShouldClassifyChangeCategory()
    {
        var diff = CompareExampleTemplates();

        Assert.Contains(diff.Changes, change => change.Path.Contains(".tables.", StringComparison.Ordinal) && change.Category == TemplateDiffCategory.Table);
        Assert.Contains(diff.Changes, change => change.Path.Contains(".pageSetup.", StringComparison.Ordinal) && change.Category == TemplateDiffCategory.PageSetup);
    }

    [Fact]
    public void TemplateDiff_ShouldProduceDeterministicOrder()
    {
        var first = CompareExampleTemplates().Changes.Select(change => change.Path).ToList();
        var second = CompareExampleTemplates().Changes.Select(change => change.Path).ToList();

        Assert.Equal(first, second);
        Assert.Equal(first.OrderBy(path => path, StringComparer.Ordinal), first);
    }

    [Fact]
    public void FormatRuleCoverage_ShouldIncludeCoreCategories()
    {
        var coverage = BuildCoverage();
        var categories = coverage.Rules.Select(rule => rule.Category).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("page setup", categories);
        Assert.Contains("tables", categories);
        Assert.Contains("equations", categories);
        Assert.Contains("template inheritance", categories);
    }

    [Fact]
    public void FormatRuleCoverage_ShouldMarkImplementedRulesSupported()
    {
        var coverage = BuildCoverage();

        Assert.Contains(coverage.Rules, rule => rule.RuleId == "tables" && rule.Status == "supported" && rule.RendererCovered);
        Assert.Contains(coverage.Rules, rule => rule.RuleId == "template-inheritance" && rule.Status == "supported" && rule.ValidatorCovered);
        Assert.Contains(coverage.Rules, rule => rule.RuleId == "page-template-image" && rule.Status == "supported");
        Assert.Contains(coverage.Rules, rule => rule.RuleId == "document-feature-advanced-table" && rule.Status == "supported");
    }

    [Fact]
    public void FormatRuleCoverage_ShouldMarkKnownGapsPartialOrPlanned()
    {
        var coverage = BuildCoverage();

        Assert.Contains(coverage.Rules, rule => rule.RuleId == "assets" && rule.Status is "partial" or "planned");
        Assert.Contains(coverage.Rules, rule => rule.RuleId == "page-templates" && rule.Status is "partial" or "planned");
    }

    [Fact]
    public void Cli_TemplateCoverage_ShouldWriteCoverageJson()
    {
        var outPath = Path.Combine(NewTempDirectory(), "coverage.json");

        var result = CliRunner.Run(RepoRoot(), "template", "coverage", "--template", TemplatePath("example-university-engineering"), "--out", outPath);
        var json = JsonNode.Parse(File.ReadAllText(outPath))!;

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("example-university-engineering", json["templateId"]!.GetValue<string>());
        Assert.True(json["rules"]!.AsArray().Count >= 16);
    }

    [Fact]
    public void FormatValidator_WithTemplate_ShouldResolveTemplateAndValidateDocx()
    {
        var rendered = RenderTemplateFull();

        var result = new FormatConformanceValidator().Validate(rendered.DocxPath, TemplatePath("example-university-engineering"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Contains("template.cover", result.CheckedRules);
    }

    [Fact]
    public void FormatValidator_WithTemplate_ShouldNotRequireAppendixTemplateWhenDocumentHasNoAppendix()
    {
        var templatePath = WriteMutatedTemplate(node =>
        {
            node["pageTemplates"]!.AsArray().Add(new JsonObject
            {
                ["id"] = "engineering-appendix-marker",
                ["targetSectionType"] = "appendix",
                ["insertPosition"] = "beforeSection",
                ["blocks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "text",
                    ["value"] = "Appendix",
                    ["style"] = "ThesisBody",
                    ["alignment"] = "center"
                })
            });
        });
        var documentPath = Path.Combine(RepoRoot(), "examples", "simple-thesis", "document.json");
        var rendered = RenderTemplateDocumentFromPath(templatePath, documentPath);

        var result = new FormatConformanceValidator().Validate(rendered.DocxPath, templatePath);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.DoesNotContain(result.Errors, error => error.Code == "template.pageTemplate.missing");
    }

    [Fact]
    public void FormatValidator_ShouldCheckCoverRequiredFields()
    {
        var result = new FormatConformanceValidator().Validate(RenderTemplateFull().DocxPath, TemplatePath("example-university-engineering"));

        Assert.Contains("template.cover", result.CheckedRules);
        Assert.DoesNotContain(result.Errors, error => error.Code == "template.cover.requiredFieldsMissing");
    }

    [Fact]
    public void FormatValidator_ShouldCheckDeclarationText()
    {
        var result = new FormatConformanceValidator().Validate(RenderTemplateFull().DocxPath, TemplatePath("example-university-engineering"));

        Assert.Contains("template.declaration", result.CheckedRules);
        Assert.DoesNotContain(result.Errors, error => error.Code == "template.declaration.textMissing");
    }

    [Fact]
    public void FormatValidator_ShouldCheckRequiredAssetRendering()
    {
        var nonTemplateDocx = TestRenderHelper.RenderFullThesis().DocxPath;

        var result = new FormatConformanceValidator().Validate(nonTemplateDocx, TemplatePath("example-university-engineering"));

        Assert.Contains(result.Errors, error => error.Code == "template.asset.notRendered");
    }

    [Fact]
    public void Cli_Validate_WithTemplate_ShouldReturnValid()
    {
        var rendered = RenderTemplateFull();

        var result = CliRunner.Run(RepoRoot(), "validate", "--docx", rendered.DocxPath, "--template", TemplatePath("example-university-engineering"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Valid", result.StandardOutput);
    }

    [Fact]
    public void Render_WithTemplate_ShouldWriteCustomTemplateProperties()
    {
        var rendered = RenderTemplateFull();
        using var document = WordprocessingDocument.Open(rendered.DocxPath, false);
        var propertyNames = document.CustomFilePropertiesPart!.Properties!.Elements<DocumentFormat.OpenXml.CustomProperties.CustomDocumentProperty>()
            .Select(property => property.Name?.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("ThesisDocx.TemplateId", propertyNames);
        Assert.Contains("ThesisDocx.RenderedPageTemplates", propertyNames);
    }

    [Fact]
    public void Inspect_ShouldReadCustomTemplateProperties()
    {
        var inspect = new DocxInspector().Inspect(RenderTemplateFull().DocxPath);

        Assert.Equal("example-university-engineering", inspect.TemplateRendering.TemplateId);
        Assert.Equal("1.0.0", inspect.TemplateRendering.TemplateVersion);
    }

    [Fact]
    public void Inspect_ShouldIncludeRenderedPageTemplates()
    {
        var inspect = new DocxInspector().Inspect(RenderTemplateFull().DocxPath);

        Assert.Contains("engineering-cover", inspect.TemplateRendering.RenderedPageTemplates);
        Assert.Contains("engineering-declaration", inspect.TemplateRendering.RenderedPageTemplates);
    }

    [Fact]
    public void Inspect_ShouldIncludeRenderedAssets()
    {
        var inspect = new DocxInspector().Inspect(RenderTemplateFull().DocxPath);

        Assert.Contains("collegeLogo", inspect.TemplateRendering.RenderedAssets);
        Assert.True(inspect.TemplateRendering.CoverSummary.HasLogoDrawing);
    }

    private static ThesisInputValidationResult ValidateTemplateSchema(string templateDirectory)
    {
        return new ThesisSchemaValidator().ValidateTemplateFile(
            Path.Combine(templateDirectory, "template.json"),
            Path.Combine(RepoRoot(), "schemas", "template-package.schema.json"));
    }

    private static string SchemaPath(string name)
    {
        return Path.Combine(RepoRoot(), "schemas", name);
    }

    private static CandidateInputPaths WriteCandidateInputs(
        string directory,
        params DocxFormatCandidateField[] fields)
    {
        return WriteCandidateInputs(directory, fields, "low", 0.1);
    }

    private static CandidateInputPaths WriteCandidateInputs(
        string directory,
        DocxFormatCandidateField field,
        string chaosLevel,
        double chaosScore)
    {
        return WriteCandidateInputs(directory, [field], chaosLevel, chaosScore);
    }

    private static CandidateInputPaths WriteCandidateInputs(
        string directory,
        IReadOnlyList<DocxFormatCandidateField> fields,
        string chaosLevel,
        double chaosScore)
    {
        Directory.CreateDirectory(directory);
        var candidateFormatPath = Path.Combine(directory, "candidate-format-spec.json");
        var candidateReportPath = Path.Combine(directory, "format-candidate-report.json");
        var candidateSpec = JsonSerializer.SerializeToNode(new ThesisFormatSpec { Name = "candidate-test" }, ThesisJson.Options)!.AsObject();
        foreach (var field in fields)
        {
            SetJsonPath(candidateSpec, field.Path, CandidateValue(field.Value));
        }

        var report = new DocxFormatCandidateReport
        {
            SourceExtraction = "synthetic-extraction.json",
            CandidateStatus = chaosLevel == "high" ? "needsReview" : "draft",
            CandidateFormatSpecName = "candidate-test",
            ChaosLevel = chaosLevel,
            ChaosScore = chaosScore,
            GeneratedFieldCount = fields.Count,
            GeneratedFields = fields.ToList(),
            ClustersUsed = [],
            UnresolvedItems = [],
            Warnings = [],
            RecommendedReviewSteps = ["Review every generated field before applying it to a template."]
        };
        File.WriteAllText(candidateFormatPath, candidateSpec.ToJsonString(ThesisJson.Options));
        File.WriteAllText(candidateReportPath, JsonSerializer.Serialize(report, ThesisJson.Options));
        return new CandidateInputPaths(candidateFormatPath, candidateReportPath);
    }

    private static DocxFormatCandidateField CandidateField(string path, string value)
    {
        return new DocxFormatCandidateField
        {
            Path = path,
            Value = value,
            SourceClusterId = "cluster-body",
            Confidence = 0.9,
            EvidencePaths = ["paragraphs[0]"],
            Reason = "Synthetic candidate field for template proposal tests."
        };
    }

    private static FormatCandidateDecisionSet ReviewDecisions(bool riskAccepted, params FormatCandidateDecision[] decisions)
    {
        return new FormatCandidateDecisionSet
        {
            SourceCandidateFormatSpec = "candidate-format-spec.json",
            SourceCandidateReport = "format-candidate-report.json",
            Reviewer = "reviewer-a",
            ReviewedAt = "2026-05-13",
            RiskAccepted = riskAccepted,
            RiskAcceptanceReason = riskAccepted ? "Reviewed high-chaos evidence and accepted the explicitly listed fields only." : string.Empty,
            Decisions = decisions.ToList()
        };
    }

    private static FormatCandidateDecision Decision(string path, FormatCandidateDecisionKind kind, JsonNode? value = null)
    {
        return new FormatCandidateDecision
        {
            FieldPath = path,
            Decision = kind,
            Value = value,
            Reason = kind switch
            {
                FormatCandidateDecisionKind.Accept => "Reviewed and accepted candidate evidence.",
                FormatCandidateDecisionKind.Modify => "Reviewed evidence and corrected the candidate value.",
                _ => "Rejected after review."
            },
            EvidencePaths = ["paragraphs[0]"]
        };
    }

    private static string WriteDecisions(string directory, FormatCandidateDecisionSet decisions)
    {
        var decisionsPath = Path.Combine(directory, "format-candidate-decisions.json");
        File.WriteAllText(decisionsPath, JsonSerializer.Serialize(decisions, ThesisJson.Options));
        return decisionsPath;
    }

    private static JsonNode CandidateValue(string raw)
    {
        if (bool.TryParse(raw, out var boolean))
        {
            return JsonValue.Create(boolean)!;
        }

        if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            return JsonValue.Create(number)!;
        }

        return JsonValue.Create(raw)!;
    }

    private static void SetJsonPath(JsonObject root, string path, JsonNode value)
    {
        var segments = path[2..].Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

    private sealed record CandidateInputPaths(string CandidateFormatPath, string CandidateReportPath);

    private static string WriteMutatedTemplate(Action<JsonNode> mutate)
    {
        var source = JsonNode.Parse(File.ReadAllText(Path.Combine(TemplatePath("example-university-engineering"), "template.json")))!;
        mutate(source);
        var directory = NewTempDirectory();
        File.Copy(TemplateFormatPath("example-university-engineering"), Path.Combine(directory, "format-spec.json"));
        var assetDirectory = Path.Combine(directory, "assets");
        Directory.CreateDirectory(assetDirectory);
        File.Copy(Path.Combine(TemplatePath("example-university-engineering"), "assets", "logo-placeholder.png"), Path.Combine(assetDirectory, "logo-placeholder.png"));
        File.WriteAllText(Path.Combine(directory, "template.json"), source.ToJsonString(ThesisJson.Options));
        return directory;
    }

    private static string WriteTemplateDirectory(TemplatePackage package)
    {
        var directory = NewTempDirectory();
        WriteTemplateJson(directory, package);
        return directory;
    }

    private static void WriteTemplateJson(string directory, TemplatePackage package)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "template.json"), JsonSerializer.Serialize(package, ThesisJson.Options));
    }

    private static (IReadOnlyList<TemplateVariableResolution> Resolutions, List<TemplateIssue> Errors, List<TemplateIssue> Warnings) ResolveVariables(
        TemplatePackage package,
        IReadOnlyDictionary<string, string> cli)
    {
        var errors = new List<TemplateIssue>();
        var warnings = new List<TemplateIssue>();
        var resolutions = new TemplateVariableResolver().Resolve(package, LoadFullDocument().Document, cli, errors, warnings);
        return (resolutions, errors, warnings);
    }

    private static RenderedTemplateDocx RenderTemplateFull(IReadOnlyDictionary<string, string>? cliVariables = null)
    {
        return RenderTemplateDocument("example-university-engineering", FullDocumentPath(), cliVariables ?? new Dictionary<string, string>());
    }

    private static RenderedTemplateDocx RenderTemplateDocument(string templateName, string documentPath, IReadOnlyDictionary<string, string>? cliVariables = null)
    {
        return RenderTemplateDocumentFromPath(TemplatePath(templateName), documentPath, cliVariables);
    }

    private static RenderedTemplateDocx RenderTemplateDocumentFromPath(string templatePath, string documentPath, IReadOnlyDictionary<string, string>? cliVariables = null)
    {
        var (document, baseDir) = LoadDocument(documentPath);
        ResolveFigurePaths(document, baseDir);
        var resolution = new TemplateResolver().Resolve(templatePath, document, cliVariables ?? new Dictionary<string, string>());
        if (!resolution.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, resolution.Errors));
        }

        var context = new DocxRenderContext
        {
            TemplateId = resolution.Template!.Id,
            TemplateVersion = resolution.Template.Version,
            TemplateSchool = resolution.Template.School,
            TemplateCollege = resolution.Template.College,
            ResolvedFormatSpecVersion = resolution.FormatSpec!.SchemaVersion,
            PageTemplates = resolution.PageTemplates,
            Variables = resolution.Variables.Where(variable => variable.Value is not null).ToDictionary(variable => variable.Name, variable => variable.Value!, StringComparer.Ordinal),
            Assets = resolution.Assets.ToDictionary(asset => asset.Id, StringComparer.Ordinal)
        };
        var output = TempDocxPath();
        new DocxRenderer().Render(document, resolution.FormatSpec!, output, context);
        return new RenderedTemplateDocx(output, resolution);
    }

    private static (ThesisDocument Document, string BaseDir) LoadFullDocument()
    {
        return LoadDocument(FullDocumentPath());
    }

    private static (ThesisDocument Document, string BaseDir) LoadDocument(string path)
    {
        var document = JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(path), ThesisJson.Options)!;
        return (document, Path.GetDirectoryName(path)!);
    }

    private static ThesisFormatSpec LoadFormat(string path)
    {
        return JsonSerializer.Deserialize<ThesisFormatSpec>(File.ReadAllText(path), ThesisJson.Options)!;
    }

    private static void ResolveFigurePaths(ThesisDocument document, string baseDir)
    {
        foreach (var figure in document.Sections.SelectMany(section => section.Blocks).OfType<FigureBlock>())
        {
            if (!string.IsNullOrWhiteSpace(figure.ImagePath) && !Path.IsPathRooted(figure.ImagePath))
            {
                figure.ImagePath = Path.GetFullPath(Path.Combine(baseDir, figure.ImagePath));
            }
        }
    }

    private static TemplateDiffResult CompareExampleTemplates()
    {
        return new TemplateDiffEngine().Compare(TemplatePath("example-university-engineering"), TemplatePath("example-university-engineering-variant"));
    }

    private static FormatRuleCoverageMatrix BuildCoverage()
    {
        return new FormatRuleCoverageReporter().Build(TemplatePath("example-university-engineering"));
    }

    private static string BodyText(WordprocessingDocument document)
    {
        return string.Concat(document.MainDocumentPart!.Document.Descendants<W.Text>().Select(text => text.Text));
    }

    private static string BodyText(W.Table table)
    {
        return string.Concat(table.Descendants<W.Text>().Select(text => text.Text));
    }

    private static string FullDocumentPath()
    {
        return Path.Combine(RepoRoot(), "examples", "full-thesis", "document.json");
    }

    private static string TemplatePath(string name)
    {
        return Path.Combine(RepoRoot(), "examples", "templates", name);
    }

    private static string TemplateFormatPath(string name)
    {
        return Path.Combine(TemplatePath(name), "format-spec.json");
    }

    private static string TempDocxPath()
    {
        return Path.Combine(NewTempDirectory(), $"{Guid.NewGuid():N}.docx");
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string RepoRoot()
    {
        return TestRenderHelper.LocateRepoRootForTests();
    }

    private sealed record RenderedTemplateDocx(string DocxPath, TemplateResolutionResult Resolution);
}
