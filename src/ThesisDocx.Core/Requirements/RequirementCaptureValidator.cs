using System.Text.RegularExpressions;
using ThesisDocx.Core.Models.Requirements;

namespace ThesisDocx.Core.Requirements;

public sealed class RequirementCaptureValidator
{
    private static readonly Regex TargetPathPattern = new(@"^\$(\.[A-Za-z][A-Za-z0-9_-]*|\[[0-9]+\])*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public RequirementCaptureValidationResult Validate(RequirementCapture capture)
    {
        var result = new RequirementCaptureValidationResult();
        if (!RequirementCaptureSchemaVersions.IsSupported(capture.SchemaVersion))
        {
            AddError(result, "requirements.schemaVersion.unsupported", "$.schemaVersion", $"Unsupported requirement capture schemaVersion '{capture.SchemaVersion}'.");
        }

        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in capture.SourceDocuments)
        {
            if (!sourceIds.Add(source.Id))
            {
                AddError(result, "requirements.source.duplicateId", $"$.sourceDocuments[{source.Id}]", $"Duplicate source id '{source.Id}'.");
            }

            if (Path.IsPathRooted(source.Path))
            {
                AddError(result, "requirements.source.absolutePath", $"$.sourceDocuments[{source.Id}].path", "Source document path must be relative.");
            }
        }

        var requirementIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requirement in capture.Requirements)
        {
            if (!requirementIds.Add(requirement.Id))
            {
                AddError(result, "requirements.item.duplicateId", $"$.requirements[{requirement.Id}]", $"Duplicate requirement id '{requirement.Id}'.");
            }

            if (!sourceIds.Contains(requirement.SourceId))
            {
                AddError(result, "requirements.item.sourceMissing", $"$.requirements[{requirement.Id}].sourceId", $"Source id '{requirement.SourceId}' does not exist.");
            }

            if (requirement.ReviewStatus == RequirementReviewStatus.Approved && IsEvidenceEmpty(requirement.Evidence))
            {
                AddError(result, "requirements.item.evidenceMissing", $"$.requirements[{requirement.Id}].evidence", "Approved requirement must include evidence.");
            }

            if (requirement.ReviewStatus == RequirementReviewStatus.Approved && requirement.Confidence == RequirementConfidence.Low)
            {
                AddWarning(result, "requirements.item.lowConfidenceApproved", $"$.requirements[{requirement.Id}].confidence", "Approved requirement has low confidence.");
            }

            ValidateTargetPath(result, requirement.TargetSpecPath, $"$.requirements[{requirement.Id}].targetSpecPath");
            ValidateTargetPath(result, requirement.TargetTemplatePath, $"$.requirements[{requirement.Id}].targetTemplatePath");
        }

        var mappings = capture.Mappings.GroupBy(m => m.RequirementId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        foreach (var mapping in capture.Mappings)
        {
            if (!requirementIds.Contains(mapping.RequirementId))
            {
                AddError(result, "requirements.mapping.requirementMissing", $"$.mappings[{mapping.RequirementId}].requirementId", $"Requirement id '{mapping.RequirementId}' does not exist.");
            }

            if (mapping.MappingStatus == RequirementMappingStatus.NotSupported && string.IsNullOrWhiteSpace(mapping.Notes))
            {
                AddError(result, "requirements.mapping.notSupportedNotesMissing", $"$.mappings[{mapping.RequirementId}].notes", "notSupported mapping must include notes.");
            }

            ValidateTargetPath(result, mapping.SpecPath, $"$.mappings[{mapping.RequirementId}].specPath");
            ValidateTargetPath(result, mapping.TemplatePath, $"$.mappings[{mapping.RequirementId}].templatePath");
        }

        foreach (var requirement in capture.Requirements.Where(r => r.ReviewStatus == RequirementReviewStatus.Approved))
        {
            var status = mappings.TryGetValue(requirement.Id, out var mapped)
                ? mapped.Select(m => m.MappingStatus).DefaultIfEmpty(RequirementMappingStatus.Unmapped).Max()
                : RequirementMappingStatus.Unmapped;
            if (status == RequirementMappingStatus.Unmapped)
            {
                AddError(result, "requirements.item.approvedUnmapped", $"$.requirements[{requirement.Id}]", "Approved requirement must be mapped, partial, or notSupported.");
            }
        }

        result.Errors = result.Errors.OrderBy(e => e.Path, StringComparer.Ordinal).ThenBy(e => e.Code, StringComparer.Ordinal).ToList();
        result.Warnings = result.Warnings.OrderBy(e => e.Path, StringComparer.Ordinal).ThenBy(e => e.Code, StringComparer.Ordinal).ToList();
        return result;
    }

    private static bool IsEvidenceEmpty(RequirementEvidence evidence)
    {
        return string.IsNullOrWhiteSpace(evidence.ShortQuote)
            && string.IsNullOrWhiteSpace(evidence.Page)
            && string.IsNullOrWhiteSpace(evidence.Section)
            && string.IsNullOrWhiteSpace(evidence.ScreenshotRef);
    }

    private static void ValidateTargetPath(RequirementCaptureValidationResult result, string? value, string path)
    {
        if (!string.IsNullOrWhiteSpace(value) && !TargetPathPattern.IsMatch(value))
        {
            AddError(result, "requirements.targetPath.invalid", path, $"Invalid target path '{value}'.");
        }
    }

    private static void AddError(RequirementCaptureValidationResult result, string code, string path, string message)
    {
        result.Errors.Add(new RequirementCaptureValidationIssue { Code = code, Path = path, Message = message });
    }

    private static void AddWarning(RequirementCaptureValidationResult result, string code, string path, string message)
    {
        result.Warnings.Add(new RequirementCaptureValidationIssue { Code = code, Path = path, Message = message });
    }
}
