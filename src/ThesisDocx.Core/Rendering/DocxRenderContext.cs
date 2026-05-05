using ThesisDocx.Core.Models.Templates;

namespace ThesisDocx.Core.Rendering;

public sealed class DocxRenderContext
{
    public string? TemplateId { get; set; }

    public string? TemplateVersion { get; set; }

    public string? TemplateSchool { get; set; }

    public string? TemplateCollege { get; set; }

    public string RendererVersion { get; set; } = "1.0.0";

    public string? ResolvedFormatSpecVersion { get; set; }

    public IReadOnlyList<TemplatePageLayout> PageTemplates { get; set; } = [];

    public IReadOnlyDictionary<string, string> Variables { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ResolvedTemplateAsset> Assets { get; set; } = new Dictionary<string, ResolvedTemplateAsset>(StringComparer.Ordinal);

    public List<string> RenderedPageTemplates { get; } = [];

    public List<string> RenderedVariables { get; } = [];

    public List<string> RenderedAssets { get; } = [];
}
