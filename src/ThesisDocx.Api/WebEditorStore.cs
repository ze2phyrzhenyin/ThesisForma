using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Services;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;

namespace ThesisDocx.Api;

public sealed class WebEditorStore
{
    private static readonly Regex SafeIdPattern = new("^[a-zA-Z0-9][a-zA-Z0-9_-]{0,80}$", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/gif"] = ".gif",
        ["image/bmp"] = ".bmp"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly TemplateRegistry _templateRegistry = new();
    private readonly TemplateResolveService _templateResolveService = new();
    private readonly ThesisValidateService _validateService = new();
    private readonly ThesisRenderService _renderService = new();

    public WebEditorStore(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        ProjectRoot = LocateProjectRoot(environment.ContentRootPath);
        RuntimeRoot = Path.GetFullPath(configuration["ThesisForma:RuntimeRoot"] ?? Path.Combine(ProjectRoot, "runtime"));
        DocumentsDirectory = Path.Combine(RuntimeRoot, "documents");
        AssetsDirectory = Path.Combine(RuntimeRoot, "assets");
        RunsDirectory = Path.Combine(RuntimeRoot, "runs");

        Directory.CreateDirectory(DocumentsDirectory);
        Directory.CreateDirectory(AssetsDirectory);
        Directory.CreateDirectory(RunsDirectory);
    }

    public string ProjectRoot { get; }

    public string RuntimeRoot { get; }

    public string DocumentsDirectory { get; }

    public string AssetsDirectory { get; }

    public string RunsDirectory { get; }

    public IReadOnlyList<TemplatePackage> ListTemplates()
    {
        return _templateRegistry.ListTemplates(Path.Combine(ProjectRoot, "examples", "templates"));
    }

    public TemplatePackage? FindTemplate(string? templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            templateId = "example-university-engineering";
        }

        return ListTemplates().FirstOrDefault(template => template.Id == templateId);
    }

    public string TemplatePath(TemplatePackage template)
    {
        return template.TemplateDirectory ?? Path.Combine(ProjectRoot, "examples", "templates", template.Id);
    }

    public string PublicTemplatePath(TemplatePackage template)
    {
        var relative = Path.GetRelativePath(ProjectRoot, TemplatePath(template));
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    public DocumentEnvelope CreateDocument(ThesisDocument document, string? templateId)
    {
        var id = NewId("doc");
        return WriteDocument(id, document, templateId);
    }

    public DocumentEnvelope? SaveDocument(string id, ThesisDocument document, string? templateId)
    {
        if (!IsSafeId(id) || !File.Exists(DocumentPath(id)))
        {
            return null;
        }

        return WriteDocument(id, document, templateId);
    }

    public DocumentEnvelope? GetDocument(string id)
    {
        if (!IsSafeId(id))
        {
            return null;
        }

        var documentPath = DocumentPath(id);
        if (!File.Exists(documentPath))
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options) ?? new ThesisDocument();
        var templateId = File.Exists(DocumentMetaPath(id))
            ? JsonSerializer.Deserialize<DocumentMeta>(File.ReadAllText(DocumentMetaPath(id)), ThesisJson.Options)?.TemplateId
            : null;

        return new DocumentEnvelope(id, templateId, document, File.GetLastWriteTimeUtc(documentPath).ToString("O"));
    }

    public TemplateResolutionResult ResolveTemplate(string? templateId, ThesisDocument document)
    {
        var serviceResult = ResolveTemplateResult(templateId, document);
        if (serviceResult.Resolution is not null)
        {
            return serviceResult.Resolution;
        }

        var result = new TemplateResolutionResult();
        result.Errors.AddRange(serviceResult.Diagnostics.Select(diagnostic => new TemplateIssue
        {
            Code = diagnostic.Code,
            Path = diagnostic.Path,
            Message = diagnostic.Message
        }));
        return result;
    }

    public TemplateResolveServiceResult ResolveTemplateResult(string? templateId, ThesisDocument document)
    {
        var template = FindTemplate(templateId);
        if (template is null)
        {
            return TemplateResolveServiceResult.Failure("template.notFound", $"Template '{templateId}' was not found.");
        }

        return _templateResolveService.Resolve(new TemplateResolveRequest
        {
            TemplatePath = TemplatePath(template),
            Document = document
        });
    }

    public ThesisInputValidationResult ValidateDocument(ThesisDocument document, ThesisFormatSpec format, string documentBaseDirectory)
    {
        var serviceResult = ValidateDocumentResult(document, format, documentBaseDirectory);
        var result = new ThesisInputValidationResult { Source = "ThesisValidateService", VersionReport = serviceResult.VersionReport };
        foreach (var diagnostic in serviceResult.Diagnostics)
        {
            var issue = new ThesisInputValidationError
            {
                Code = diagnostic.Code,
                Path = diagnostic.Path,
                Message = diagnostic.Message
            };
            if (UnifiedDiagnosticMapper.IsWarning(diagnostic.Severity))
            {
                result.Warnings.Add(issue);
            }
            else
            {
                result.Errors.Add(issue);
            }
        }

        return result;
    }

    public ValidateInputResult ValidateDocumentResult(ThesisDocument document, ThesisFormatSpec format, string documentBaseDirectory)
    {
        return _validateService.ValidateInput(new ValidateInputRequest
        {
            Document = document,
            Format = format,
            BaseDirectory = documentBaseDirectory
        });
    }

    public RenderRunResponse RenderDocument(
        string documentId,
        ThesisDocument document,
        string? templateId,
        ThesisFormatSpec format,
        TemplateResolutionResult resolution)
    {
        ResolveRelativeImagePaths(document, DocumentsDirectory);

        var runId = NewId("run");
        var runDirectory = Path.Combine(RunsDirectory, runId);
        Directory.CreateDirectory(runDirectory);
        var docxPath = Path.Combine(runDirectory, "document.docx");

        var context = new DocxRenderContext
        {
            TemplateId = resolution.Template?.Id,
            TemplateVersion = resolution.Template?.Version,
            ResolvedFormatSpecVersion = format.SchemaVersion,
            RendererVersion = "1.0.0",
            PageTemplates = resolution.PageTemplates,
            Variables = resolution.Variables.ToDictionary(variable => variable.Name, variable => variable.Value ?? string.Empty, StringComparer.Ordinal),
            Assets = resolution.Assets.ToDictionary(asset => asset.Id, asset => asset, StringComparer.Ordinal)
        };

        var render = _renderService.Render(new RenderRequest
        {
            Document = document,
            Format = format,
            OutputPath = docxPath,
            BaseDirectory = DocumentsDirectory,
            ValidateInput = false,
            RenderContext = context
        });
        var formatResult = render.Success
            ? _validateService.ValidateDocx(new ValidateDocxRequest { DocxPath = docxPath, Format = format })
            : ValidateDocxResult.Failure("render.failed", "DOCX render failed.");
        object inspect = render.Success && File.Exists(docxPath)
            ? new DocxInspector().Inspect(docxPath)
            : new { status = "notRendered" };

        var issues = new List<ApiIssue>();
        issues.AddRange(render.Diagnostics.Select(ApiIssue.FromDiagnostic));
        issues.AddRange(formatResult.Diagnostics.Select(ApiIssue.FromDiagnostic));

        var run = new RenderRunResponse(
            runId,
            documentId,
            resolution.Template?.Id ?? templateId ?? string.Empty,
            render.Success && formatResult.IsValid ? "valid" : "invalid",
            render.Success,
            formatResult.IsValid,
            render.ErrorCount,
            formatResult.ErrorCount,
            "document.docx",
            $"/api/runs/{runId}/download",
            inspect,
            issues,
            DateTimeOffset.UtcNow.ToString("O"));

        File.WriteAllText(Path.Combine(runDirectory, "run.json"), JsonSerializer.Serialize(run, ThesisJson.Options));
        File.WriteAllText(Path.Combine(runDirectory, "inspect.json"), JsonSerializer.Serialize(inspect, ThesisJson.Options));
        File.WriteAllText(Path.Combine(runDirectory, "validation.json"), JsonSerializer.Serialize(formatResult, ThesisJson.Options));
        return run;
    }

    public RenderRunResponse? GetRun(string runId)
    {
        if (!IsSafeId(runId))
        {
            return null;
        }

        var path = Path.Combine(RunsDirectory, runId, "run.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<RenderRunResponse>(File.ReadAllText(path), ThesisJson.Options)
            : null;
    }

    public string? GetRunDocxPath(string runId)
    {
        if (!IsSafeId(runId))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(RunsDirectory, runId, "document.docx"));
        return IsInside(path, RunsDirectory) ? path : null;
    }

    public async Task<AssetSaveResult> SaveImageAsset(IFormFile file)
    {
        if (file.Length == 0)
        {
            return new AssetSaveResult(null, ApiError.BadRequest("asset.empty", "Uploaded image is empty."));
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return new AssetSaveResult(null, ApiError.BadRequest("asset.tooLarge", "Image assets are limited to 5 MB in the MVP."));
        }

        if (!AllowedImageContentTypes.TryGetValue(file.ContentType, out var extension))
        {
            return new AssetSaveResult(null, ApiError.BadRequest("asset.unsupportedType", $"Unsupported image content type '{file.ContentType}'."));
        }

        var id = NewId("asset");
        var fileName = id + extension;
        var path = Path.Combine(AssetsDirectory, fileName);
        await using (var stream = File.Create(path))
        {
            await file.CopyToAsync(stream);
        }

        var meta = new AssetMeta(id, fileName, file.ContentType, file.Length);
        File.WriteAllText(Path.Combine(AssetsDirectory, id + ".json"), JsonSerializer.Serialize(meta, ThesisJson.Options));

        return new AssetSaveResult(new AssetUploadResponse(
            id,
            fileName,
            file.ContentType,
            file.Length,
            "../assets/" + fileName,
            $"/api/assets/{id}"), null);
    }

    public AssetLookup? FindAsset(string assetId)
    {
        if (!IsSafeId(assetId))
        {
            return null;
        }

        var metaPath = Path.Combine(AssetsDirectory, assetId + ".json");
        if (!File.Exists(metaPath))
        {
            return null;
        }

        var meta = JsonSerializer.Deserialize<AssetMeta>(File.ReadAllText(metaPath), ThesisJson.Options);
        if (meta is null || !IsSafeRelativeFileName(meta.FileName))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(AssetsDirectory, meta.FileName));
        return IsInside(path, AssetsDirectory) && File.Exists(path)
            ? new AssetLookup(assetId, path, meta.ContentType)
            : null;
    }

    public static bool IsSafeId(string value) => SafeIdPattern.IsMatch(value);

    private DocumentEnvelope WriteDocument(string id, ThesisDocument document, string? templateId)
    {
        Directory.CreateDirectory(DocumentsDirectory);
        File.WriteAllText(DocumentPath(id), JsonSerializer.Serialize(document, ThesisJson.Options));
        File.WriteAllText(DocumentMetaPath(id), JsonSerializer.Serialize(new DocumentMeta(templateId), ThesisJson.Options));
        return new DocumentEnvelope(id, templateId, document, DateTimeOffset.UtcNow.ToString("O"));
    }

    private string DocumentPath(string id) => Path.Combine(DocumentsDirectory, id + ".json");

    private string DocumentMetaPath(string id) => Path.Combine(DocumentsDirectory, id + ".meta.json");

    private static string NewId(string prefix)
    {
        return prefix + "-" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(9)).ToLowerInvariant();
    }

    private static bool IsSafeRelativeFileName(string value)
    {
        return !Path.IsPathRooted(value) && value == Path.GetFileName(value);
    }

    private static bool IsInside(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullDirectory, StringComparison.Ordinal);
    }

    private static string LocateProjectRoot(string start)
    {
        var current = new DirectoryInfo(Path.GetFullPath(start));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ThesisDocx.slnx"))
                && Directory.Exists(Path.Combine(current.FullName, "examples")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(start);
    }

    private static void ResolveRelativeImagePaths(ThesisDocument document, string baseDirectory)
    {
        foreach (var block in document.Sections.SelectMany(section => section.Blocks))
        {
            if (block is FigureBlock figure && !string.IsNullOrWhiteSpace(figure.ImagePath) && !Path.IsPathRooted(figure.ImagePath))
            {
                figure.ImagePath = Path.GetFullPath(Path.Combine(baseDirectory, figure.ImagePath));
            }
        }
    }

    private sealed record DocumentMeta(string? TemplateId);

    private sealed record AssetMeta(string AssetId, string FileName, string ContentType, long Size);
}

public static class WebEditorDocumentFactory
{
    public static ThesisDocument Create(CreateDocumentRequest request)
    {
        return new ThesisDocument
        {
            SchemaVersion = ThesisSchemaVersions.Version110,
            Metadata = new ThesisMetadata
            {
                Title = request.Title ?? "未命名论文",
                Author = request.Author ?? string.Empty,
                College = request.College ?? string.Empty,
                Major = request.Major ?? string.Empty,
                StudentId = request.StudentId ?? string.Empty,
                Advisor = request.Advisor ?? string.Empty,
                Date = request.Date ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
                Language = "zh-CN"
            },
            Sections =
            [
                new ThesisSection { Id = "cover", Kind = ThesisSectionKind.Cover, Title = "封面" },
                new ThesisSection { Id = "declaration", Kind = ThesisSectionKind.OriginalityStatement, Title = "声明" },
                new ThesisSection { Id = "abstract-cn", Kind = ThesisSectionKind.Abstract, Title = "摘要" },
                new ThesisSection { Id = "toc", Kind = ThesisSectionKind.Toc, Title = "目录" },
                new ThesisSection
                {
                    Id = "body",
                    Kind = ThesisSectionKind.Body,
                    Title = "正文",
                    Blocks =
                    [
                        new HeadingBlock
                        {
                            Id = "heading-intro",
                            Level = 1,
                            BookmarkName = "heading-intro",
                            Inlines = [new TextInline { Text = "绪论" }]
                        },
                        new ParagraphBlock
                        {
                            Id = "paragraph-intro",
                            Inlines = [new TextInline { Text = "在这里输入正文内容。" }]
                        }
                    ]
                },
                new ThesisSection { Id = "refs", Kind = ThesisSectionKind.Bibliography, Title = "参考文献", Blocks = [new BibliographyBlock { Id = "bibliography" }] }
            ]
        };
    }
}
