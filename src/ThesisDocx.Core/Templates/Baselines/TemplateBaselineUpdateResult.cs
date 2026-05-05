namespace ThesisDocx.Core.Templates.Baselines;

public sealed class TemplateBaselineUpdateResult
{
    public bool Updated { get; set; }

    public string CaseId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public List<string> UpdatedFiles { get; set; } = [];

    public List<string> Changes { get; set; } = [];

    public List<string> Errors { get; set; } = [];
}
