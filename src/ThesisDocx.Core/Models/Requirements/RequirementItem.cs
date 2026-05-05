namespace ThesisDocx.Core.Models.Requirements;

public sealed class RequirementItem
{
    public string Id { get; set; } = string.Empty;

    public RequirementCategory Category { get; set; } = RequirementCategory.Other;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public RequirementEvidence Evidence { get; set; } = new();

    public string NormalizedValue { get; set; } = string.Empty;

    public string? TargetSpecPath { get; set; }

    public string? TargetTemplatePath { get; set; }

    public RequirementConfidence Confidence { get; set; } = RequirementConfidence.Medium;

    public RequirementReviewStatus ReviewStatus { get; set; } = RequirementReviewStatus.Draft;

    public string? Reviewer { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public enum RequirementCategory
{
    PageSetup,
    Section,
    Font,
    Paragraph,
    Heading,
    HeaderFooter,
    Toc,
    Abstract,
    Cover,
    Declaration,
    Table,
    Figure,
    Equation,
    Citation,
    FootnoteEndnote,
    Bibliography,
    Appendix,
    Asset,
    Other
}

public enum RequirementConfidence
{
    High,
    Medium,
    Low
}

public enum RequirementReviewStatus
{
    Draft,
    Reviewed,
    Approved,
    Rejected
}
