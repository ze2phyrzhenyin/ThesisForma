using NJsonSchema;
using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Versioning;

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

    public ThesisInputValidationResult ValidateVersionReportFile(string reportPath, string schemaPath)
    {
        return ValidateJsonFile(reportPath, schemaPath, "versionReport");
    }

    public ThesisInputValidationResult ValidateReportContractFile(string reportPath, string schemaPath)
    {
        return ValidateJsonFile(reportPath, schemaPath, "reportContract");
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
        var json = File.ReadAllText(jsonPath);
        result.VersionReport = BuildVersionReport(json, rootName);
        var errors = schema.Validate(json);

        foreach (var error in errors)
        {
            result.Add(
                $"schema.{rootName}.{error.Kind}",
                error.Path ?? "$",
                error.ToString());
        }

        return result;
    }

    private static SchemaVersionReport BuildVersionReport(string json, string rootName)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject();
        }
        catch (JsonException)
        {
            return SchemaVersionReport.Empty();
        }

        if (root is null)
        {
            return SchemaVersionReport.Empty();
        }

        return rootName switch
        {
            "document" => SchemaVersionReport.ForDocument(ReadString(root, "schemaVersion")),
            "format" => SchemaVersionReport.ForFormat(ReadString(root, "schemaVersion")),
            "template" => SchemaVersionReport.ForTemplatePackage(ReadString(root, "templateSchemaVersion")),
            _ => SchemaVersionReport.Empty()
        };
    }

    private static string? ReadString(JsonObject root, string propertyName)
    {
        return root[propertyName] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    }

    public static bool IsSupportedVersion(string? schemaVersion)
    {
        return ThesisSchemaVersions.IsSupported(schemaVersion);
    }
}
