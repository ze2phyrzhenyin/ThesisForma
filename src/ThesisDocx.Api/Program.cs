using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json;
using ThesisDocx.Api;
using ThesisDocx.Core.Utilities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin());
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    foreach (var converter in ThesisJson.Options.Converters)
    {
        options.SerializerOptions.Converters.Add(converter);
    }
});
builder.Services.AddSingleton<WebEditorStore>();

var app = builder.Build();

app.UseCors();

app.MapGet("/", () => Results.Json(new
{
    name = "ThesisForma.Api",
    stage = "web-editor-mvp",
    message = "Structured thesis editor API. Formatting is controlled by TemplatePackage and ThesisFormatSpec."
}));

app.MapGet("/api/templates", (WebEditorStore store) =>
{
    var templates = store.ListTemplates()
        .Select(template => TemplateSummary.FromTemplate(template, store.PublicTemplatePath(template)))
        .ToList();
    return Results.Json(new { templates }, ThesisJson.Options);
});

app.MapGet("/api/templates/{id}", Results<Ok<TemplateDetail>, NotFound<ApiError>> (string id, WebEditorStore store) =>
{
    var template = store.FindTemplate(id);
    if (template is null)
    {
        return TypedResults.NotFound(ApiError.NotFound("template.notFound", $"Template '{id}' was not found."));
    }

    return TypedResults.Ok(TemplateDetail.FromTemplate(template, store.PublicTemplatePath(template)));
});

app.MapPost("/api/documents", Results<Created<DocumentEnvelope>, BadRequest<ApiError>> (CreateDocumentRequest request, WebEditorStore store) =>
{
    if (!string.IsNullOrWhiteSpace(request.TemplateId) && store.FindTemplate(request.TemplateId) is null)
    {
        return TypedResults.BadRequest(ApiError.BadRequest("template.notFound", $"Template '{request.TemplateId}' was not found."));
    }

    var document = WebEditorDocumentFactory.Create(request);
    var envelope = store.CreateDocument(document, request.TemplateId);
    return TypedResults.Created($"/api/documents/{envelope.Id}", envelope);
});

app.MapGet("/api/documents/{id}", Results<Ok<DocumentEnvelope>, NotFound<ApiError>> (string id, WebEditorStore store) =>
{
    var envelope = store.GetDocument(id);
    return envelope is null
        ? TypedResults.NotFound(ApiError.NotFound("document.notFound", $"Document '{id}' was not found."))
        : TypedResults.Ok(envelope);
});

app.MapPut("/api/documents/{id}", Results<Ok<DocumentEnvelope>, BadRequest<ApiError>, NotFound<ApiError>> (string id, SaveDocumentRequest request, WebEditorStore store) =>
{
    if (!WebEditorStore.IsSafeId(id))
    {
        return TypedResults.BadRequest(ApiError.BadRequest("document.invalidId", "Document id contains unsafe characters."));
    }

    if (request.Document is null)
    {
        return TypedResults.BadRequest(ApiError.BadRequest("document.missing", "Request must include a ThesisDocument."));
    }

    var saved = store.SaveDocument(id, request.Document, request.TemplateId);
    return saved is null
        ? TypedResults.NotFound(ApiError.NotFound("document.notFound", $"Document '{id}' was not found."))
        : TypedResults.Ok(saved);
});

app.MapPost("/api/documents/{id}/validate", Results<Ok<DocumentValidationResponse>, NotFound<ApiError>, BadRequest<ApiError>> (string id, ValidateDocumentRequest request, WebEditorStore store) =>
{
    var envelope = store.GetDocument(id);
    if (envelope is null)
    {
        return TypedResults.NotFound(ApiError.NotFound("document.notFound", $"Document '{id}' was not found."));
    }

    var templateId = request.TemplateId ?? envelope.TemplateId;
    var resolved = store.ResolveTemplateResult(templateId, envelope.Document);
    if (!resolved.Success || resolved.Resolution is null)
    {
        return TypedResults.BadRequest(ApiError.FromDiagnostics("template.validationFailed", "Template resolution failed.", resolved.Diagnostics));
    }

    var result = store.ValidateDocumentResult(envelope.Document, resolved.Resolution.FormatSpec!, store.DocumentsDirectory);
    return TypedResults.Ok(DocumentValidationResponse.FromServiceResult(result));
});

app.MapPost("/api/documents/{id}/render", Results<Ok<RenderRunResponse>, NotFound<ApiError>, BadRequest<ApiError>> (string id, RenderDocumentRequest request, WebEditorStore store) =>
{
    var envelope = store.GetDocument(id);
    if (envelope is null)
    {
        return TypedResults.NotFound(ApiError.NotFound("document.notFound", $"Document '{id}' was not found."));
    }

    var templateId = request.TemplateId ?? envelope.TemplateId;
    var resolved = store.ResolveTemplateResult(templateId, envelope.Document);
    if (!resolved.Success || resolved.Resolution is null)
    {
        return TypedResults.BadRequest(ApiError.FromDiagnostics("template.validationFailed", "Template resolution failed.", resolved.Diagnostics));
    }

    var format = resolved.Resolution.FormatSpec!;
    var inputValidation = store.ValidateDocumentResult(envelope.Document, format, store.DocumentsDirectory);
    if (!inputValidation.IsValid)
    {
        return TypedResults.BadRequest(ApiError.FromDiagnostics("document.validationFailed", "ThesisDocument failed input validation.", inputValidation.Diagnostics));
    }

    var run = store.RenderDocument(id, envelope.Document, templateId, format, resolved.Resolution);
    return TypedResults.Ok(run);
});

app.MapGet("/api/runs/{runId}", Results<Ok<RenderRunResponse>, NotFound<ApiError>> (string runId, WebEditorStore store) =>
{
    var run = store.GetRun(runId);
    return run is null
        ? TypedResults.NotFound(ApiError.NotFound("run.notFound", $"Render run '{runId}' was not found."))
        : TypedResults.Ok(run);
});

app.MapGet("/api/runs/{runId}/download", Results<FileContentHttpResult, NotFound<ApiError>> (string runId, WebEditorStore store) =>
{
    var docxPath = store.GetRunDocxPath(runId);
    if (docxPath is null || !File.Exists(docxPath))
    {
        return TypedResults.NotFound(ApiError.NotFound("run.docxMissing", $"Rendered DOCX for run '{runId}' was not found."));
    }

    return TypedResults.File(
        File.ReadAllBytes(docxPath),
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        $"{runId}.docx");
});

app.MapPost("/api/assets/images", async Task<Results<Ok<AssetUploadResponse>, BadRequest<ApiError>>> (IFormFile file, WebEditorStore store) =>
{
    var result = await store.SaveImageAsset(file);
    return result.Error is not null
        ? TypedResults.BadRequest(result.Error)
        : TypedResults.Ok(result.Asset!);
}).DisableAntiforgery();

app.MapGet("/api/assets/{assetId}", Results<FileContentHttpResult, NotFound<ApiError>> (string assetId, WebEditorStore store) =>
{
    var asset = store.FindAsset(assetId);
    if (asset is null)
    {
        return TypedResults.NotFound(ApiError.NotFound("asset.notFound", $"Asset '{assetId}' was not found."));
    }

    return TypedResults.File(File.ReadAllBytes(asset.Path), asset.ContentType);
});

app.MapPost("/api/documents/{id}/export-json", Results<FileContentHttpResult, NotFound<ApiError>> (string id, WebEditorStore store) =>
{
    var envelope = store.GetDocument(id);
    if (envelope is null)
    {
        return TypedResults.NotFound(ApiError.NotFound("document.notFound", $"Document '{id}' was not found."));
    }

    return TypedResults.File(
        JsonSerializer.SerializeToUtf8Bytes(envelope.Document, ThesisJson.Options),
        "application/json",
        $"{id}.thesis-document.json");
});

app.MapPost("/api/documents/import-json", Results<Created<DocumentEnvelope>, BadRequest<ApiError>> (ImportDocumentRequest request, WebEditorStore store) =>
{
    if (request.Document is null)
    {
        return TypedResults.BadRequest(ApiError.BadRequest("document.missing", "Request must include a ThesisDocument."));
    }

    var envelope = store.CreateDocument(request.Document, request.TemplateId);
    return TypedResults.Created($"/api/documents/{envelope.Id}", envelope);
});

app.Run();

public partial class Program;
