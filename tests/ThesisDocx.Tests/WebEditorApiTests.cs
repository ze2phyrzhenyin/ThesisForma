using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using ThesisDocx.Api;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class WebEditorApiTests
{
    [Fact]
    public void Api_ShouldListTemplates()
    {
        var store = CreateStore();

        var templates = store.ListTemplates();

        Assert.True(templates.Count >= 3);
        Assert.Contains(templates, template => template.Id == "example-university-engineering");
    }

    [Fact]
    public void Api_ShouldCreateDocument()
    {
        var store = CreateStore();
        var document = WebEditorDocumentFactory.Create(new CreateDocumentRequest("example-university-engineering", "结构化论文", null, null, null, null, null, null));

        var envelope = store.CreateDocument(document, "example-university-engineering");

        Assert.StartsWith("doc-", envelope.Id);
        Assert.Equal("结构化论文", envelope.Document.Metadata.Title);
        Assert.NotNull(store.GetDocument(envelope.Id));
    }

    [Fact]
    public void Api_ShouldSaveDocument()
    {
        var store = CreateStore();
        var envelope = store.CreateDocument(WebEditorDocumentFactory.Create(new CreateDocumentRequest("example-university-engineering", "结构化论文", null, null, null, null, null, null)), "example-university-engineering");
        envelope.Document.Metadata.Author = "作者";

        var saved = store.SaveDocument(envelope.Id, envelope.Document, "example-university-engineering");

        Assert.NotNull(saved);
        Assert.Equal("作者", store.GetDocument(envelope.Id)!.Document.Metadata.Author);
    }

    [Fact]
    public void Api_ShouldRoundTripDocumentOverrides()
    {
        var store = CreateStore();
        var overrides = new DocumentOverrides
        {
            Toc = new TocOverrideSpec { Title = "本文目录", MinLevel = 1, MaxLevel = 2 },
            SectionFormats = new Dictionary<string, SectionFormatOverrideSpec>
            {
                ["body"] = new() { StartPageNumber = 3, RestartPageNumbering = true }
            }
        };
        var envelope = store.CreateDocument(
            WebEditorDocumentFactory.Create(new CreateDocumentRequest("example-university-engineering", "结构化论文", null, null, null, null, null, null)),
            "example-university-engineering",
            overrides);

        var loaded = store.GetDocument(envelope.Id);

        Assert.NotNull(loaded);
        Assert.Equal("本文目录", loaded.Overrides?.Toc?.Title);
        Assert.Equal(3, loaded.Overrides?.SectionFormats?["body"].StartPageNumber);
    }

    [Fact]
    public void Api_ShouldValidateDocument()
    {
        var store = CreateStore();
        var document = LoadSimpleDocument();
        var resolved = store.ResolveTemplate("example-university-engineering", document);

        var result = store.ValidateDocument(document, resolved.FormatSpec!, TestRenderHelper.LocateRepoRootForTests());

        Assert.True(resolved.IsValid);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(error => $"{error.Code}: {error.Message}")));
    }

    [Fact]
    public void Api_ShouldRejectInvalidDocument()
    {
        var store = CreateStore();
        var document = WebEditorDocumentFactory.Create(new CreateDocumentRequest("example-university-engineering", "结构化论文", null, null, null, null, null, null));
        var body = document.Sections.First(section => section.Kind == ThesisSectionKind.Body);
        body.Blocks.Add(new ParagraphBlock
        {
            Id = "dangling-citation",
            Inlines = [new CitationInline { TargetId = "missing-ref", DisplayText = "[99]" }]
        });
        var resolved = store.ResolveTemplate("example-university-engineering", document);

        var result = store.ValidateDocument(document, resolved.FormatSpec!, store.DocumentsDirectory);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, issue => issue.Code == "citation.targetMissing");
    }

    [Fact]
    public void Api_ShouldRenderDocx()
    {
        var store = CreateStore();
        var document = LoadSimpleDocument();
        var resolved = store.ResolveTemplate("example-university-engineering", document);

        var run = store.RenderDocument("doc-test", document, "example-university-engineering", resolved.FormatSpec!, resolved);

        Assert.Equal("valid", run.Status);
        Assert.True(run.OpenXmlValid);
        Assert.True(run.FormatValid);
        Assert.Equal("document.docx", run.DocxPath);
        Assert.False(Path.IsPathRooted(run.DocxPath));
        Assert.True(File.Exists(store.GetRunDocxPath(run.RunId)));
    }

    [Fact]
    public void Api_ShouldRenderDocxWithOverrides()
    {
        var store = CreateStore();
        var document = LoadSimpleDocument();
        var resolved = store.ResolveTemplate("example-university-engineering", document);
        var overrides = new DocumentOverrides
        {
            Toc = new TocOverrideSpec { Title = "本文目录", MinLevel = 1, MaxLevel = 2 },
            SectionInstances = new Dictionary<string, SectionInstanceOverrideSpec>
            {
                ["body"] = new()
                {
                    HeaderText = "正文专用页眉",
                    PageNumberStyle = PageNumberStyle.Decimal,
                    RestartPageNumbering = true,
                    StartPageNumber = 5
                }
            }
        };

        var run = store.RenderDocument("doc-test", document, "example-university-engineering", resolved.FormatSpec!, resolved, overrides);

        Assert.Equal("valid", run.Status);
        Assert.True(run.OpenXmlValid);
        Assert.True(run.FormatValid);
        Assert.True(File.Exists(store.GetRunDocxPath(run.RunId)));
    }

    [Fact]
    public void Api_ShouldValidateDocumentOverrides()
    {
        var store = CreateStore();

        var diagnostics = store.ValidateOverrides(new DocumentOverrides { Toc = new TocOverrideSpec { MinLevel = 4, MaxLevel = 2 } });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "overrides.toc.levelRange");
    }

    [Fact]
    public void Api_ShouldResolveTemplatesThroughFacadeDiagnostics()
    {
        var store = CreateStore();
        var document = LoadSimpleDocument();

        var result = store.ResolveTemplateResult("missing-template", document);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "template.notFound");
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("template", diagnostic.Category));
    }

    [Fact]
    public void Api_ShouldValidateThroughFacadeDiagnostics()
    {
        var store = CreateStore();
        var document = LoadSimpleDocument();
        document.SchemaVersion = "9.9.9";
        var resolved = store.ResolveTemplate("example-university-engineering", document);

        var result = store.ValidateDocumentResult(document, resolved.FormatSpec!, store.DocumentsDirectory);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "thesis.schemaVersion.unsupported");
    }

    [Fact]
    public void Api_ShouldReturnRenderReport()
    {
        var store = CreateStore();
        var document = LoadSimpleDocument();
        var resolved = store.ResolveTemplate("example-university-engineering", document);
        var run = store.RenderDocument("doc-test", document, "example-university-engineering", resolved.FormatSpec!, resolved);

        var loaded = store.GetRun(run.RunId);

        Assert.NotNull(loaded);
        Assert.Equal(run.RunId, loaded.RunId);
        Assert.NotNull(loaded.InspectSummary);
    }

    [Fact]
    public void Api_ShouldDownloadRenderedDocx()
    {
        var store = CreateStore();
        var document = LoadSimpleDocument();
        var resolved = store.ResolveTemplate("example-university-engineering", document);
        var run = store.RenderDocument("doc-test", document, "example-university-engineering", resolved.FormatSpec!, resolved);

        var docxPath = store.GetRunDocxPath(run.RunId);

        Assert.NotNull(docxPath);
        Assert.True(File.Exists(docxPath));
        Assert.True(File.ReadAllBytes(docxPath).Length > 1024);
    }

    [Fact]
    public async Task Api_ShouldUploadImageAsset()
    {
        var store = CreateStore();
        var formFile = CreateFormFile(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="), "pixel.png", "image/png");

        var result = await store.SaveImageAsset(formFile);

        Assert.Null(result.Error);
        Assert.NotNull(result.Asset);
        Assert.StartsWith("asset-", result.Asset.AssetId);
        Assert.EndsWith(".png", result.Asset.FileName);
        Assert.Contains("../assets/", result.Asset.ImagePath);
    }

    [Fact]
    public async Task Api_ShouldRejectUnsafeAssetType()
    {
        var store = CreateStore();
        var formFile = CreateFormFile([1, 2, 3], "note.txt", "text/plain");

        var result = await store.SaveImageAsset(formFile);

        Assert.NotNull(result.Error);
        Assert.Equal("asset.unsupportedType", result.Error.Code);
    }

    [Fact]
    public void Api_ShouldReturnStructuredErrors()
    {
        var error = ApiError.NotFound("template.notFound", "Template was not found.");

        Assert.Equal("template.notFound", error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    private static WebEditorStore CreateStore()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), "thesisforma-api-tests", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ThesisForma:RuntimeRoot"] = runtimeRoot })
            .Build();
        return new WebEditorStore(new TestEnvironment(TestRenderHelper.LocateRepoRootForTests()), configuration);
    }

    private static ThesisDocument LoadSimpleDocument()
    {
        var documentPath = Path.Combine(TestRenderHelper.LocateRepoRootForTests(), "examples", "simple-thesis", "document.json");
        return JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options)!;
    }

    private static FormFile CreateFormFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ThesisDocx.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
