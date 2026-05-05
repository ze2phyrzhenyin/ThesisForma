namespace ThesisDocx.Core.Models.Requirements;

public sealed class RequirementMapping
{
    public string RequirementId { get; set; } = string.Empty;

    public string? SpecPath { get; set; }

    public string? TemplatePath { get; set; }

    public RequirementMappingStatus MappingStatus { get; set; } = RequirementMappingStatus.Unmapped;

    public string Notes { get; set; } = string.Empty;
}

public enum RequirementMappingStatus
{
    Unmapped,
    Mapped,
    Partial,
    NotSupported
}
