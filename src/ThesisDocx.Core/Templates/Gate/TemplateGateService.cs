using System.Text.Json;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Diff.Layout;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Core.Validation.FormatRuleCoverage;
using ThesisDocx.Core.Versioning;

namespace ThesisDocx.Core.Templates.Gate;

public sealed class TemplateGateService
{
    public TemplateGateReport Run(TemplateGateOptions options)
    {
        var outputDirectory = options.OutputDirectory ?? Path.Combine(Path.GetTempPath(), "ThesisDocx.TemplateGate", Path.GetFileName(options.TemplatePath));
        Directory.CreateDirectory(outputDirectory);
        var report = new TemplateGateReport();

        var document = ReadJson<ThesisDocument>(options.DocumentPath);
        report.VersionReport.MergeFrom(SchemaVersionReport.ForDocument(document.SchemaVersion));
        var templateValidation = new TemplateValidationService().Validate(options.TemplatePath, LocateSchema("template-package.schema.json"));
        report.VersionReport.MergeFrom(templateValidation.VersionReport);
        AddCheck(report, "template.validate", "Template validation", templateValidation.IsValid, string.Join("; ", templateValidation.Errors.Select(e => e.ToString())));

        var resolution = new TemplateResolver().Resolve(options.TemplatePath, document);
        report.TemplateId = resolution.Template?.Id ?? string.Empty;
        AddCheck(report, "template.resolve", "Template resolution", resolution.IsValid, string.Join("; ", resolution.Errors.Select(e => e.ToString())));
        var format = resolution.FormatSpec ?? new ThesisFormatSpec();
        var formatPath = Path.Combine(outputDirectory, "resolved-format-spec.json");
        File.WriteAllText(formatPath, JsonSerializer.Serialize(format, ThesisJson.Options));
        report.Artifacts["resolvedFormatSpec"] = formatPath;

        var formatSchema = new ThesisSchemaValidator().ValidateFormatFile(formatPath, LocateSchema("thesis-format-spec.schema.json"));
        report.VersionReport.MergeFrom(formatSchema.VersionReport);
        AddCheck(report, "format.schema", "Resolved format spec schema", formatSchema.IsValid, string.Join("; ", formatSchema.Errors.Select(e => e.ToString())));

        var input = new ThesisInputValidator().Validate(document, format, Path.GetDirectoryName(Path.GetFullPath(options.DocumentPath)));
        report.VersionReport.MergeFrom(input.VersionReport);
        AddCheck(
            report,
            "schema.version",
            "Schema version support",
            report.VersionReport.IsValid,
            report.VersionReport.IsValid
                ? "document, template, and format schema versions are supported"
                : string.Join("; ", report.VersionReport.Diagnostics.Select(d => $"{d.Code}:{d.Path}")));
        AddCheck(report, "input.validate", "Document input validation", input.IsValid, string.Join("; ", input.Errors.Select(e => e.ToString())));

        ResolveRelativeImagePaths(document, Path.GetDirectoryName(Path.GetFullPath(options.DocumentPath))!);
        var context = CreateRenderContext(resolution);
        var docxPath = Path.Combine(outputDirectory, "template-gate.docx");
        try
        {
            new DocxRenderer().Render(document, format, docxPath, context);
            report.Artifacts["docx"] = docxPath;
            AddCheck(report, "render", "DOCX render", true, "rendered");
        }
        catch (Exception ex)
        {
            AddCheck(report, "render", "DOCX render", false, ex.Message);
        }

        if (File.Exists(docxPath))
        {
            var openXml = new OpenXmlPackageValidator().Validate(docxPath);
            AddCheck(report, "openxml", "OpenXmlValidator", openXml.Errors.Count == 0, string.Join("; ", openXml.Errors.Select(e => e.ToString())));

            var conformance = new FormatConformanceValidator().Validate(docxPath, options.TemplatePath);
            AddCheck(report, "format.conformance", "Format conformance", conformance.IsValid, string.Join("; ", conformance.Errors.Select(e => e.ToString())));

            var inspect = new DocxInspector().Inspect(docxPath);
            var inspectPath = Path.Combine(outputDirectory, "template-gate.inspect.json");
            File.WriteAllText(inspectPath, JsonSerializer.Serialize(inspect, ThesisJson.Options));
            report.Artifacts["inspect"] = inspectPath;
            AddCheck(report, "customProperties", "Template custom properties", !string.IsNullOrWhiteSpace(inspect.TemplateRendering.TemplateId), "template id custom property");

            var signature = new DocxLayoutSignatureExtractor().Extract(docxPath);
            var signaturePath = Path.Combine(outputDirectory, "template-gate.layout.json");
            File.WriteAllText(signaturePath, JsonSerializer.Serialize(signature, ThesisJson.Options));
            report.Artifacts["layoutSignature"] = signaturePath;
            AddCheck(report, "layout.signature", "Layout signature", signature.Sections.Count > 0, "layout signature extracted");

            var snapshotPath = Path.Combine(outputDirectory, "template-gate.snapshot.txt");
            File.WriteAllText(snapshotPath, new DocxSnapshotNormalizer().NormalizeToStableSnapshot(docxPath));
            report.Artifacts["snapshot"] = snapshotPath;
            AddCheck(report, "snapshot", "Normalized snapshot", File.Exists(snapshotPath), "snapshot generated");
        }

        var coverage = new FormatRuleCoverageReporter().Build(options.TemplatePath);
        var supported = coverage.Rules.Count(rule => rule.Status == "supported");
        report.CoverageRatio = coverage.Rules.Count == 0 ? 0 : Math.Round(supported / (double)coverage.Rules.Count, 6);
        AddCheck(
            report,
            "coverage",
            "Coverage threshold",
            report.CoverageRatio >= options.CoverageThreshold,
            FormattableString.Invariant($"{report.CoverageRatio} >= {options.CoverageThreshold}"));

        var forbiddenAssets = resolution.Assets
            .Where(asset => options.ForbiddenAssetExtensions.Contains(Path.GetExtension(asset.Path), StringComparer.OrdinalIgnoreCase))
            .Select(asset => asset.Id)
            .Order(StringComparer.Ordinal)
            .ToList();
        AddCheck(report, "assets.forbidden", "Forbidden asset check", forbiddenAssets.Count == 0, string.Join(",", forbiddenAssets));

        AddCheck(report, "limitations", "Known limitations recorded", resolution.Template?.Notes.Count > 0, "template notes");

        report.Checks = report.Checks.OrderBy(check => check.Code, StringComparer.Ordinal).ToList();
        report.Artifacts = report.Artifacts.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        report.Status = report.Checks.Any(check => check.Status == TemplateGateCheckStatus.Fail)
            ? TemplateGateStatus.Fail
            : report.Checks.Any(check => check.Status == TemplateGateCheckStatus.Warning)
                ? TemplateGateStatus.PassWithWarnings
                : TemplateGateStatus.Pass;
        PopulateDiagnostics(report);
        return report;
    }

    private static void AddCheck(TemplateGateReport report, string code, string name, bool passed, string message)
    {
        report.Checks.Add(new TemplateGateCheck
        {
            Code = code,
            Name = name,
            Status = passed ? TemplateGateCheckStatus.Pass : TemplateGateCheckStatus.Fail,
            Message = string.IsNullOrWhiteSpace(message) ? (passed ? "passed" : "failed") : message
        });
    }

    private static void PopulateDiagnostics(TemplateGateReport report)
    {
        var fixHints = new FixHintEngine();
        report.ArtifactPaths = report.Artifacts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        report.Checklist = report.Checks
            .Select(check => new TemplateGateChecklistItem
            {
                Code = check.Code,
                Title = check.Name,
                Status = check.Status.ToString().ToLowerInvariant(),
                Message = check.Message
            })
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ToList();
        report.Diagnostics = report.Checks
            .Where(check => check.Status != TemplateGateCheckStatus.Pass)
            .Select(check =>
            {
                var issue = new DiagnosticIssue
                {
                    Id = $"gate.{check.Code}",
                    Source = "TemplateGate",
                    Category = Classify(check.Code),
                    Severity = check.Status == TemplateGateCheckStatus.Fail ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                    Title = check.Name,
                    Message = check.Message,
                    Evidence = [new DiagnosticEvidence { Kind = "check", Value = check.Code }]
                };
                issue.FixHints = fixHints.Suggest(issue).ToList();
                issue.RelatedDocs = issue.FixHints
                    .Select(hint => hint.DocsRef)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList();
                issue.RelatedFixtures = issue.FixHints
                    .Select(hint => hint.ExampleFixtureRef)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList();
                return issue;
            })
            .OrderBy(issue => issue.Id, StringComparer.Ordinal)
            .ToList();
        report.FixHints = report.Diagnostics
            .SelectMany(issue => issue.FixHints)
            .GroupBy(hint => hint.HintId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(hint => hint.HintId, StringComparer.Ordinal)
            .ToList();
        report.NextActions = report.Diagnostics.Count == 0
            ? ["Run template regression and authoring-report before publishing."]
            : report.Diagnostics
                .SelectMany(issue => issue.FixHints.Select(hint => hint.SuggestedAction))
                .Where(action => !string.IsNullOrWhiteSpace(action))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Take(8)
                .ToList();
    }

    private static string Classify(string code)
    {
        if (code.Contains("coverage", StringComparison.OrdinalIgnoreCase)) return "coverage";
        if (code.Contains("custom", StringComparison.OrdinalIgnoreCase)) return "customProperties";
        if (code.Contains("layout", StringComparison.OrdinalIgnoreCase)) return "layout";
        if (code.Contains("format", StringComparison.OrdinalIgnoreCase)) return "format";
        if (code.Contains("openxml", StringComparison.OrdinalIgnoreCase)) return "openXml";
        if (code.Contains("asset", StringComparison.OrdinalIgnoreCase)) return "asset";
        return "templateGate";
    }

    private static DocxRenderContext CreateRenderContext(TemplateResolutionResult resolution)
    {
        return new DocxRenderContext
        {
            TemplateId = resolution.Template?.Id,
            TemplateVersion = resolution.Template?.Version,
            TemplateSchool = resolution.Template?.School,
            TemplateCollege = resolution.Template?.College,
            ResolvedFormatSpecVersion = resolution.FormatSpec?.SchemaVersion,
            PageTemplates = resolution.PageTemplates,
            Variables = resolution.Variables.Where(v => v.Value is not null).ToDictionary(v => v.Name, v => v.Value!, StringComparer.Ordinal),
            Assets = resolution.Assets.ToDictionary(a => a.Id, StringComparer.Ordinal)
        };
    }

    private static string LocateSchema(string name)
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "schemas", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException($"Could not locate schema '{name}'.");
    }

    private static void ResolveRelativeImagePaths(ThesisDocument document, string baseDirectory)
    {
        foreach (var figure in document.Sections.SelectMany(s => s.Blocks).OfType<FigureBlock>())
        {
            if (!string.IsNullOrWhiteSpace(figure.ImagePath) && !Path.IsPathRooted(figure.ImagePath))
            {
                figure.ImagePath = Path.GetFullPath(Path.Combine(baseDirectory, figure.ImagePath));
            }
        }
    }

    private static T ReadJson<T>(string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize {path}.");
    }
}
