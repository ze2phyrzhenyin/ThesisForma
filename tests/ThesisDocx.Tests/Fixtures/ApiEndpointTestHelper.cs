using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Api;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Tests.Fixtures;

internal static class ApiEndpointTestHelper
{
    public static async Task<DocumentEnvelope> ImportDocument(HttpClient client, ThesisDocument document, string templateId)
    {
        var response = await client.PostAsync("/api/documents/import-json", JsonContent(new ImportDocumentRequest(document, templateId)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJson<DocumentEnvelope>(response);
    }

    public static async Task<DocumentEnvelope> ImportDocument(HttpClient client, ThesisDocument document, string templateId, DocumentOverrides overrides)
    {
        var response = await client.PostAsync("/api/documents/import-json", JsonContent(new ImportDocumentRequest(document, templateId, overrides)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJson<DocumentEnvelope>(response);
    }

    public static ThesisDocument LoadSimpleDocument()
    {
        var documentPath = Path.Combine(TestRenderHelper.LocateRepoRootForTests(), "examples", "simple-thesis", "document.json");
        return JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options)!;
    }

    public static StringContent JsonContent<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value, ThesisJson.Options), Encoding.UTF8, "application/json");
    }

    public static async Task<T> ReadJson<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize response as {typeof(T).Name}: {json}");
    }

    public static async Task AssertErrorResponseContract(HttpResponseMessage response)
    {
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected a client error response, got {(int)response.StatusCode}.");
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        AssertRequiredString(json, "code");
        AssertRequiredString(json, "message");
        AssertRequiredString(json, "path");
        var issues = Assert.IsType<JsonArray>(json["issues"]);
        foreach (var issue in issues)
        {
            AssertRequiredString(issue!, "code");
            AssertRequiredString(issue!, "message");
            AssertRequiredString(issue!, "path");
            var severity = AssertRequiredString(issue!, "severity");
            Assert.Contains(severity, new[] { "error", "warning", "info" });
            AssertRequiredString(issue!, "suggestedAction");
        }

        AssertNoLocalAbsolutePaths(json);
    }

    public static void AssertNoLocalAbsolutePaths(JsonNode? node)
    {
        switch (node)
        {
            case JsonValue value:
                var text = value.TryGetValue<string>(out var stringValue) ? stringValue : null;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Assert.False(LooksLikeLocalAbsolutePath(text), $"Local absolute path leaked in API JSON: {text}");
                }

                break;
            case JsonObject obj:
                foreach (var child in obj)
                {
                    AssertNoLocalAbsolutePaths(child.Value);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    AssertNoLocalAbsolutePaths(child);
                }

                break;
        }
    }

    private static string AssertRequiredString(JsonNode node, string propertyName)
    {
        var value = node[propertyName]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(value), $"Missing or empty '{propertyName}' in {node.ToJsonString()}.");
        return value!;
    }

    private static bool LooksLikeLocalAbsolutePath(string value)
    {
        var normalized = value.Replace('\\', '/');
        return normalized.StartsWith("/Users/", StringComparison.Ordinal)
            || normalized.StartsWith("/tmp/", StringComparison.Ordinal)
            || normalized.StartsWith("/var/", StringComparison.Ordinal)
            || normalized.Contains("/Downloads/xmllunwen/", StringComparison.Ordinal)
            || (normalized.Length >= 3 && char.IsLetter(normalized[0]) && normalized[1] == ':' && normalized[2] == '/');
    }
}
