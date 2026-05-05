using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace ThesisDocx.Core.Validation;

public sealed class OpenXmlPackageValidator
{
    public OpenXmlValidationResult Validate(string docxPath)
    {
        if (!File.Exists(docxPath))
        {
            throw new FileNotFoundException("DOCX file not found.", docxPath);
        }

        using var document = WordprocessingDocument.Open(docxPath, false);
        var validator = new OpenXmlValidator();
        var result = new OpenXmlValidationResult();

        foreach (var error in validator.Validate(document))
        {
            result.Errors.Add(new ValidationIssue
            {
                Code = "openxml.schema",
                Message = error.Description ?? "OpenXML validation error.",
                PartName = error.Part?.Uri?.ToString(),
                Path = error.Path?.XPath
            });
        }

        return result;
    }
}
