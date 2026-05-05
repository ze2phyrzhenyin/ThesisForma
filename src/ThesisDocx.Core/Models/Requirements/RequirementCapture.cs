namespace ThesisDocx.Core.Models.Requirements;

public sealed class RequirementCapture
{
    public string SchemaVersion { get; set; } = RequirementCaptureSchemaVersions.Current;

    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string School { get; set; } = string.Empty;

    public string College { get; set; } = string.Empty;

    public string DegreeType { get; set; } = string.Empty;

    public string Locale { get; set; } = "zh-CN";

    public List<RequirementSource> SourceDocuments { get; set; } = [];

    public List<RequirementItem> Requirements { get; set; } = [];

    public List<RequirementMapping> Mappings { get; set; } = [];

    public List<string> UnresolvedItems { get; set; } = [];

    public RequirementApproval Approval { get; set; } = new();
}

public static class RequirementCaptureSchemaVersions
{
    public const string Version100 = "1.0.0";
    public const string Current = Version100;

    public static bool IsSupported(string? version) => version == Version100;
}

public sealed class RequirementApproval
{
    public string PreparedBy { get; set; } = string.Empty;

    public string? ReviewedBy { get; set; }

    public string? ApprovedBy { get; set; }

    public string? ApprovedAt { get; set; }

    public string Notes { get; set; } = string.Empty;
}
