namespace ThesisDocx.Core.Templates.Authoring;

public sealed class TemplateAuthoringChecklist
{
    public List<TemplateAuthoringChecklistItem> Items { get; set; } = [];

    public bool Passed => Items.All(item => item.Status == "pass" || item.Status == "warning");
}
