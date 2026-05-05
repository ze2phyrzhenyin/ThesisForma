using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ThesisDocx.Core.Models.Templates;

public sealed class TemplatePackage
{
    public string TemplateSchemaVersion { get; set; } = TemplateSchemaVersions.Current;

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0.0";

    public string Locale { get; set; } = "zh-CN";

    public string Description { get; set; } = string.Empty;

    public string School { get; set; } = string.Empty;

    public string College { get; set; } = string.Empty;

    public string DegreeType { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public TemplateInheritance? Extends { get; set; }

    public ThesisFormatSpec? FormatSpec { get; set; }

    public string? FormatSpecRef { get; set; }

    public List<TemplateVariable> Variables { get; set; } = [];

    public List<TemplateAsset> Assets { get; set; } = [];

    public List<TemplatePageLayout> PageTemplates { get; set; } = [];

    public List<TemplateComplianceRule> ComplianceRules { get; set; } = [];

    public List<string> Notes { get; set; } = [];

    [JsonIgnore]
    public string? TemplateDirectory { get; set; }
}

public static class TemplateSchemaVersions
{
    public const string Version100 = "1.0.0";
    public const string Current = Version100;

    public static bool IsSupported(string? version)
    {
        return version == Version100;
    }
}

public sealed class TemplateMetadata
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0.0";

    public string School { get; set; } = string.Empty;

    public string College { get; set; } = string.Empty;
}

public sealed class TemplateInheritance
{
    public string TemplateId { get; set; } = string.Empty;

    public string? VersionRange { get; set; }
}

public sealed class TemplateVariable
{
    public string Name { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public TemplateVariableType Type { get; set; } = TemplateVariableType.String;

    public bool Required { get; set; }

    public JsonNode? DefaultValue { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? SourcePath { get; set; }

    public string? Pattern { get; set; }

    public List<string> EnumValues { get; set; } = [];

    public string? Format { get; set; }

    public int DisplayOrder { get; set; }
}

public enum TemplateVariableType
{
    String,
    MultilineText,
    Date,
    Number,
    Boolean,
    Enum,
    Image,
    RichText
}

public sealed class TemplateAsset
{
    public string Id { get; set; } = string.Empty;

    public TemplateAssetType Type { get; set; } = TemplateAssetType.Image;

    public string Path { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool Required { get; set; }

    [JsonIgnore]
    public string? BaseDirectory { get; set; }
}

public enum TemplateAssetType
{
    Image,
    Font,
    StaticDocxFragment,
    Text
}

public sealed class TemplateComplianceRule
{
    public string Id { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Severity { get; set; } = "info";
}

public sealed class TemplateResolutionResult
{
    public bool IsValid => Errors.Count == 0;

    public TemplatePackage? Template { get; set; }

    public ThesisFormatSpec? FormatSpec { get; set; }

    public List<TemplateVariableResolution> Variables { get; set; } = [];

    public List<ResolvedTemplateAsset> Assets { get; set; } = [];

    public List<TemplatePageLayout> PageTemplates { get; set; } = [];

    public List<TemplateIssue> Errors { get; set; } = [];

    public List<TemplateIssue> Warnings { get; set; } = [];
}

public sealed class TemplateVariableResolution
{
    public string Name { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string Source { get; set; } = "missing";

    public bool Resolved { get; set; }
}

public sealed class ResolvedTemplateAsset
{
    public string Id { get; set; } = string.Empty;

    public TemplateAssetType Type { get; set; }

    public string Path { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public bool Required { get; set; }
}

public sealed class TemplateIssue
{
    public string Code { get; set; } = string.Empty;

    public string Path { get; set; } = "$";

    public string Message { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{Code} at {Path}: {Message}";
    }
}
