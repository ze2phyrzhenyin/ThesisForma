using NJsonSchema;
using ThesisDocx.Core.Models;

namespace ThesisDocx.Core.Validation;

public sealed class ThesisSchemaValidator
{
    public ThesisInputValidationResult ValidateDocumentFile(string documentPath, string schemaPath)
    {
        return ValidateJsonFile(documentPath, schemaPath, "document");
    }

    public ThesisInputValidationResult ValidateFormatFile(string formatPath, string schemaPath)
    {
        return ValidateJsonFile(formatPath, schemaPath, "format");
    }

    public ThesisInputValidationResult ValidateTemplateFile(string templatePath, string schemaPath)
    {
        return ValidateJsonFile(templatePath, schemaPath, "template");
    }

    public ThesisInputValidationResult ValidateTemplateRegressionSuiteFile(string suitePath, string schemaPath)
    {
        return ValidateJsonFile(suitePath, schemaPath, "templateRegressionSuite");
    }

    public ThesisInputValidationResult ValidateRequirementCaptureFile(string requirementsPath, string schemaPath)
    {
        return ValidateJsonFile(requirementsPath, schemaPath, "requirementCapture");
    }

    public ThesisInputValidationResult ValidateTemplateBaselineManifestFile(string manifestPath, string schemaPath)
    {
        return ValidateJsonFile(manifestPath, schemaPath, "templateBaselineManifest");
    }

    public ThesisInputValidationResult ValidateNegativeFixtureManifestFile(string manifestPath, string schemaPath)
    {
        return ValidateJsonFile(manifestPath, schemaPath, "negativeFixtureManifest");
    }

    public ThesisInputValidationResult ValidateFixHintRulesFile(string rulesPath, string schemaPath)
    {
        return ValidateJsonFile(rulesPath, schemaPath, "fixHintRules");
    }

    public ThesisInputValidationResult ValidateDiagnosticReportFile(string reportPath, string schemaPath)
    {
        return ValidateJsonFile(reportPath, schemaPath, "diagnosticReport");
    }

    public ThesisInputValidationResult ValidateOnboardingWorkspaceFile(string workspacePath, string schemaPath)
    {
        return ValidateJsonFile(workspacePath, schemaPath, "onboardingWorkspace");
    }

    public ThesisInputValidationResult ValidateTemplatePilotPackageManifestFile(string manifestPath, string schemaPath)
    {
        return ValidateJsonFile(manifestPath, schemaPath, "templatePilotPackageManifest");
    }

    public ThesisInputValidationResult ValidateDocxExtractionFile(string extractionPath, string schemaPath)
    {
        return ValidateJsonFile(extractionPath, schemaPath, "docxExtraction");
    }

    private static ThesisInputValidationResult ValidateJsonFile(string jsonPath, string schemaPath, string rootName)
    {
        var result = new ThesisInputValidationResult { Source = "ThesisSchemaValidator" };
        var schema = JsonSchema.FromJsonAsync(File.ReadAllText(schemaPath)).GetAwaiter().GetResult();
        var errors = schema.Validate(File.ReadAllText(jsonPath));

        foreach (var error in errors)
        {
            result.Add(
                $"schema.{rootName}.{error.Kind}",
                error.Path ?? "$",
                error.ToString());
        }

        return result;
    }

    public static bool IsSupportedVersion(string? schemaVersion)
    {
        return ThesisSchemaVersions.IsSupported(schemaVersion);
    }
}
