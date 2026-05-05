namespace ThesisDocx.Core.Validation;

public sealed class ThesisInputValidationError
{
    public string Code { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{Code} at {Path}: {Message}";
    }
}
