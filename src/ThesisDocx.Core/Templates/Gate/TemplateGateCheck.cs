namespace ThesisDocx.Core.Templates.Gate;

public sealed class TemplateGateCheck
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TemplateGateCheckStatus Status { get; set; }

    public string Message { get; set; } = string.Empty;
}

public enum TemplateGateCheckStatus
{
    Pass,
    Warning,
    Fail
}
