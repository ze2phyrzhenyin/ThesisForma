namespace ThesisDocx.Core.Models.Requirements;

public sealed class RequirementMetadata
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string School { get; set; } = string.Empty;

    public string College { get; set; } = string.Empty;

    public string DegreeType { get; set; } = string.Empty;

    public string Locale { get; set; } = "zh-CN";
}
