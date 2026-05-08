using System.Text.Json;
using System.Text.RegularExpressions;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Core.Versioning;

namespace ThesisDocx.Core.Templates;

public sealed class TemplateValidationService
{
    private static readonly HashSet<string> MetadataSourcePaths = new(StringComparer.Ordinal)
    {
        "metadata.title",
        "metadata.subtitle",
        "metadata.author",
        "metadata.college",
        "metadata.major",
        "metadata.studentId",
        "metadata.advisor",
        "metadata.date",
        "metadata.language"
    };

    public ThesisInputValidationResult Validate(string templatePath, string? schemaPath = null)
    {
        var result = new ThesisInputValidationResult { Source = "TemplateValidationService" };
        if (schemaPath is not null)
        {
            Merge(result, new ThesisSchemaValidator().ValidateTemplateFile(Path.Combine(templatePath, "template.json"), schemaPath));
        }

        var template = new TemplateLoader().Load(templatePath);
        ValidateTemplatePackage(template, result);

        var resolution = new TemplateResolver().Resolve(templatePath);
        result.VersionReport = SchemaVersionReport.ForTemplate(template.TemplateSchemaVersion, resolution.FormatSpec?.SchemaVersion);
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

        if (resolution.FormatSpec is not null && schemaPath is not null)
        {
            var formatSchemaPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(schemaPath))!, "thesis-format-spec.schema.json");
            if (File.Exists(formatSchemaPath))
            {
                var tempFormatPath = Path.Combine(Path.GetTempPath(), "ThesisDocx.TemplateValidation", $"{Guid.NewGuid():N}.format.json");
                Directory.CreateDirectory(Path.GetDirectoryName(tempFormatPath)!);
                File.WriteAllText(tempFormatPath, JsonSerializer.Serialize(resolution.FormatSpec, ThesisJson.Options));
                Merge(result, new ThesisSchemaValidator().ValidateFormatFile(tempFormatPath, formatSchemaPath));
            }
        }

        return result;
    }

    private static void ValidateTemplatePackage(TemplatePackage template, ThesisInputValidationResult result)
    {
        ValidateDuplicates(template.Variables, variable => variable.Name, "$.variables", "template.variable.duplicate", result);
        ValidateDuplicates(template.Assets, asset => asset.Id, "$.assets", "template.asset.duplicate", result);
        ValidateDuplicates(template.PageTemplates, page => page.Id, "$.pageTemplates", "template.pageTemplate.duplicate", result);

        var variables = template.Variables.Select(variable => variable.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToHashSet(StringComparer.Ordinal);
        var assets = template.Assets.Select(asset => asset.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
        for (var templateIndex = 0; templateIndex < template.PageTemplates.Count; templateIndex++)
        {
            var pageTemplate = template.PageTemplates[templateIndex];
            var path = $"$.pageTemplates[{templateIndex}]";
            ValidatePageSetupOverride(pageTemplate.PageSetupOverride, $"{path}.pageSetupOverride", result);
            for (var blockIndex = 0; blockIndex < pageTemplate.Blocks.Count; blockIndex++)
            {
                ValidateLayoutBlock(pageTemplate.Blocks[blockIndex], $"{path}.blocks[{blockIndex}]", variables, assets, result);
            }
        }
    }

    private static void ValidateLayoutBlock(PageLayoutBlock block, string path, HashSet<string> variables, HashSet<string> assets, ThesisInputValidationResult result)
    {
        switch (block)
        {
            case TextLayoutBlock text:
                ValidateTemplateTextReferences(text.Value, $"{path}.value", variables, result);
                break;
            case MetadataFieldLayoutBlock field:
                ValidateMetadataField(field, path, variables, result);
                break;
            case ImageLayoutBlock image:
                if (string.IsNullOrWhiteSpace(image.AssetId))
                {
                    result.Add("template.pageTemplate.image.asset.required", $"{path}.assetId", "Page template image block requires assetId.");
                }
                else if (!assets.Contains(image.AssetId))
                {
                    result.Add("template.pageTemplate.image.asset.missing", $"{path}.assetId", $"Page template image block references missing asset '{image.AssetId}'.");
                }

                break;
            case FieldTableLayoutBlock table:
                for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                {
                    for (var fieldIndex = 0; fieldIndex < table.Rows[rowIndex].Count; fieldIndex++)
                    {
                        ValidateMetadataField(table.Rows[rowIndex][fieldIndex], $"{path}.rows[{rowIndex}][{fieldIndex}]", variables, result);
                    }
                }

                break;
            case DeclarationTextLayoutBlock declaration:
                for (var paragraphIndex = 0; paragraphIndex < declaration.Paragraphs.Count; paragraphIndex++)
                {
                    ValidateTemplateTextReferences(declaration.Paragraphs[paragraphIndex], $"{path}.paragraphs[{paragraphIndex}]", variables, result);
                }

                for (var fieldIndex = 0; fieldIndex < declaration.SignatureFields.Count; fieldIndex++)
                {
                    ValidateMetadataField(declaration.SignatureFields[fieldIndex], $"{path}.signatureFields[{fieldIndex}]", variables, result);
                }

                break;
        }
    }

    private static void ValidateMetadataField(MetadataFieldLayoutBlock field, string path, HashSet<string> variables, ThesisInputValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(field.VariableName) && !variables.Contains(field.VariableName))
        {
            result.Add("template.pageTemplate.variable.missing", $"{path}.variableName", $"Page template references missing variable '{field.VariableName}'.");
        }

        if (!string.IsNullOrWhiteSpace(field.ValueTemplate))
        {
            ValidateTemplateTextReferences(field.ValueTemplate, $"{path}.valueTemplate", variables, result);
        }

        if (!string.IsNullOrWhiteSpace(field.SourcePath))
        {
            if (!field.SourcePath.StartsWith("metadata.", StringComparison.Ordinal) && !field.SourcePath.StartsWith("variables.", StringComparison.Ordinal))
            {
                result.Add("template.pageTemplate.sourcePath.invalid", $"{path}.sourcePath", "sourcePath must start with metadata. or variables.");
            }
            else if (field.SourcePath.StartsWith("metadata.", StringComparison.Ordinal) && !MetadataSourcePaths.Contains(field.SourcePath))
            {
                result.AddWarning("template.pageTemplate.metadataField.unknown", $"{path}.sourcePath", $"Metadata field '{field.SourcePath}' is not part of ThesisMetadata.");
            }
            else if (field.SourcePath.StartsWith("variables.", StringComparison.Ordinal) && !variables.Contains(field.SourcePath["variables.".Length..]))
            {
                result.Add("template.pageTemplate.variable.missing", $"{path}.sourcePath", $"Page template references missing variable '{field.SourcePath}'.");
            }
        }
    }

    private static void ValidateTemplateTextReferences(string? value, string path, HashSet<string> variables, ThesisInputValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (Match match in Regex.Matches(value, @"\{\{\s*variables\.([A-Za-z][A-Za-z0-9_.-]*)\s*\}\}", RegexOptions.CultureInvariant))
        {
            var variableName = match.Groups[1].Value;
            if (!variables.Contains(variableName))
            {
                result.Add("template.pageTemplate.variable.missing", path, $"Page template references missing variable '{variableName}'.");
            }
        }

        foreach (Match match in Regex.Matches(value, @"\{\{\s*metadata\.([A-Za-z][A-Za-z0-9_.-]*)\s*\}\}", RegexOptions.CultureInvariant))
        {
            var metadataPath = $"metadata.{match.Groups[1].Value}";
            if (!MetadataSourcePaths.Contains(metadataPath))
            {
                result.AddWarning("template.pageTemplate.metadataField.unknown", path, $"Metadata field '{metadataPath}' is not part of ThesisMetadata.");
            }
        }
    }

    private static void ValidatePageSetupOverride(PageSetupSpec? pageSetup, string path, ThesisInputValidationResult result)
    {
        if (pageSetup is null)
        {
            return;
        }

        ValidateRange(pageSetup.TopMarginCm, 0.1, 10, $"{path}.topMarginCm", "template.pageTemplate.pageSetup.margin.invalid", "Page setup margins must be between 0.1 and 10 cm.", result);
        ValidateRange(pageSetup.BottomMarginCm, 0.1, 10, $"{path}.bottomMarginCm", "template.pageTemplate.pageSetup.margin.invalid", "Page setup margins must be between 0.1 and 10 cm.", result);
        ValidateRange(pageSetup.LeftMarginCm, 0.1, 10, $"{path}.leftMarginCm", "template.pageTemplate.pageSetup.margin.invalid", "Page setup margins must be between 0.1 and 10 cm.", result);
        ValidateRange(pageSetup.RightMarginCm, 0.1, 10, $"{path}.rightMarginCm", "template.pageTemplate.pageSetup.margin.invalid", "Page setup margins must be between 0.1 and 10 cm.", result);
        ValidateRange(pageSetup.GutterCm, 0, 5, $"{path}.gutterCm", "template.pageTemplate.pageSetup.margin.invalid", "Page setup gutter must be between 0 and 5 cm.", result);
        ValidateRange(pageSetup.HeaderDistanceCm, 0, 5, $"{path}.headerDistanceCm", "template.pageTemplate.pageSetup.margin.invalid", "Header distance must be between 0 and 5 cm.", result);
        ValidateRange(pageSetup.FooterDistanceCm, 0, 5, $"{path}.footerDistanceCm", "template.pageTemplate.pageSetup.margin.invalid", "Footer distance must be between 0 and 5 cm.", result);
        if (pageSetup.Columns is < 1 or > 4)
        {
            result.Add("template.pageTemplate.pageSetup.columns.invalid", $"{path}.columns", "Page setup columns must be between 1 and 4.");
        }
    }

    private static void ValidateRange(double value, double min, double max, string path, string code, string message, ThesisInputValidationResult result)
    {
        if (value < min || value > max)
        {
            result.Add(code, path, message);
        }
    }

    private static void ValidateDuplicates<T>(IReadOnlyList<T> values, Func<T, string> keySelector, string path, string code, ThesisInputValidationResult result)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            var key = keySelector(values[index]);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (seen.TryGetValue(key, out var firstIndex))
            {
                result.Add(code, $"{path}[{index}]", $"'{key}' duplicates {path}[{firstIndex}].");
            }
            else
            {
                seen[key] = index;
            }
        }
    }

    private static void Merge(ThesisInputValidationResult target, ThesisInputValidationResult source)
    {
        target.Errors.AddRange(source.Errors);
        target.Warnings.AddRange(source.Warnings);
        target.VersionReport.MergeFrom(source.VersionReport);
    }
}
