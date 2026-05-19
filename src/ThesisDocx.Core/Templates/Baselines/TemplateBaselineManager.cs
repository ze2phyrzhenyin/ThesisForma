using System.Text.Json;
using ThesisDocx.Core.Diff.Layout;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Templates.Regression;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;

namespace ThesisDocx.Core.Templates.Baselines;

public sealed class TemplateBaselineManager
{
    public TemplateBaselineManifest LoadManifest(string manifestPath)
    {
        var manifest = JsonSerializer.Deserialize<TemplateBaselineManifest>(File.ReadAllText(manifestPath), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not read baseline manifest '{manifestPath}'.");
        foreach (var entry in manifest.Baselines)
        {
            if (Path.IsPathRooted(entry.SnapshotPath) || Path.IsPathRooted(entry.LayoutSignaturePath) || Path.IsPathRooted(entry.InspectPath))
            {
                throw new InvalidDataException($"Baseline '{entry.CaseId}' uses an absolute artifact path.");
            }
        }

        return manifest;
    }

    public IReadOnlyList<TemplateBaselineEntry> List(string suitePath)
    {
        var manifestPath = LocateManifestForSuite(suitePath);
        return File.Exists(manifestPath)
            ? LoadManifest(manifestPath).Baselines.OrderBy(b => b.CaseId, StringComparer.Ordinal).ToList()
            : [];
    }

    public TemplateBaselineManifest Init(string suitePath, string manifestPath)
    {
        var suite = ReadJson<TemplateRegressionSuite>(suitePath);
        var suiteDirectory = Path.GetDirectoryName(Path.GetFullPath(suitePath))!;
        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        Directory.CreateDirectory(manifestDirectory);
        var manifest = File.Exists(manifestPath)
            ? LoadManifest(manifestPath)
            : new TemplateBaselineManifest { SchemaVersion = "1.0.0", SuiteId = suite.Name, GeneratedAt = StableNow() };

        foreach (var regressionCase in suite.Cases.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (manifest.Baselines.Any(b => b.CaseId == regressionCase.Id))
            {
                continue;
            }

            var entry = CreateEntry(regressionCase, suiteDirectory, manifestDirectory);
            manifest.Baselines.Add(entry);
        }

        manifest.Baselines = manifest.Baselines.OrderBy(b => b.CaseId, StringComparer.Ordinal).ToList();
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ThesisJson.Options));
        return manifest;
    }

    public TemplateBaselineCompareResult CompareSuite(string suitePath, string? outputDirectory = null)
    {
        var suite = ReadJson<TemplateRegressionSuite>(suitePath);
        var suiteDirectory = Path.GetDirectoryName(Path.GetFullPath(suitePath))!;
        var manifestPath = LocateManifestForSuite(suitePath);
        var manifest = File.Exists(manifestPath)
            ? LoadManifest(manifestPath)
            : Init(suitePath, manifestPath);
        outputDirectory ??= Path.Combine(Path.GetTempPath(), "ThesisDocx.BaselineCompare", Path.GetFileNameWithoutExtension(suitePath));
        Directory.CreateDirectory(outputDirectory);

        var result = new TemplateBaselineCompareResult { SuiteId = manifest.SuiteId };
        foreach (var regressionCase in suite.Cases.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var entry = manifest.Baselines.FirstOrDefault(b => b.CaseId == regressionCase.Id);
            if (entry is null)
            {
                result.Cases.Add(new TemplateBaselineCaseCompareResult { CaseId = regressionCase.Id, Errors = ["baseline.entryMissing"] });
                continue;
            }

            result.Cases.Add(CompareCase(regressionCase, entry, suiteDirectory, Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, outputDirectory));
        }

        return result;
    }

    public TemplateBaselineCompareResult CompareFixtures(string fixturesDirectory, string? outputDirectory = null)
    {
        var manifestPath = Path.Combine(fixturesDirectory, "baselines", "format-fixture-baseline-manifest.json");
        if (!File.Exists(manifestPath))
        {
            InitFixtures(fixturesDirectory, manifestPath);
        }

        var manifest = LoadManifest(manifestPath);
        outputDirectory ??= Path.Combine(Path.GetTempPath(), "ThesisDocx.FixtureBaselineCompare");
        Directory.CreateDirectory(outputDirectory);
        var result = new TemplateBaselineCompareResult { SuiteId = manifest.SuiteId };
        foreach (var entry in manifest.Baselines.OrderBy(e => e.CaseId, StringComparer.Ordinal))
        {
            var fixturePath = Path.Combine(fixturesDirectory, entry.CaseId);
            result.Cases.Add(CompareFixture(entry, fixturePath, Path.GetDirectoryName(manifestPath)!, outputDirectory));
        }

        return result;
    }

    public TemplateBaselineUpdateResult Update(string suitePath, string caseId, string reason, string? outputDirectory = null)
    {
        var result = new TemplateBaselineUpdateResult { CaseId = caseId, Reason = reason };
        if (string.IsNullOrWhiteSpace(reason))
        {
            result.Errors.Add("baseline.update.reasonRequired");
            return result;
        }

        var suite = ReadJson<TemplateRegressionSuite>(suitePath);
        var regressionCase = suite.Cases.FirstOrDefault(c => c.Id == caseId);
        if (regressionCase is null)
        {
            result.Errors.Add($"baseline.case.missing:{caseId}");
            return result;
        }

        var suiteDirectory = Path.GetDirectoryName(Path.GetFullPath(suitePath))!;
        var manifestPath = LocateManifestForSuite(suitePath);
        var manifest = File.Exists(manifestPath) ? LoadManifest(manifestPath) : Init(suitePath, manifestPath);
        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var entry = manifest.Baselines.FirstOrDefault(b => b.CaseId == caseId) ?? CreateEntry(regressionCase, suiteDirectory, manifestDirectory);
        RenderCaseArtifacts(regressionCase, suiteDirectory, manifestDirectory, entry);
        entry.LastUpdatedAt = StableNow();
        entry.Notes = string.IsNullOrWhiteSpace(entry.Notes) ? reason : $"{entry.Notes}; {reason}";
        manifest.Baselines.RemoveAll(b => b.CaseId == caseId);
        manifest.Baselines.Add(entry);
        manifest.Baselines = manifest.Baselines.OrderBy(b => b.CaseId, StringComparer.Ordinal).ToList();
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ThesisJson.Options));
        result.Updated = true;
        result.UpdatedFiles.AddRange([entry.LayoutSignaturePath, entry.SnapshotPath, entry.InspectPath, Path.GetRelativePath(manifestDirectory, manifestPath)]);
        result.Changes.Add($"Updated baseline '{caseId}' with reason '{reason}'.");
        return result;
    }

    public TemplateBaselineManifest InitFixtures(string fixturesDirectory, string manifestPath)
    {
        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        Directory.CreateDirectory(manifestDirectory);
        var manifest = new TemplateBaselineManifest { SchemaVersion = "1.0.0", SuiteId = "format-fixtures", GeneratedAt = StableNow() };
        foreach (var fixture in Directory.EnumerateDirectories(fixturesDirectory).Where(d => Path.GetFileName(d) != "baselines").Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileName(fixture);
            var entry = new TemplateBaselineEntry
            {
                CaseId = id,
                TemplateId = File.Exists(Path.Combine(fixture, "template.json")) ? id : "format-fixture",
                TemplateVersion = "1.0.0",
                DocumentId = id,
                SnapshotPath = $"{id}.snapshot.txt",
                LayoutSignaturePath = $"{id}.layout.json",
                InspectPath = $"{id}.inspect.json",
                LayoutThreshold = 0.99,
                LastUpdatedAt = StableNow(),
                Notes = "Format fixture baseline."
            };
            RenderFixtureArtifacts(fixture, manifestDirectory, entry);
            manifest.Baselines.Add(entry);
        }

        manifest.Baselines = manifest.Baselines.OrderBy(b => b.CaseId, StringComparer.Ordinal).ToList();
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ThesisJson.Options));
        return manifest;
    }

    private TemplateBaselineCaseCompareResult CompareCase(TemplateRegressionCase regressionCase, TemplateBaselineEntry entry, string suiteDirectory, string manifestDirectory, string outputDirectory)
    {
        var caseOutput = Path.Combine(outputDirectory, entry.CaseId);
        Directory.CreateDirectory(caseOutput);
        var rendered = RenderRegressionCase(regressionCase, suiteDirectory, caseOutput);
        return CompareArtifacts(entry, manifestDirectory, rendered, entry.CaseId, null);
    }

    private TemplateBaselineCaseCompareResult CompareFixture(TemplateBaselineEntry entry, string fixturePath, string manifestDirectory, string outputDirectory)
    {
        var caseOutput = Path.Combine(outputDirectory, entry.CaseId);
        Directory.CreateDirectory(caseOutput);
        var rendered = RenderFixture(fixturePath, caseOutput, entry.CaseId);
        return CompareArtifacts(entry, manifestDirectory, rendered, entry.CaseId, entry.CaseId);
    }

    private static TemplateBaselineCaseCompareResult CompareArtifacts(TemplateBaselineEntry entry, string manifestDirectory, RenderedBaselineArtifacts rendered, string caseId, string? fixtureId)
    {
        var result = new TemplateBaselineCaseCompareResult { CaseId = caseId, FixtureId = fixtureId, Artifacts = rendered.Artifacts };
        var baselineLayout = ReadJson<DocxLayoutSignature>(Path.Combine(manifestDirectory, entry.LayoutSignaturePath));
        var comparison = new LayoutSignatureComparer().Compare(baselineLayout, rendered.LayoutSignature, entry.LayoutThreshold);
        result.LayoutSimilarity = comparison.SimilarityScore;
        result.Diffs.AddRange(comparison.Differences.Select(d => new BaselineDiffSummary
        {
            Category = d.Category,
            Path = d.Path,
            Severity = d.Severity.ToString(),
            Expected = d.BaseValue,
            Actual = d.TargetValue
        }));

        if (!comparison.MeetsThreshold)
        {
            result.Errors.Add($"baseline.layoutBelowThreshold:{comparison.SimilarityScore}<={entry.LayoutThreshold}");
        }

        var baselineSnapshot = File.ReadAllText(Path.Combine(manifestDirectory, entry.SnapshotPath));
        result.SnapshotMatches = string.Equals(baselineSnapshot, rendered.Snapshot, StringComparison.Ordinal);
        if (!result.SnapshotMatches)
        {
            result.Errors.Add("baseline.snapshotMismatch");
        }

        result.Errors = result.Errors.Order(StringComparer.Ordinal).ToList();
        result.Diffs = result.Diffs.OrderBy(d => d.Path, StringComparer.Ordinal).ToList();
        return result;
    }

    private static TemplateBaselineEntry CreateEntry(TemplateRegressionCase regressionCase, string suiteDirectory, string manifestDirectory)
    {
        var entry = new TemplateBaselineEntry
        {
            CaseId = regressionCase.Id,
            TemplateId = Path.GetFileName(regressionCase.TemplatePath ?? "format-spec"),
            TemplateVersion = "1.0.0",
            DocumentId = Path.GetFileNameWithoutExtension(regressionCase.DocumentPath),
            SnapshotPath = $"{regressionCase.Id}.snapshot.txt",
            LayoutSignaturePath = $"{regressionCase.Id}.layout.json",
            InspectPath = $"{regressionCase.Id}.inspect.json",
            LayoutThreshold = regressionCase.MinLayoutSimilarity,
            LastUpdatedAt = StableNow(),
            Notes = "Initialized baseline."
        };
        RenderCaseArtifacts(regressionCase, suiteDirectory, manifestDirectory, entry);
        return entry;
    }

    private static void RenderCaseArtifacts(TemplateRegressionCase regressionCase, string suiteDirectory, string manifestDirectory, TemplateBaselineEntry entry)
    {
        var rendered = RenderRegressionCase(regressionCase, suiteDirectory, manifestDirectory);
        WriteArtifacts(manifestDirectory, entry, rendered);
        DeleteIntermediateDocx(rendered);
    }

    private static void RenderFixtureArtifacts(string fixturePath, string manifestDirectory, TemplateBaselineEntry entry)
    {
        var rendered = RenderFixture(fixturePath, manifestDirectory, entry.CaseId);
        WriteArtifacts(manifestDirectory, entry, rendered);
        DeleteIntermediateDocx(rendered);
    }

    private static void WriteArtifacts(string directory, TemplateBaselineEntry entry, RenderedBaselineArtifacts rendered)
    {
        File.WriteAllText(Path.Combine(directory, entry.LayoutSignaturePath), JsonSerializer.Serialize(rendered.LayoutSignature, ThesisJson.Options));
        File.WriteAllText(Path.Combine(directory, entry.SnapshotPath), rendered.Snapshot);
        File.WriteAllText(Path.Combine(directory, entry.InspectPath), JsonSerializer.Serialize(rendered.Inspect, ThesisJson.Options));
    }

    private static void DeleteIntermediateDocx(RenderedBaselineArtifacts rendered)
    {
        if (File.Exists(rendered.DocxPath))
        {
            File.Delete(rendered.DocxPath);
        }
    }

    private static RenderedBaselineArtifacts RenderRegressionCase(TemplateRegressionCase regressionCase, string suiteDirectory, string outputDirectory)
    {
        var documentPath = ResolvePath(suiteDirectory, regressionCase.DocumentPath);
        var document = ReadJson<ThesisDocument>(documentPath);
        ResolveRelativeImagePaths(document, Path.GetDirectoryName(documentPath)!);
        ThesisFormatSpec format;
        DocxRenderContext? context = null;
        if (!string.IsNullOrWhiteSpace(regressionCase.TemplatePath))
        {
            var resolution = new TemplateResolver().Resolve(ResolvePath(suiteDirectory, regressionCase.TemplatePath), document, regressionCase.Variables);
            format = resolution.FormatSpec ?? new ThesisFormatSpec();
            context = CreateRenderContext(resolution);
        }
        else
        {
            format = ReadJson<ThesisFormatSpec>(ResolvePath(suiteDirectory, regressionCase.FormatPath!));
        }

        return Render(document, format, context, outputDirectory, regressionCase.Id);
    }

    private static RenderedBaselineArtifacts RenderFixture(string fixturePath, string outputDirectory, string id)
    {
        var documentPath = Path.Combine(fixturePath, "document.json");
        var document = ReadJson<ThesisDocument>(documentPath);
        ResolveRelativeImagePaths(document, Path.GetDirectoryName(documentPath)!);
        ThesisFormatSpec format;
        DocxRenderContext? context = null;
        if (File.Exists(Path.Combine(fixturePath, "template.json")))
        {
            var resolution = new TemplateResolver().Resolve(fixturePath, document, new Dictionary<string, string> { ["variables.defenseDate"] = "2026-06-01" });
            format = resolution.FormatSpec ?? new ThesisFormatSpec();
            context = CreateRenderContext(resolution);
        }
        else
        {
            format = ReadJson<ThesisFormatSpec>(Path.Combine(fixturePath, "format-spec.json"));
        }

        return Render(document, format, context, outputDirectory, id);
    }

    private static RenderedBaselineArtifacts Render(ThesisDocument document, ThesisFormatSpec format, DocxRenderContext? context, string outputDirectory, string id)
    {
        Directory.CreateDirectory(outputDirectory);
        var docxPath = Path.Combine(outputDirectory, $"{id}.docx");
        new DocxRenderer().Render(document, format, docxPath, context);
        var signature = new DocxLayoutSignatureExtractor().Extract(docxPath);
        return new RenderedBaselineArtifacts(
            docxPath,
            signature,
            new DocxSnapshotNormalizer().NormalizeToStableSnapshot(docxPath),
            new DocxInspector().Inspect(docxPath),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["docx"] = docxPath
            });
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
            Assets = resolution.Assets.ToDictionary(a => a.Id, StringComparer.Ordinal),
            Overrides = resolution.Template?.DocumentOverrides
        };
    }

    private static string LocateManifestForSuite(string suitePath)
    {
        var suiteDirectory = Path.GetDirectoryName(Path.GetFullPath(suitePath))!;
        return Path.Combine(suiteDirectory, "baselines", "baseline-manifest.json");
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

    private static string ResolvePath(string baseDirectory, string path)
    {
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static T ReadJson<T>(string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize {path}.");
    }

    private static string StableNow() => "2000-01-01T00:00:00Z";

    private sealed record RenderedBaselineArtifacts(
        string DocxPath,
        DocxLayoutSignature LayoutSignature,
        string Snapshot,
        DocxInspectionResult Inspect,
        Dictionary<string, string> Artifacts);
}
