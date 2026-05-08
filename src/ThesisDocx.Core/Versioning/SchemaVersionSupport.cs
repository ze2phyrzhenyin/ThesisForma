using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;

namespace ThesisDocx.Core.Versioning;

public static class SchemaContractKind
{
    public const string ThesisDocument = "thesisDocument";
    public const string ThesisFormatSpec = "thesisFormatSpec";
    public const string TemplatePackage = "templatePackage";
}

public static class SupportedSchemaVersions
{
    public static IReadOnlyList<string> ThesisDocument { get; } = [ThesisSchemaVersions.Version100, ThesisSchemaVersions.Version110];
    public static IReadOnlyList<string> ThesisFormatSpec { get; } = [ThesisSchemaVersions.Version100, ThesisSchemaVersions.Version110, ThesisSchemaVersions.Version120];
    public static IReadOnlyList<string> TemplatePackage { get; } = [TemplateSchemaVersions.Version100];
}

public sealed class SchemaVersionSupport
{
    public SchemaVersionCheckResult CheckThesisDocument(string? version)
    {
        return Check(SchemaContractKind.ThesisDocument, version, SupportedSchemaVersions.ThesisDocument, "$.schemaVersion", "thesis.schemaVersion.unsupported");
    }

    public SchemaVersionCheckResult CheckThesisFormatSpec(string? version)
    {
        return Check(SchemaContractKind.ThesisFormatSpec, version, SupportedSchemaVersions.ThesisFormatSpec, "$.schemaVersion", "format.schemaVersion.unsupported");
    }

    public SchemaVersionCheckResult CheckTemplatePackage(string? version)
    {
        return Check(SchemaContractKind.TemplatePackage, version, SupportedSchemaVersions.TemplatePackage, "$.templateSchemaVersion", "template.schemaVersion.unsupported");
    }

    private static SchemaVersionCheckResult Check(string kind, string? version, IReadOnlyList<string> supported, string path, string code)
    {
        var normalized = string.IsNullOrWhiteSpace(version) ? string.Empty : version.Trim();
        var isSupported = supported.Contains(normalized, StringComparer.Ordinal);
        var result = new SchemaVersionCheckResult
        {
            Kind = kind,
            Version = normalized,
            IsSupported = isSupported,
            SupportedVersions = supported.ToList(),
            Direction = isSupported ? ClassifySupportedDirection(normalized, supported) : ClassifyDirection(normalized, supported)
        };

        if (!isSupported)
        {
            result.Diagnostic = new UnifiedDiagnostic
            {
                Code = code,
                Severity = DiagnosticSeverity.Error,
                Path = path,
                Message = $"Unsupported {kind} schema version '{(string.IsNullOrWhiteSpace(normalized) ? "<missing>" : normalized)}'.",
                FixHint = "Use a supported schema version or add an explicit migration before processing.",
                Category = DiagnosticCategory.Schema,
                Source = "SchemaVersionSupport",
                Details =
                {
                    ["direction"] = result.Direction,
                    ["supportedVersions"] = string.Join(",", supported)
                }
            };
        }

        return result;
    }

    private static string ClassifyDirection(string version, IReadOnlyList<string> supported)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "missing";
        }

        if (!Version.TryParse(version, out var parsed)
            || supported.Select(v => Version.TryParse(v, out var supportedVersion) ? supportedVersion : null).Any(v => v is null))
        {
            return "unknown";
        }

        var parsedSupported = supported.Select(Version.Parse).OrderBy(v => v).ToList();
        if (parsed < parsedSupported.First())
        {
            return "old";
        }

        if (parsed > parsedSupported.Last())
        {
            return "future";
        }

        return "unsupported";
    }

    private static string ClassifySupportedDirection(string version, IReadOnlyList<string> supported)
    {
        return string.Equals(version, supported.LastOrDefault(), StringComparison.Ordinal)
            ? "current"
            : "supported";
    }
}

public sealed class SchemaVersionReport
{
    public string ReportVersion { get; set; } = "1.0.0";

    public bool IsValid => Checks.All(check => check.IsSupported);

    public List<SchemaVersionCheckResult> Checks { get; set; } = [];

    public List<UnifiedDiagnostic> Diagnostics => Checks
        .Select(check => check.Diagnostic)
        .Where(diagnostic => diagnostic is not null)
        .Cast<UnifiedDiagnostic>()
        .ToList();

    public static SchemaVersionReport Empty()
    {
        return new SchemaVersionReport();
    }

    public static SchemaVersionReport ForDocumentAndFormat(string? documentVersion, string? formatVersion)
    {
        var support = new SchemaVersionSupport();
        return new SchemaVersionReport
        {
            Checks =
            [
                support.CheckThesisDocument(documentVersion),
                support.CheckThesisFormatSpec(formatVersion)
            ]
        };
    }

    public static SchemaVersionReport ForTemplate(string? templateVersion, string? formatVersion = null)
    {
        var support = new SchemaVersionSupport();
        var checks = new List<SchemaVersionCheckResult>
        {
            support.CheckTemplatePackage(templateVersion)
        };
        if (!string.IsNullOrWhiteSpace(formatVersion))
        {
            checks.Add(support.CheckThesisFormatSpec(formatVersion));
        }

        return new SchemaVersionReport { Checks = checks };
    }
}

public sealed class SchemaVersionCheckResult
{
    public string Kind { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsSupported { get; set; }
    public string Direction { get; set; } = "unknown";
    public List<string> SupportedVersions { get; set; } = [];
    public UnifiedDiagnostic? Diagnostic { get; set; }
}

public interface IThesisDocumentMigrator
{
    ThesisDocument Migrate(ThesisDocument document, string targetVersion);
}

public interface IFormatSpecMigrator
{
    ThesisFormatSpec Migrate(ThesisFormatSpec format, string targetVersion);
}

public interface ITemplatePackageMigrator
{
    TemplatePackage Migrate(TemplatePackage template, string targetVersion);
}

public sealed class NoOpThesisDocumentMigrator : IThesisDocumentMigrator
{
    public ThesisDocument Migrate(ThesisDocument document, string targetVersion) => document;
}

public sealed class NoOpFormatSpecMigrator : IFormatSpecMigrator
{
    public ThesisFormatSpec Migrate(ThesisFormatSpec format, string targetVersion) => format;
}

public sealed class NoOpTemplatePackageMigrator : ITemplatePackageMigrator
{
    public TemplatePackage Migrate(TemplatePackage template, string targetVersion) => template;
}
