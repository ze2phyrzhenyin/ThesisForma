namespace ThesisDocx.Core.Templates.Authoring;

public sealed class TemplateAuthoringChecklistItem
{
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";

    public string Message { get; set; } = string.Empty;
}
