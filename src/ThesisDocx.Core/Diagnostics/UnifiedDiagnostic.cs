using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Privacy;
using ThesisDocx.Core.Validation;

namespace ThesisDocx.Core.Diagnostics;

public sealed class UnifiedDiagnostic
{
    public string Code { get; set; } = string.Empty;

    public string Severity { get; set; } = DiagnosticSeverity.Error;

    public string Path { get; set; } = "$";

    public string Message { get; set; } = string.Empty;

    public string? FixHint { get; set; }

    public string Category { get; set; } = DiagnosticCategory.Semantic;

    public string Source { get; set; } = string.Empty;

    public List<string> RelatedPaths { get; set; } = [];

    public Dictionary<string, string> Details { get; set; } = new(StringComparer.Ordinal);

    public string? DocumentationRef { get; set; }
}

public static class DiagnosticSeverity
{
    public const string Error = "error";
    public const string Warning = "warning";
    public const string Info = "info";
}

public static class DiagnosticCategory
{
    public const string Schema = "schema";
    public const string Semantic = "semantic";
    public const string Template = "template";
    public const string Rendering = "rendering";
    public const string OpenXml = "openxml";
    public const string Privacy = "privacy";
    public const string Requirement = "requirement";
    public const string Coverage = "coverage";
    public const string Regression = "regression";
    public const string Baseline = "baseline";
    public const string Intake = "intake";
}

public static class UnifiedDiagnosticMapper
{
    public static bool IsError(string? severity)
    {
        return NormalizeSeverity(severity) == DiagnosticSeverity.Error;
    }

    public static bool IsWarning(string? severity)
    {
        return NormalizeSeverity(severity) == DiagnosticSeverity.Warning;
    }

    public static int SeveritySortRank(string? severity)
    {
        return NormalizeSeverity(severity) switch
        {
            DiagnosticSeverity.Error => 0,
            DiagnosticSeverity.Warning => 1,
            _ => 2
        };
    }

    public static UnifiedDiagnostic FromInputError(ThesisInputValidationError issue, string severity, string source)
    {
        return new UnifiedDiagnostic
        {
            Code = CanonicalCode(issue.Code),
            Severity = NormalizeSeverity(severity),
            Path = NormalizePath(issue.Path),
            Message = issue.Message,
            FixHint = FixHintFor(issue.Code),
            Category = Classify(issue.Code),
            Source = source
        };
    }

    public static UnifiedDiagnostic FromValidationIssue(ValidationIssue issue, string severity, string source)
    {
        var diagnostic = new UnifiedDiagnostic
        {
            Code = CanonicalCode(issue.Code),
            Severity = NormalizeSeverity(severity),
            Path = NormalizePath(issue.Path),
            Message = issue.Message,
            FixHint = FixHintFor(issue.Code),
            Category = Classify(issue.Code),
            Source = source
        };

        if (!string.IsNullOrWhiteSpace(issue.PartName))
        {
            diagnostic.Details["partName"] = issue.PartName!;
        }

        if (!string.IsNullOrWhiteSpace(issue.Expected))
        {
            diagnostic.Details["expected"] = issue.Expected!;
        }

        if (!string.IsNullOrWhiteSpace(issue.Actual))
        {
            diagnostic.Details["actual"] = issue.Actual!;
        }

        return diagnostic;
    }

    public static UnifiedDiagnostic FromRequirementIssue(RequirementCaptureValidationIssue issue, string severity, string source)
    {
        return new UnifiedDiagnostic
        {
            Code = CanonicalCode(issue.Code),
            Severity = NormalizeSeverity(severity),
            Path = NormalizePath(issue.Path),
            Message = issue.Message,
            FixHint = FixHintFor(issue.Code),
            Category = DiagnosticCategory.Requirement,
            Source = source
        };
    }

    public static UnifiedDiagnostic FromPrivacyFinding(PrivacyFinding finding, string source)
    {
        return new UnifiedDiagnostic
        {
            Code = CanonicalCode(finding.Code),
            Severity = NormalizeSeverity(finding.Severity),
            Path = NormalizePath(finding.Path),
            Message = finding.Message,
            FixHint = string.IsNullOrWhiteSpace(finding.SuggestedAction) ? FixHintFor(finding.Code) : finding.SuggestedAction,
            Category = DiagnosticCategory.Privacy,
            Source = source
        };
    }

    public static UnifiedDiagnostic FromDiagnosticIssue(DiagnosticIssue issue)
    {
        var diagnostic = new UnifiedDiagnostic
        {
            Code = CanonicalCode(string.IsNullOrWhiteSpace(issue.Id) ? issue.Code : issue.Id),
            Severity = NormalizeSeverity(issue.Severity),
            Path = NormalizePath(issue.Path ?? issue.SpecPath ?? issue.TemplatePath ?? issue.PartName),
            Message = issue.Message,
            FixHint = issue.FixHint ?? issue.FixHints.FirstOrDefault()?.SuggestedAction ?? FixHintFor(issue.Id),
            Category = NormalizeCategory(issue.Category),
            Source = issue.Source,
            RelatedPaths = issue.RelatedPaths.ToList(),
            DocumentationRef = issue.DocumentationRef ?? issue.RelatedDocs.FirstOrDefault()
        };

        if (!string.IsNullOrWhiteSpace(issue.PartName))
        {
            diagnostic.Details["partName"] = issue.PartName!;
        }

        if (!string.IsNullOrWhiteSpace(issue.Expected))
        {
            diagnostic.Details["expected"] = issue.Expected!;
        }

        if (!string.IsNullOrWhiteSpace(issue.Actual))
        {
            diagnostic.Details["actual"] = issue.Actual!;
        }

        foreach (var pair in issue.Details)
        {
            diagnostic.Details[pair.Key] = pair.Value;
        }

        return diagnostic;
    }

    public static string NormalizeSeverity(string? severity)
    {
        return severity?.Trim().ToLowerInvariant() switch
        {
            "error" or "breaking" or "fatal" or "fail" => DiagnosticSeverity.Error,
            "warning" or "warn" => DiagnosticSeverity.Warning,
            "info" or "information" or "pass" => DiagnosticSeverity.Info,
            _ => DiagnosticSeverity.Error
        };
    }

    public static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return DiagnosticCategory.Semantic;
        }

        return category.Trim() switch
        {
            "requirements" or "requirement" => DiagnosticCategory.Requirement,
            "negativeFixtures" => "negativeFixture",
            "crossReference" or "document" or "heading" or "table" or "note" or "bookmark" or "citation" => DiagnosticCategory.Semantic,
            "validation" => DiagnosticCategory.Semantic,
            "pageSetup" or "pageNumber" or "toc" or "figure" or "equation" or "bibliography" => DiagnosticCategory.Semantic,
            "openxml" => DiagnosticCategory.OpenXml,
            "template" => DiagnosticCategory.Template,
            "baseline" => DiagnosticCategory.Baseline,
            "regression" => DiagnosticCategory.Regression,
            "coverage" => DiagnosticCategory.Coverage,
            "privacy" => DiagnosticCategory.Privacy,
            "intake" => DiagnosticCategory.Intake,
            var value => value
        };
    }

    public static string CanonicalCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "diagnostic.unknown";
        }

        if (code.StartsWith("schema.document.PropertyRequired.schemaVersion", StringComparison.Ordinal))
        {
            return "thesis.schemaVersion.missing";
        }

        if (code.StartsWith("schema.document.PropertyRequired.metadata", StringComparison.Ordinal))
        {
            return "thesis.metadata.missing";
        }

        if (code.StartsWith("schema.document.PropertyRequired.sections", StringComparison.Ordinal))
        {
            return "thesis.sections.missing";
        }

        return code switch
        {
            "unsupported.documentSchemaVersion" => "thesis.schemaVersion.unsupported",
            "unsupported.formatSchemaVersion" => "format.schemaVersion.unsupported",
            "table.gridSpan.invalid" => "table.gridSpan.outOfRange",
            "table.gridSpan.tooWide" => "table.gridSpan.outOfRange",
            "table.verticalMerge.invalidChain" => "table.vMerge.missingRestart",
            "table.grid.inconsistent" => "table.gridWidth.inconsistent",
            "duplicate.blockOrBookmarkId" => "bookmark.duplicate",
            "dangling.reference" => "ref.targetMissing",
            "dangling.citation" => "citation.targetMissing",
            "duplicate.footnoteId" => "note.id.duplicate",
            "duplicate.endnoteId" => "note.id.duplicate",
            "note.empty" => "note.content.empty",
            "template.variable.duplicate" => "template.variable.duplicate",
            "template.pageTemplate.variable.missing" => "template.variable.missing",
            "template.pageTemplate.image.asset.missing" => "template.asset.missing",
            "template.asset.missing" => "template.asset.missing",
            "template.pageTemplate.duplicate" => "template.pageTemplate.duplicate",
            "template.pageTemplate.block.unsupported" => "template.pageTemplate.elementUnsupported",
            "template.pageTemplate.pageSetup.margin.invalid" => "format.margin.negative",
            "openxml.schema" => "openxml.validation.error",
            "SchemaDocumentPropertyRequired" => "thesis.requiredProperty.missing",
            "TableGridSpanTooWide" => "table.gridSpan.outOfRange",
            "TableVerticalMergeInvalidChain" => "table.vMerge.missingRestart",
            "TableGridInconsistent" => "table.gridWidth.inconsistent",
            "DuplicateBookmarkId" => "bookmark.duplicate",
            "CrossReferenceTargetMissing" => "ref.targetMissing",
            "DuplicateNoteId" => "note.id.duplicate",
            "NoteContentEmpty" => "note.content.empty",
            "TemplateDuplicateVariable" => "template.variable.duplicate",
            "TemplateVariableReferenceMissing" => "template.variable.missing",
            "TemplatePageTemplateImageAssetMissing" => "template.asset.missing",
            "TemplateIllegalMargin" => "format.margin.negative",
            _ => code
        };
    }

    private static string Classify(string code)
    {
        var canonical = CanonicalCode(code);
        if (canonical.StartsWith("schema.", StringComparison.Ordinal) || canonical.Contains(".schemaVersion.", StringComparison.Ordinal))
        {
            return DiagnosticCategory.Schema;
        }

        if (canonical.StartsWith("template.", StringComparison.Ordinal))
        {
            return DiagnosticCategory.Template;
        }

        if (canonical.StartsWith("openxml.", StringComparison.Ordinal))
        {
            return DiagnosticCategory.OpenXml;
        }

        if (canonical.StartsWith("privacy.", StringComparison.Ordinal))
        {
            return DiagnosticCategory.Privacy;
        }

        if (canonical.StartsWith("requirements.", StringComparison.Ordinal) || canonical.StartsWith("requirement.", StringComparison.Ordinal))
        {
            return DiagnosticCategory.Requirement;
        }

        return DiagnosticCategory.Semantic;
    }

    private static string NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? "$" : path!;
    }

    private static string? FixHintFor(string? code)
    {
        return CanonicalCode(code) switch
        {
            "thesis.schemaVersion.missing" => "Add a supported schemaVersion to the ThesisDocument.",
            "thesis.metadata.missing" => "Add the required metadata object.",
            "thesis.sections.missing" => "Add at least one thesis section.",
            "thesis.schemaVersion.unsupported" => "Use a supported ThesisDocument schema version or add an explicit migration.",
            "format.schemaVersion.unsupported" => "Use a supported ThesisFormatSpec schema version or add an explicit migration.",
            "table.gridSpan.outOfRange" => "Keep gridSpan within the logical table grid.",
            "table.vMerge.missingRestart" => "Start each vertical merge chain with restart before continue cells.",
            "table.gridWidth.inconsistent" => "Make every table row resolve to the same logical column count.",
            "bookmark.duplicate" => "Give each bookmark and block id a unique value.",
            "ref.targetMissing" => "Point the REF inline to an existing bookmark, heading, figure, table, or equation target.",
            "note.id.duplicate" => "Use unique note ids within the document.",
            "note.content.empty" => "Add note text or remove the empty note.",
            "template.variable.duplicate" => "Give each template variable a unique name.",
            "template.variable.missing" => "Define the referenced variable or remove the reference.",
            "template.asset.missing" => "Define the referenced asset and keep it inside the template package.",
            "format.margin.negative" => "Use non-negative page margins.",
            "openxml.validation.error" => "Inspect the generated OpenXML part and fix the renderer or input structure.",
            _ => null
        };
    }
}
