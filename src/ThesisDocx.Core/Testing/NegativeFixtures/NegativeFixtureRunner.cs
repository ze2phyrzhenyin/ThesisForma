using System.Text.Json;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Requirements;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;

namespace ThesisDocx.Core.Testing.NegativeFixtures;

public sealed class NegativeFixtureRunner
{
    private readonly FixHintEngine _fixHints = new();

    public NegativeFixtureRunResult Run(string manifestPath)
    {
        var manifest = JsonSerializer.Deserialize<NegativeFixtureManifest>(File.ReadAllText(manifestPath), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not read negative fixture manifest '{manifestPath}'.");
        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var result = new NegativeFixtureRunResult { SuiteId = manifest.SuiteId };
        foreach (var fixture in manifest.Cases.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            result.Cases.Add(RunCase(fixture, manifestDirectory));
        }

        return result;
    }

    private NegativeFixtureCaseResult RunCase(NegativeFixtureCase fixture, string manifestDirectory)
    {
        var result = new NegativeFixtureCaseResult
        {
            Id = fixture.Id,
            Type = fixture.Type,
            ExpectedExitCode = fixture.ExpectedExitCode,
            ExpectedCodes = fixture.ExpectedCodes.Order(StringComparer.Ordinal).ToList(),
            ExpectedSeverity = fixture.ExpectedSeverity,
            ExpectedFixHintIds = fixture.ExpectedFixHintIds.Order(StringComparer.Ordinal).ToList()
        };

        try
        {
            var path = ResolvePath(manifestDirectory, fixture.Path);
            result.Issues = fixture.Type switch
            {
                "requirement" => RunRequirement(path),
                "template" => RunTemplate(path),
                "document" => RunDocument(path),
                _ => RunMetadata(path)
            };
        }
        catch (Exception ex)
        {
            result.Issues.Add(ToIssue("negativeFixture.execution", fixture.Type, "breaking", ex.Message));
        }

        result.ActualCodes = result.Issues.Select(issue => issue.Id).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        result.ActualSeverities = result.Issues.Select(issue => issue.Severity).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        result.ActualFixHintIds = result.Issues.SelectMany(issue => issue.FixHints.Select(hint => hint.HintId)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        result.ActualExitCode = result.Issues.Any(issue => issue.Severity == "breaking") ? 2 : 0;
        if (result.ActualExitCode != result.ExpectedExitCode)
        {
            result.Errors.Add($"exitCode:{result.ActualExitCode}!={result.ExpectedExitCode}");
        }

        foreach (var code in result.ExpectedCodes)
        {
            if (!result.ActualCodes.Contains(code, StringComparer.Ordinal))
            {
                result.Errors.Add($"missingCode:{code}");
            }
        }

        if (!result.ActualSeverities.Contains(result.ExpectedSeverity, StringComparer.Ordinal))
        {
            result.Errors.Add($"missingSeverity:{result.ExpectedSeverity}");
        }

        foreach (var hint in result.ExpectedFixHintIds)
        {
            if (!result.ActualFixHintIds.Contains(hint, StringComparer.Ordinal))
            {
                result.Errors.Add($"missingFixHint:{hint}");
            }
        }

        if (fixture.ExpectedExitCode != 0 && result.ActualExitCode == 0)
        {
            result.Errors.Add("negativeFixture.unexpectedPass");
        }

        result.Passed = result.Errors.Count == 0;
        result.Errors = result.Errors.Order(StringComparer.Ordinal).ToList();
        return result;
    }

    private List<DiagnosticIssue> RunRequirement(string path)
    {
        var validation = new RequirementCaptureValidator().Validate(JsonSerializer.Deserialize<RequirementCapture>(File.ReadAllText(path), ThesisJson.Options)!);
        return validation.Errors.Select(error => ToIssue(CanonicalCode(error.Code), "requirements", "breaking", error.Message, error.Path))
            .Concat(validation.Warnings.Select(warning => ToIssue(CanonicalCode(warning.Code), "requirements", "warning", warning.Message, warning.Path)))
            .ToList();
    }

    private List<DiagnosticIssue> RunTemplate(string path)
    {
        var schema = LocateSchema("template-package.schema.json");
        var validation = new TemplateValidationService().Validate(path, schema);
        return validation.Errors.Select(error => ToIssue(CanonicalCode(error.Code), "template", "breaking", error.Message, error.Path))
            .Concat(validation.Warnings.Select(warning => ToIssue(CanonicalCode(warning.Code), "template", "warning", warning.Message, warning.Path)))
            .ToList();
    }

    private List<DiagnosticIssue> RunDocument(string path)
    {
        var document = JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(path), ThesisJson.Options)!;
        var format = JsonSerializer.Deserialize<ThesisFormatSpec>(File.ReadAllText(Path.Combine(LocateRepoRoot(), "examples", "format-specs", "strict-cn-thesis.json")), ThesisJson.Options)!;
        var validation = new ThesisInputValidator().Validate(document, format, Path.GetDirectoryName(path));
        return validation.Errors.Select(error => ToIssue(CanonicalCode(error.Code), ClassifyDocument(error.Code), "breaking", error.Message, error.Path))
            .Concat(validation.Warnings.Select(warning => ToIssue(CanonicalCode(warning.Code), ClassifyDocument(warning.Code), "warning", warning.Message, warning.Path)))
            .ToList();
    }

    private List<DiagnosticIssue> RunMetadata(string path)
    {
        var metadata = JsonSerializer.Deserialize<NegativeFixtureMetadata>(File.ReadAllText(Path.Combine(path, "metadata.json")), ThesisJson.Options)
            ?? new NegativeFixtureMetadata();
        return metadata.ActualCodes
            .Select(code => ToIssue(code, metadata.Category, metadata.Severity, metadata.Message, metadata.Path))
            .ToList();
    }

    private DiagnosticIssue ToIssue(string code, string category, string severity, string message, string? path = null)
    {
        var issue = new DiagnosticIssue
        {
            Id = code,
            Source = "NegativeFixture",
            Category = category,
            Severity = severity,
            Title = code,
            Message = message,
            Path = path
        };
        issue.FixHints = _fixHints.Suggest(issue).ToList();
        return issue;
    }

    private static string CanonicalCode(string code)
    {
        return code switch
        {
            "requirements.item.evidenceMissing" => "RequirementMissingEvidence",
            "requirements.item.approvedUnmapped" => "RequirementUnmappedApproved",
            "requirements.item.lowConfidenceApproved" => "RequirementLowConfidenceApproved",
            "template.asset.missing" => "TemplateMissingAsset",
            "template.variable.requiredMissing" => "TemplateMissingRequiredVariable",
            "template.inheritance.circular" => "TemplateCircularInheritance",
            "template.inheritance.parentMissing" => "TemplateCircularInheritance",
            "template.schemaVersion.unsupported" => "SchemaInvalidVersion",
            "dangling.reference" => "CrossReferenceTargetMissing",
            "dangling.citation" => "CitationBibliographyKeyMissing",
            "heading.levelJump" => "HeadingLevelJump",
            _ => code
        };
    }

    private static string ClassifyDocument(string code)
    {
        if (code.Contains("citation", StringComparison.OrdinalIgnoreCase)) return "citation";
        if (code.Contains("reference", StringComparison.OrdinalIgnoreCase)) return "crossReference";
        if (code.Contains("heading", StringComparison.OrdinalIgnoreCase)) return "heading";
        return "document";
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static string LocateSchema(string name) => Path.Combine(LocateRepoRoot(), "schemas", name);

    private static string LocateRepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ThesisDocx.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class NegativeFixtureMetadata
    {
        public List<string> ActualCodes { get; set; } = [];

        public string Severity { get; set; } = "breaking";

        public string Category { get; set; } = "baseline";

        public string Message { get; set; } = "Expected negative fixture failure.";

        public string? Path { get; set; }
    }
}
