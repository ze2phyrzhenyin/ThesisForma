using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using ThesisDocx.Api;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class WebEditorEndpointTests
{
    [Fact]
    public async Task Endpoint_ShouldValidateAndRenderImportedDocument()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var document = LoadSimpleDocument();

        var envelope = await ImportDocument(client, document, "example-university-engineering");
        var validate = await client.PostAsync($"/api/documents/{envelope.Id}/validate", JsonContent(new ValidateDocumentRequest(null)));
        var validation = await ReadJson<DocumentValidationResponse>(validate);

        Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));

        var render = await client.PostAsync($"/api/documents/{envelope.Id}/render", JsonContent(new RenderDocumentRequest(null)));
        var run = await ReadJson<RenderRunResponse>(render);

        Assert.Equal(HttpStatusCode.OK, render.StatusCode);
        Assert.Equal("valid", run.Status);
        Assert.Equal("document.docx", run.DocxPath);
        Assert.False(Path.IsPathRooted(run.DocxPath));
        Assert.StartsWith("/api/runs/", run.DownloadUrl, StringComparison.Ordinal);

        var download = await client.GetAsync(run.DownloadUrl);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            download.Content.Headers.ContentType?.MediaType);
        Assert.True((await download.Content.ReadAsByteArrayAsync()).Length > 1024);
    }

    [Fact]
    public async Task Endpoint_RenderInvalidDocument_ShouldReturnFacadeDiagnostics()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var document = LoadSimpleDocument();
        document.SchemaVersion = "9.9.9";
        var envelope = await ImportDocument(client, document, "example-university-engineering");

        var response = await client.PostAsync($"/api/documents/{envelope.Id}/render", JsonContent(new RenderDocumentRequest(null)));
        var error = await ReadJson<ApiError>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("document.validationFailed", error.Code);
        Assert.Contains(error.Issues ?? [], issue => issue.Code == "thesis.schemaVersion.unsupported" && issue.Severity == "error");
    }

    [Fact]
    public async Task Endpoint_ValidateMissingTemplate_ShouldReturnTemplateDiagnostics()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var envelope = await ImportDocument(client, LoadSimpleDocument(), "missing-template");

        var response = await client.PostAsync($"/api/documents/{envelope.Id}/validate", JsonContent(new ValidateDocumentRequest(null)));
        var error = await ReadJson<ApiError>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("template.validationFailed", error.Code);
        Assert.Contains(error.Issues ?? [], issue => issue.Code == "template.notFound" && issue.Severity == "error");
    }

    [Fact]
    public async Task Endpoint_ShouldUploadDownloadAndRejectImageAssets()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var imageForm = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="));
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        imageForm.Add(imageContent, "file", "pixel.png");

        var upload = await client.PostAsync("/api/assets/images", imageForm);
        var asset = await ReadJson<AssetUploadResponse>(upload);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.StartsWith("asset-", asset.AssetId, StringComparison.Ordinal);
        Assert.Equal("image/png", asset.ContentType);
        Assert.False(Path.IsPathRooted(asset.ImagePath));

        var download = await client.GetAsync(asset.PreviewUrl);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.True((await download.Content.ReadAsByteArrayAsync()).Length > 0);

        using var textForm = new MultipartFormDataContent();
        var textContent = new ByteArrayContent(Encoding.UTF8.GetBytes("not an image"));
        textContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        textForm.Add(textContent, "file", "note.txt");

        var rejected = await client.PostAsync("/api/assets/images", textForm);
        var error = await ReadJson<ApiError>(rejected);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("asset.unsupportedType", error.Code);
    }

    [Fact]
    public async Task Endpoint_ShouldExportAndImportJson()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var document = LoadSimpleDocument();
        document.Metadata.Title = "Endpoint export contract";
        var envelope = await ImportDocument(client, document, "example-university-engineering");

        var export = await client.PostAsync($"/api/documents/{envelope.Id}/export-json", content: null);
        var exportedDocument = JsonSerializer.Deserialize<ThesisDocument>(
            await export.Content.ReadAsStringAsync(),
            ThesisJson.Options)!;

        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/json", export.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Endpoint export contract", exportedDocument.Metadata.Title);

        var importedAgain = await ImportDocument(client, exportedDocument, "example-university-engineering");

        Assert.NotEqual(envelope.Id, importedAgain.Id);
        Assert.Equal(exportedDocument.Metadata.Title, importedAgain.Document.Metadata.Title);

        var missing = await client.PostAsync("/api/documents/missing-doc/export-json", content: null);
        var error = await ReadJson<ApiError>(missing);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("document.notFound", error.Code);
    }

    [Fact]
    public async Task Endpoint_ShouldReturnRunLookupAndDownloadFailures()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var envelope = await ImportDocument(client, LoadSimpleDocument(), "example-university-engineering");
        var render = await client.PostAsync($"/api/documents/{envelope.Id}/render", JsonContent(new RenderDocumentRequest(null)));
        var run = await ReadJson<RenderRunResponse>(render);

        var lookup = await client.GetAsync($"/api/runs/{run.RunId}");
        var loaded = await ReadJson<RenderRunResponse>(lookup);

        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        Assert.Equal(run.RunId, loaded.RunId);
        Assert.Equal("document.docx", loaded.DocxPath);

        var missingRun = await client.GetAsync("/api/runs/missing-run");
        var missingRunError = await ReadJson<ApiError>(missingRun);
        var missingDownload = await client.GetAsync("/api/runs/missing-run/download");
        var missingDownloadError = await ReadJson<ApiError>(missingDownload);

        Assert.Equal(HttpStatusCode.NotFound, missingRun.StatusCode);
        Assert.Equal("run.notFound", missingRunError.Code);
        Assert.Equal(HttpStatusCode.NotFound, missingDownload.StatusCode);
        Assert.Equal("run.docxMissing", missingDownloadError.Code);
    }

    [Fact]
    public async Task Endpoint_ShouldReturnStructuredDocumentErrors()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var missingTemplate = await client.PostAsync("/api/documents", JsonContent(new CreateDocumentRequest("missing-template", "Draft", null, null, null, null, null, null)));
        var missingTemplateError = await ReadJson<ApiError>(missingTemplate);
        var missingDocument = await client.GetAsync("/api/documents/missing-doc");
        var missingDocumentError = await ReadJson<ApiError>(missingDocument);
        var unsafeSave = await client.PutAsync("/api/documents/bad$id", JsonContent(new SaveDocumentRequest(LoadSimpleDocument(), "example-university-engineering")));
        var unsafeSaveError = await ReadJson<ApiError>(unsafeSave);
        var importMissingDocument = await client.PostAsync("/api/documents/import-json", JsonContent(new ImportDocumentRequest(null, "example-university-engineering")));
        var importMissingDocumentError = await ReadJson<ApiError>(importMissingDocument);

        Assert.Equal(HttpStatusCode.BadRequest, missingTemplate.StatusCode);
        Assert.Equal("template.notFound", missingTemplateError.Code);
        Assert.Equal(HttpStatusCode.NotFound, missingDocument.StatusCode);
        Assert.Equal("document.notFound", missingDocumentError.Code);
        Assert.Equal(HttpStatusCode.BadRequest, unsafeSave.StatusCode);
        Assert.Equal("document.invalidId", unsafeSaveError.Code);
        Assert.Equal(HttpStatusCode.BadRequest, importMissingDocument.StatusCode);
        Assert.Equal("document.missing", importMissingDocumentError.Code);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var repoRoot = TestRenderHelper.LocateRepoRootForTests();
        var runtimeRoot = Path.Combine(Path.GetTempPath(), "thesisforma-endpoint-tests", Guid.NewGuid().ToString("N"));
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(repoRoot);
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ThesisForma:RuntimeRoot"] = runtimeRoot
                    });
                });
            });
    }

    private static async Task<DocumentEnvelope> ImportDocument(HttpClient client, ThesisDocument document, string templateId)
    {
        var response = await client.PostAsync("/api/documents/import-json", JsonContent(new ImportDocumentRequest(document, templateId)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJson<DocumentEnvelope>(response);
    }

    private static ThesisDocument LoadSimpleDocument()
    {
        var documentPath = Path.Combine(TestRenderHelper.LocateRepoRootForTests(), "examples", "simple-thesis", "document.json");
        return JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options)!;
    }

    private static StringContent JsonContent<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value, ThesisJson.Options), Encoding.UTF8, "application/json");
    }

    private static async Task<T> ReadJson<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize response as {typeof(T).Name}: {json}");
    }
}
