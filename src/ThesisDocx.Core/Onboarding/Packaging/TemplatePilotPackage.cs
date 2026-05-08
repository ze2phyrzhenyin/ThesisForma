using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Onboarding.Reports;
using ThesisDocx.Core.Privacy;
using ThesisDocx.Core.Requirements;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Onboarding.Packaging;

public sealed class TemplatePilotPackageBuilder
{
    private static readonly DateTimeOffset StableTimestamp = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<string> ForbiddenExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".wps", ".ttf", ".otf", ".ttc"
    };

    public TemplatePilotPackageBuildResult Build(string workspacePath, string outputZipPath)
    {
        var workspace = OnboardingWorkspaceInspector.Load(workspacePath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputZipPath)) ?? Directory.GetCurrentDirectory());

        var privacy = new PrivacyGuard().Scan(PrivacyGuardOptions.FromPolicy(workspacePath, workspace.Manifest.Privacy));
        if (!privacy.IsValid)
        {
            return new TemplatePilotPackageBuildResult
            {
                IsValid = false,
                Errors = privacy.Findings.Where(f => UnifiedDiagnosticMapper.IsError(f.Severity)).Select(f => f.Code).ToList(),
                PrivacyScan = privacy
            };
        }

        var staging = Path.Combine(Path.GetTempPath(), "ThesisDocx.Onboarding.Package", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            CopyTemplate(workspace, staging);
            WriteRequirements(workspace, staging);
            WriteReports(workspace, staging);
            CopyBaselines(workspace, staging);
            var manifest = BuildManifest(workspace, staging, privacy);
            File.WriteAllText(Path.Combine(staging, "manifest.json"), JsonSerializer.Serialize(manifest, ThesisJson.Options));
            WriteChecksums(staging);
            CreateZip(staging, outputZipPath);
            return new TemplatePilotPackageBuildResult
            {
                IsValid = true,
                PackagePath = outputZipPath,
                Manifest = manifest,
                PrivacyScan = privacy
            };
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    private static void CopyTemplate(ResolvedOnboardingWorkspace workspace, string staging)
    {
        CopyDirectory(workspace.TemplateDirectory, Path.Combine(staging, "template"));
    }

    private static void WriteRequirements(ResolvedOnboardingWorkspace workspace, string staging)
    {
        var target = Path.Combine(staging, "requirements");
        Directory.CreateDirectory(target);
        if (File.Exists(workspace.RequirementsFile))
        {
            var redacted = RedactionHelper.RedactRequirementCapture(JsonNode.Parse(File.ReadAllText(workspace.RequirementsFile))!);
            File.WriteAllText(Path.Combine(target, "requirements.redacted.json"), redacted.ToJsonString(ThesisJson.Options));
            var report = new RequirementMappingReporter().Build(new RequirementCaptureLoader().Load(workspace.RequirementsFile), workspace.TemplateDirectory);
            File.WriteAllText(Path.Combine(target, "mapping-report.json"), JsonSerializer.Serialize(report, ThesisJson.Options));
        }
    }

    private static void WriteReports(ResolvedOnboardingWorkspace workspace, string staging)
    {
        var reports = Path.Combine(staging, "reports");
        Directory.CreateDirectory(reports);
        var summary = new OnboardingReportBuilder().Build(new OnboardingReportOptions { WorkspacePath = workspace.Root });
        File.WriteAllText(Path.Combine(reports, "onboarding-summary.json"), JsonSerializer.Serialize(summary, ThesisJson.Options));
        foreach (var source in Directory.Exists(workspace.ReportsDirectory)
            ? Directory.EnumerateFiles(workspace.ReportsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            : [])
        {
            File.Copy(source, Path.Combine(reports, Path.GetFileName(source)), overwrite: true);
        }

        foreach (var required in new[] { "gate.json", "diagnostic.json", "authoring.json" })
        {
            var path = Path.Combine(reports, required);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, JsonSerializer.Serialize(new { status = "notGenerated", generatedBy = "TemplatePilotPackageBuilder" }, ThesisJson.Options));
            }
        }
    }

    private static void CopyBaselines(ResolvedOnboardingWorkspace workspace, string staging)
    {
        if (!Directory.Exists(workspace.BaselinesDirectory))
        {
            return;
        }

        CopyDirectory(workspace.BaselinesDirectory, Path.Combine(staging, "baselines"));
    }

    private static TemplatePilotPackageManifest BuildManifest(ResolvedOnboardingWorkspace workspace, string staging, PrivacyGuardResult privacy)
    {
        var template = File.Exists(Path.Combine(workspace.TemplateDirectory, "template.json"))
            ? JsonSerializer.Deserialize<TemplatePackage>(File.ReadAllText(Path.Combine(workspace.TemplateDirectory, "template.json")), ThesisJson.Options)
            : null;
        var included = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(staging, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        return new TemplatePilotPackageManifest
        {
            SchemaVersion = "1.0.0",
            WorkspaceId = workspace.Manifest.WorkspaceId,
            TemplateId = template?.Id ?? workspace.Manifest.WorkspaceId,
            TemplateVersion = template?.Version ?? "1.0.0",
            CreatedAt = "2000-01-01T00:00:00Z",
            IncludedFiles = included,
            PrivacyScanSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["isValid"] = privacy.IsValid,
                ["breakingCount"] = privacy.BreakingCount,
                ["warningCount"] = privacy.WarningCount,
                ["suppressedWarningCount"] = privacy.SuppressedWarningCount
            }
        };
    }

    private static void WriteChecksums(string staging)
    {
        var checksums = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(staging, file).Replace('\\', '/'))
            .Where(path => path != "checksums.json")
            .Order(StringComparer.Ordinal)
            .ToDictionary(path => path, path => Sha256(File.ReadAllBytes(Path.Combine(staging, path))), StringComparer.Ordinal);
        File.WriteAllText(Path.Combine(staging, "checksums.json"), JsonSerializer.Serialize(checksums, ThesisJson.Options));

        var manifestPath = Path.Combine(staging, "manifest.json");
        var manifest = JsonSerializer.Deserialize<TemplatePilotPackageManifest>(File.ReadAllText(manifestPath), ThesisJson.Options)!;
        manifest.IncludedFiles = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(staging, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        manifest.Sha256 = checksums.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ThesisJson.Options));
        checksums["manifest.json"] = Sha256(File.ReadAllBytes(manifestPath));
        File.WriteAllText(Path.Combine(staging, "checksums.json"), JsonSerializer.Serialize(checksums.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), ThesisJson.Options));
    }

    private static void CreateZip(string staging, string outputZipPath)
    {
        if (File.Exists(outputZipPath))
        {
            File.Delete(outputZipPath);
        }

        using var stream = File.Create(outputZipPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var relative in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(staging, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal))
        {
            var entry = archive.CreateEntry(relative, CompressionLevel.NoCompression);
            entry.LastWriteTime = StableTimestamp;
            using var entryStream = entry.Open();
            using var fileStream = File.OpenRead(Path.Combine(staging, relative));
            fileStream.CopyTo(entryStream);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var extension = Path.GetExtension(file);
            if (ForbiddenExtensions.Contains(extension))
            {
                continue;
            }

            var relative = Path.GetRelativePath(source, file);
            if (relative.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
            {
                continue;
            }

            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

public sealed class TemplatePilotPackageValidator
{
    private static readonly HashSet<string> ForbiddenExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".wps", ".ttf", ".otf", ".ttc"
    };

    public TemplatePilotPackageValidationResult Validate(string packagePath)
    {
        var result = new TemplatePilotPackageValidationResult { PackagePath = packagePath };
        using var archive = ZipFile.OpenRead(packagePath);
        var entries = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).Order(StringComparer.Ordinal).ToList();
        foreach (var entry in entries)
        {
            if (entry.StartsWith("/", StringComparison.Ordinal) || entry.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
            {
                AddError(result, "privacy.package.path.invalid", entry, $"Invalid package path: {entry}", "Remove entries that escape the package root.");
            }

            if (ForbiddenExtensions.Contains(Path.GetExtension(entry)))
            {
                AddError(result, "privacy.package.forbiddenExtension", entry, $"Forbidden package entry: {entry}", "Remove source documents and font binaries from the package.");
            }
        }

        var checksumsEntry = archive.GetEntry("checksums.json");
        if (checksumsEntry is null)
        {
            AddError(result, "privacy.package.checksums.missing", "checksums.json", "Missing checksums.json.", "Build the pilot package with onboarding package so checksums are generated.");
        }
        else
        {
            var checksums = JsonSerializer.Deserialize<Dictionary<string, string>>(ReadEntry(checksumsEntry), ThesisJson.Options)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in checksums.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var entry = archive.GetEntry(pair.Key);
                if (entry is null)
                {
                    AddError(result, "privacy.package.checksumEntry.missing", pair.Key, $"Checksum entry missing from package: {pair.Key}", "Regenerate the pilot package so checksums match included files.");
                    continue;
                }

                var actual = Convert.ToHexString(SHA256.HashData(ReadEntryBytes(entry))).ToLowerInvariant();
                if (!string.Equals(actual, pair.Value, StringComparison.OrdinalIgnoreCase))
                {
                    AddError(result, "privacy.package.checksum.mismatch", pair.Key, $"Checksum mismatch for {pair.Key}.", "Regenerate the pilot package from a clean onboarding workspace.");
                }
            }
        }

        var manifestEntry = archive.GetEntry("manifest.json");
        if (manifestEntry is null)
        {
            AddError(result, "privacy.package.manifest.missing", "manifest.json", "Missing manifest.json.", "Build the pilot package with onboarding package.");
        }
        else
        {
            result.Manifest = JsonSerializer.Deserialize<TemplatePilotPackageManifest>(ReadEntry(manifestEntry), ThesisJson.Options);
        }

        result.IsValid = result.Errors.Count == 0;
        result.Errors = result.Errors.Order(StringComparer.Ordinal).ToList();
        result.Diagnostics = result.Diagnostics
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Path, StringComparer.Ordinal)
            .ToList();
        return result;
    }

    private static void AddError(TemplatePilotPackageValidationResult result, string code, string path, string message, string fixHint)
    {
        result.Errors.Add(message);
        result.Diagnostics.Add(new UnifiedDiagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Error,
            Path = path,
            Message = message,
            FixHint = fixHint,
            Category = DiagnosticCategory.Privacy,
            Source = "TemplatePilotPackageValidator"
        });
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}

public sealed class TemplatePilotPackageBuildResult
{
    public bool IsValid { get; set; }
    public string PackagePath { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = [];
    public TemplatePilotPackageManifest? Manifest { get; set; }
    public PrivacyGuardResult? PrivacyScan { get; set; }
}

public sealed class TemplatePilotPackageValidationResult
{
    public string PackagePath { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<UnifiedDiagnostic> Diagnostics { get; set; } = [];
    public TemplatePilotPackageManifest? Manifest { get; set; }
}

public sealed class TemplatePilotPackageManifest
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public string WorkspaceId { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = "1.0.0";
    public string CreatedAt { get; set; } = "2000-01-01T00:00:00Z";
    public List<string> IncludedFiles { get; set; } = [];
    public Dictionary<string, string> Sha256 { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, object> PrivacyScanSummary { get; set; } = new(StringComparer.Ordinal);
}
