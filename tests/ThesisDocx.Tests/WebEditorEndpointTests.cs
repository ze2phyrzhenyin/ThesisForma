using System.Net;
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
