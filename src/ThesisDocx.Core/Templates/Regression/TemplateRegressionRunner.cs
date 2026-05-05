using System.Text.Json;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Diff.Layout;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;

namespace ThesisDocx.Core.Templates.Regression;

public sealed class TemplateRegressionRunner
{
    public TemplateRegressionResult Run(string suitePath, string? outputDirectory = null)
    {
        var suite = JsonSerializer.Deserialize<TemplateRegressionSuite>(File.ReadAllText(suitePath), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not read regression suite '{suitePath}'.");
        var suiteDirectory = Path.GetDirectoryName(Path.GetFullPath(suitePath)) ?? Directory.GetCurrentDirectory();
        outputDirectory ??= Path.Combine(Path.GetTempPath(), "ThesisDocx.TemplateRegression", Path.GetFileNameWithoutExtension(suitePath));
        Directory.CreateDirectory(outputDirectory);

        var result = new TemplateRegressionResult { SuiteName = suite.Name };
        foreach (var regressionCase in suite.Cases.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            result.Cases.Add(RunCase(regressionCase, suiteDirectory, outputDirectory));
        }

        PopulateDiagnostics(result);
        return result;
    }

    private static TemplateRegressionCaseResult RunCase(TemplateRegressionCase regressionCase, string suiteDirectory, string outputDirectory)
    {
        var result = new TemplateRegressionCaseResult { Id = regressionCase.Id };
        try
        {
            var documentPath = ResolvePath(suiteDirectory, regressionCase.DocumentPath);
            var document = ReadJson<ThesisDocument>(documentPath);
            ThesisFormatSpec format;
            DocxRenderContext? context = null;
            string? formatPath = null;
            if (!string.IsNullOrWhiteSpace(regressionCase.TemplatePath))
            {
                var templatePath = ResolvePath(suiteDirectory, regressionCase.TemplatePath);
                var resolution = new TemplateResolver().Resolve(templatePath, document, regressionCase.Variables);
                if (!resolution.IsValid)
                {
                    result.Errors.AddRange(resolution.Errors.Select(e => e.ToString()));
                    return result;
                }

                format = resolution.FormatSpec ?? new ThesisFormatSpec();
                formatPath = WriteTempFormatSpec(format, outputDirectory, regressionCase.Id);
                context = CreateRenderContext(resolution);
            }
            else
            {
                formatPath = ResolvePath(suiteDirectory, regressionCase.FormatPath ?? throw new InvalidOperationException("Regression case requires formatPath or templatePath."));
                format = ReadJson<ThesisFormatSpec>(formatPath);
            }

            MergeInputValidation(result, new ThesisInputValidator().Validate(document, format, Path.GetDirectoryName(documentPath)));
            ResolveRelativeImagePaths(document, Path.GetDirectoryName(documentPath)!);

            var docxPath = Path.Combine(outputDirectory, $"{regressionCase.Id}.docx");
            new DocxRenderer().Render(document, format, docxPath, context);
            result.Artifacts["docx"] = docxPath;

            var openXml = new OpenXmlPackageValidator().Validate(docxPath);
            result.OpenXmlErrorCount = openXml.Errors.Count;
            result.Errors.AddRange(openXml.Errors.Select(e => e.ToString()));

            var formatResult = !string.IsNullOrWhiteSpace(regressionCase.TemplatePath)
                ? new FormatConformanceValidator().Validate(docxPath, ResolvePath(suiteDirectory, regressionCase.TemplatePath!))
                : new FormatConformanceValidator().Validate(docxPath, format);
            result.FormatConformanceValid = formatResult.IsValid;
            result.Errors.AddRange(formatResult.Errors.Select(e => e.ToString()));

            var inspect = new DocxInspector().Inspect(docxPath);
            var inspectPath = Path.Combine(outputDirectory, $"{regressionCase.Id}.inspect.json");
            File.WriteAllText(inspectPath, JsonSerializer.Serialize(inspect, ThesisJson.Options));
            result.Artifacts["inspect"] = inspectPath;

            var signature = new DocxLayoutSignatureExtractor().Extract(docxPath);
            var signaturePath = Path.Combine(outputDirectory, $"{regressionCase.Id}.layout.json");
            File.WriteAllText(signaturePath, JsonSerializer.Serialize(signature, ThesisJson.Options));
            result.Artifacts["layoutSignature"] = signaturePath;

            CompareLayout(regressionCase, suiteDirectory, result, signature);

            var snapshot = new DocxSnapshotNormalizer().NormalizeToStableSnapshot(docxPath);
            var snapshotPath = Path.Combine(outputDirectory, $"{regressionCase.Id}.snapshot.txt");
            File.WriteAllText(snapshotPath, snapshot);
            result.Artifacts["snapshot"] = snapshotPath;
            CompareSnapshot(regressionCase, suiteDirectory, result, snapshot);

            CheckRequiredCustomProperties(regressionCase, result, signature);
            CheckRequiredParts(regressionCase, result, inspect);
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
        }

        result.Errors = result.Errors.Order(StringComparer.Ordinal).ToList();
        result.Warnings = result.Warnings.Order(StringComparer.Ordinal).ToList();
        result.Artifacts = result.Artifacts.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return result;
    }

    private static void CompareLayout(TemplateRegressionCase regressionCase, string suiteDirectory, TemplateRegressionCaseResult result, DocxLayoutSignature signature)
    {
        if (string.IsNullOrWhiteSpace(regressionCase.BaselineLayoutPath))
        {
            result.Warnings.Add("layout.baseline.missing");
            return;
        }

        var baselinePath = ResolvePath(suiteDirectory, regressionCase.BaselineLayoutPath);
        if (!File.Exists(baselinePath))
        {
            result.Warnings.Add($"layout.baseline.notFound:{regressionCase.BaselineLayoutPath}");
            return;
        }

        var baseline = ReadJson<DocxLayoutSignature>(baselinePath);
        var comparison = new LayoutSignatureComparer().Compare(baseline, signature, regressionCase.MinLayoutSimilarity);
        result.LayoutSimilarity = comparison.SimilarityScore;
        if (!comparison.MeetsThreshold)
        {
            result.Errors.Add($"layout.similarityBelowThreshold:{comparison.SimilarityScore}<={regressionCase.MinLayoutSimilarity}");
        }
    }

    private static void CompareSnapshot(TemplateRegressionCase regressionCase, string suiteDirectory, TemplateRegressionCaseResult result, string snapshot)
    {
        if (string.IsNullOrWhiteSpace(regressionCase.BaselineSnapshotPath))
        {
            result.Warnings.Add("snapshot.baseline.missing");
            return;
        }

        var baselinePath = ResolvePath(suiteDirectory, regressionCase.BaselineSnapshotPath);
        if (!File.Exists(baselinePath))
        {
            result.Warnings.Add($"snapshot.baseline.notFound:{regressionCase.BaselineSnapshotPath}");
            return;
        }

        var baseline = File.ReadAllText(baselinePath);
        result.SnapshotMatches = string.Equals(baseline, snapshot, StringComparison.Ordinal);
        if (!result.SnapshotMatches)
        {
            result.Errors.Add("snapshot.mismatch");
        }
    }

    private static void CheckRequiredCustomProperties(TemplateRegressionCase regressionCase, TemplateRegressionCaseResult result, DocxLayoutSignature signature)
    {
        foreach (var property in regressionCase.RequiredCustomProperties.Order(StringComparer.Ordinal))
        {
            if (!signature.CustomProperties.ContainsKey(property))
            {
                result.RequiredCustomPropertiesPassed = false;
                result.Errors.Add($"customProperty.missing:{property}");
            }
        }
    }

    private static void CheckRequiredParts(TemplateRegressionCase regressionCase, TemplateRegressionCaseResult result, DocxInspectionResult inspect)
    {
        foreach (var part in regressionCase.RequiredParts.Order(StringComparer.Ordinal))
        {
            if (!inspect.PackageParts.Contains(part, StringComparer.Ordinal))
            {
                result.RequiredPartsPassed = false;
                result.Errors.Add($"part.missing:{part}");
            }
        }
    }

    private static void MergeInputValidation(TemplateRegressionCaseResult result, ThesisInputValidationResult validation)
    {
        result.Errors.AddRange(validation.Errors.Select(e => e.ToString()));
        result.Warnings.AddRange(validation.Warnings.Select(e => e.ToString()));
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

    private static string WriteTempFormatSpec(ThesisFormatSpec format, string outputDirectory, string caseId)
    {
        var path = Path.Combine(outputDirectory, $"{caseId}.resolved-format-spec.json");
        File.WriteAllText(path, JsonSerializer.Serialize(format, ThesisJson.Options));
        return path;
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static T ReadJson<T>(string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize {path}.");
    }

    private static void PopulateDiagnostics(TemplateRegressionResult result)
    {
        var fixHints = new FixHintEngine();
        result.FailedCases = result.Cases
            .Where(regressionCase => !regressionCase.Passed)
            .Select(regressionCase => regressionCase.Id)
            .Order(StringComparer.Ordinal)
            .ToList();
        result.BaselineSummary = new TemplateRegressionBaselineSummary
        {
            TotalCases = result.Cases.Count,
            ComparedCases = result.Cases.Count(c => c.LayoutSimilarity < 1.0 || c.SnapshotMatches),
            SnapshotMatches = result.Cases.Count(c => c.SnapshotMatches),
            LayoutBelowThreshold = result.Cases.Count(c => c.Errors.Any(error => error.StartsWith("layout.similarityBelowThreshold", StringComparison.Ordinal)))
        };
        foreach (var regressionCase in result.Cases.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            foreach (var error in regressionCase.Errors.Order(StringComparer.Ordinal))
            {
                var issue = new DiagnosticIssue
                {
                    Id = $"regression.{regressionCase.Id}.{NormalizeCode(error)}",
                    Source = "TemplateRegression",
                    Category = Classify(error),
                    Severity = "breaking",
                    Title = $"Regression case {regressionCase.Id} failed",
                    Message = error,
                    FixtureId = regressionCase.Id,
                    Evidence = [new DiagnosticEvidence { Kind = "case", Value = regressionCase.Id }]
                };
                issue.FixHints = fixHints.Suggest(issue).ToList();
                result.CaseDiagnostics.Add(issue);
            }
        }

        result.CaseDiagnostics = result.CaseDiagnostics
            .OrderBy(issue => issue.Id, StringComparer.Ordinal)
            .ToList();
        result.NextActions = result.CaseDiagnostics.Count == 0
            ? ["Regression passed; run template gate and authoring-report before publishing."]
            : result.CaseDiagnostics
                .SelectMany(issue => issue.FixHints.Select(hint => hint.SuggestedAction))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Take(8)
                .ToList();
    }

    private static string Classify(string error)
    {
        if (error.Contains("layout", StringComparison.OrdinalIgnoreCase)) return "layout";
        if (error.Contains("snapshot", StringComparison.OrdinalIgnoreCase)) return "baseline";
        if (error.Contains("customProperty", StringComparison.OrdinalIgnoreCase)) return "customProperties";
        if (error.Contains("part.", StringComparison.OrdinalIgnoreCase)) return "packagePart";
        if (error.Contains("table", StringComparison.OrdinalIgnoreCase)) return "table";
        if (error.Contains("heading", StringComparison.OrdinalIgnoreCase)) return "heading";
        return "regression";
    }

    private static string NormalizeCode(string error)
    {
        var code = new string(error.Select(ch => char.IsLetterOrDigit(ch) ? ch : '.').ToArray()).Trim('.');
        return string.IsNullOrWhiteSpace(code) ? "issue" : code;
    }
}
