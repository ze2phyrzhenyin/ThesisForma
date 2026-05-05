using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Validation;

namespace ThesisDocx.Core.Templates;

public sealed class TemplateValidationService
{
    public ThesisInputValidationResult Validate(string templatePath, string? schemaPath = null)
    {
        var result = new ThesisInputValidationResult();
        if (schemaPath is not null)
        {
            Merge(result, new ThesisSchemaValidator().ValidateTemplateFile(Path.Combine(templatePath, "template.json"), schemaPath));
        }

        var resolution = new TemplateResolver().Resolve(templatePath);
        foreach (var error in resolution.Errors)
        {
            result.Add(error.Code, error.Path, error.Message);
        }

        foreach (var warning in resolution.Warnings)
        {
            result.AddWarning(warning.Code, warning.Path, warning.Message);
        }

        if (resolution.FormatSpec is not null && !ThesisSchemaVersions.IsSupportedFormat(resolution.FormatSpec.SchemaVersion))
        {
            result.Add("template.formatSpec.unsupportedSchemaVersion", "$.formatSpec.schemaVersion", $"Unsupported format spec schemaVersion '{resolution.FormatSpec.SchemaVersion}'.");
        }

        return result;
    }

    private static void Merge(ThesisInputValidationResult target, ThesisInputValidationResult source)
    {
        target.Errors.AddRange(source.Errors);
        target.Warnings.AddRange(source.Warnings);
    }
}
