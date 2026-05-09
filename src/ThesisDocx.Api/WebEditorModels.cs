using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Services;
using ThesisDocx.Core.Validation;

namespace ThesisDocx.Api;

public sealed record ApiError(string Code, string Message, string Path, IReadOnlyList<ApiIssue> Issues)
{
    public static ApiError BadRequest(string code, string message, string? path = null) => new(code, message, NormalizePath(path), []);

    public static ApiError NotFound(string code, string message) => new(code, message, "$", []);

    public static ApiError FromInputValidation(ThesisInputValidationResult result)
    {
        return new ApiError(
            "document.validationFailed",
            "ThesisDocument failed input validation.",
            "$",
            result.Errors.Select(error => ApiIssue.FromValidationError(error)).ToList());
    }

    public static ApiError FromTemplateIssues(IEnumerable<TemplateIssue> issues)
    {
        return new ApiError(
            "template.validationFailed",
            "Template resolution failed.",
            "$",
            issues.Select(issue => ApiIssue.FromTemplateIssue(issue)).ToList());
    }

    public static ApiError FromDiagnostics(string code, string message, IEnumerable<UnifiedDiagnostic> diagnostics)
    {
        return new ApiError(
            code,
            message,
            "$",
            diagnostics.Select(ApiIssue.FromDiagnostic).ToList());
    }

    private static string NormalizePath(string? path) => string.IsNullOrWhiteSpace(path) ? "$" : path;
}

public sealed record ApiIssue(string Code, string Message, string Path, string Severity, string SuggestedAction)
{
    public static ApiIssue FromDiagnostic(UnifiedDiagnostic diagnostic)
    {
        return new ApiIssue(
            diagnostic.Code,
            diagnostic.Message,
            NormalizePath(diagnostic.Path),
            diagnostic.Severity,
            NormalizeSuggestedAction(diagnostic.FixHint, diagnostic.Message));
    }

    public static ApiIssue FromValidationError(ThesisInputValidationError error)
    {
        return new ApiIssue(error.Code, error.Message, NormalizePath(error.Path), "error", NormalizeSuggestedAction(error.Message, error.Message));
    }

    public static ApiIssue FromTemplateIssue(TemplateIssue issue)
    {
        return new ApiIssue(issue.Code, issue.Message, NormalizePath(issue.Path), "error", NormalizeSuggestedAction(issue.Message, issue.Message));
    }

    private static string NormalizePath(string? path) => string.IsNullOrWhiteSpace(path) ? "$" : path;

    private static string NormalizeSuggestedAction(string? action, string message) => string.IsNullOrWhiteSpace(action) ? message : action;
}

public sealed record CreateDocumentRequest(
    string? TemplateId,
    string? Title,
    string? Author,
    string? College,
    string? Major,
    string? StudentId,
    string? Advisor,
    string? Date);

public sealed record SaveDocumentRequest(ThesisDocument? Document, string? TemplateId);

public sealed record ImportDocumentRequest(ThesisDocument? Document, string? TemplateId);

public sealed record ValidateDocumentRequest(string? TemplateId);

public sealed record RenderDocumentRequest(string? TemplateId);

public sealed record DocumentEnvelope(string Id, string? TemplateId, ThesisDocument Document, string UpdatedAt);

public sealed record DocumentValidationResponse(bool IsValid, IReadOnlyList<ApiIssue> Issues)
{
    public static DocumentValidationResponse FromResult(ThesisInputValidationResult result)
    {
        return new DocumentValidationResponse(
            result.IsValid,
            result.Errors.Select(ApiIssue.FromValidationError).ToList());
    }

    public static DocumentValidationResponse FromServiceResult(ValidateInputResult result)
    {
        return new DocumentValidationResponse(
            result.IsValid,
            result.Diagnostics.Select(ApiIssue.FromDiagnostic).ToList());
    }
}

public sealed record TemplateSummary(
    string Id,
    string Name,
    string School,
    string College,
    string Version,
    string Status,
    double Coverage,
    string Readiness,
    IReadOnlyList<string> Tags,
    string Path)
{
    public static TemplateSummary FromTemplate(TemplatePackage template, string path)
    {
        var status = template.Notes.Any(note => note.Contains("draft", StringComparison.OrdinalIgnoreCase)) ? "draft" : "ready";
        return new TemplateSummary(
            template.Id,
            template.Name,
            template.School,
            template.College,
            template.Version,
            status,
            template.Id.Contains("example-university-engineering", StringComparison.Ordinal) ? 0.875 : 0.75,
            status == "ready" ? "ready" : "review",
            template.Tags,
            path);
    }
}

public sealed record TemplateDetail(
    TemplateSummary Summary,
    IReadOnlyList<TemplateVariable> Variables,
    IReadOnlyList<TemplatePageLayout> PageTemplates,
    IReadOnlyList<string> KnownGaps,
    string? FormatSpecRef)
{
    public static TemplateDetail FromTemplate(TemplatePackage template, string path)
    {
        var knownGaps = template.Notes
            .Where(note => note.Contains("gap", StringComparison.OrdinalIgnoreCase)
                || note.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
                || note.Contains("draft", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new TemplateDetail(
            TemplateSummary.FromTemplate(template, path),
            template.Variables,
            template.PageTemplates,
            knownGaps,
            template.FormatSpecRef);
    }
}

public sealed record AssetUploadResponse(
    string AssetId,
    string FileName,
    string ContentType,
    long Size,
    string ImagePath,
    string PreviewUrl);

public sealed record AssetLookup(string AssetId, string Path, string ContentType);

public sealed record AssetSaveResult(AssetUploadResponse? Asset, ApiError? Error);

public sealed record RenderRunResponse(
    string RunId,
    string DocumentId,
    string TemplateId,
    string Status,
    bool OpenXmlValid,
    bool FormatValid,
    int OpenXmlErrorCount,
    int FormatErrorCount,
    string DocxPath,
    string DownloadUrl,
    object InspectSummary,
    IReadOnlyList<ApiIssue> Issues,
    string CreatedAt);
