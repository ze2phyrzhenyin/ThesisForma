namespace ThesisDocx.Core.Validation;

public sealed class ThesisInputValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<ThesisInputValidationError> Errors { get; set; } = [];

    public List<ThesisInputValidationError> Warnings { get; set; } = [];

    public void Add(string code, string path, string message)
    {
        Errors.Add(new ThesisInputValidationError
        {
            Code = code,
            Path = path,
            Message = message
        });
    }

    public void AddWarning(string code, string path, string message)
    {
        Warnings.Add(new ThesisInputValidationError
        {
            Code = code,
            Path = path,
            Message = message
        });
    }
}
