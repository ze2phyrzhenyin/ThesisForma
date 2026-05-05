using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ThesisDocx.Core.Onboarding;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Privacy;

public sealed class PrivacyGuard
{
    private static readonly HashSet<string> SourceDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".wps"
    };

    private static readonly HashSet<string> ForbiddenPackageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".wps", ".ttf", ".otf", ".ttc"
    };

    public PrivacyGuardResult Scan(PrivacyGuardOptions options)
    {
        var root = Path.GetFullPath(options.Path);
        var result = new PrivacyGuardResult { RootPath = root };
        if (!Directory.Exists(root) && !File.Exists(root))
        {
            Add(result, "privacy.path.missing", "breaking", root, "Path does not exist.", "Check the workspace or package path.");
            return Finish(result);
        }

        if (Directory.Exists(root))
        {
            ScanDirectory(root, options, result);
        }
        else
        {
            ScanFile(root, root, options, result);
        }

        return Finish(result);
    }

    private static void ScanDirectory(string root, PrivacyGuardOptions options, PrivacyGuardResult result)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            ScanFile(root, file, options, result);
        }
    }

    private static void ScanFile(string root, string file, PrivacyGuardOptions options, PrivacyGuardResult result)
    {
        var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
        var extension = Path.GetExtension(file);
        var isExamples = IsUnderExamples(root) || IsUnderExamples(file);
        var normalizedRelative = relative.Replace('\\', '/');
        var isGeneratedArtifact = normalizedRelative.StartsWith("artifacts/", StringComparison.Ordinal)
            || normalizedRelative.Contains("/artifacts/", StringComparison.Ordinal)
            || normalizedRelative.StartsWith("reports/", StringComparison.Ordinal)
            || normalizedRelative.Contains("/reports/", StringComparison.Ordinal);
        if (isExamples && SourceDocumentExtensions.Contains(extension) && !isGeneratedArtifact)
        {
            Add(result, "privacy.sourceDocumentInExamples", "breaking", relative, "Source documents must not be committed under examples.", "Move real source files to onboarding-workspaces/<slug>/source-documents.");
        }

        if (SensitivePatternCatalog.FontExtensions.Contains(extension))
        {
            Add(result, "privacy.fontAsset.forbidden", "breaking", relative, "Font files must not be distributed by this workflow.", "Keep only font metadata in templates.");
        }

        if (options.PackageMode && ForbiddenPackageExtensions.Contains(extension))
        {
            Add(result, "privacy.package.forbiddenExtension", "breaking", relative, "Pilot package contains a forbidden source or font file.", "Remove source documents and font binaries from the package.");
        }

        if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) || new FileInfo(file).Length > 2_000_000)
        {
            return;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(File.ReadAllText(file));
        }
        catch (JsonException)
        {
            return;
        }

        if (node is null)
        {
            return;
        }

        if (Path.GetFileName(file).Equals("onboarding.json", StringComparison.OrdinalIgnoreCase))
        {
            ScanOnboardingManifest(node, isExamples, relative, result);
        }

        ScanJson(node, "$", relative, options, result);
    }

    private static void ScanOnboardingManifest(JsonNode node, bool isExamples, string path, PrivacyGuardResult result)
    {
        var isReal = node["institution"]?["isRealInstitution"]?.GetValue<bool>() ?? false;
        if (isExamples && isReal)
        {
            Add(result, "privacy.realInstitutionInExamples", "breaking", path, "Real institution workspace is not allowed under examples.", "Move it to onboarding-workspaces/ or another private directory.");
        }
    }

    private static void ScanJson(JsonNode node, string jsonPath, string filePath, PrivacyGuardOptions options, PrivacyGuardResult result)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var nextPath = $"{jsonPath}.{pair.Key}";
                if (pair.Value is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    ScanScalar(pair.Key, text, nextPath, filePath, options, result);
                }

                if (pair.Value is not null)
                {
                    ScanJson(pair.Value, nextPath, filePath, options, result);
                }
            }
        }
        else if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (array[i] is not null)
                {
                    ScanJson(array[i]!, $"{jsonPath}[{i}]", filePath, options, result);
                }
            }
        }
    }

    private static void ScanScalar(string key, string text, string jsonPath, string filePath, PrivacyGuardOptions options, PrivacyGuardResult result)
    {
        if ((key.Contains("path", StringComparison.OrdinalIgnoreCase) || key.EndsWith("Dir", StringComparison.OrdinalIgnoreCase))
            && !key.Equals("rootPath", StringComparison.OrdinalIgnoreCase))
        {
            if (Path.IsPathRooted(text))
            {
                Add(result, "privacy.path.absolute", options.PackageMode ? "breaking" : "warning", $"{filePath}:{jsonPath}", $"Absolute path is not allowed: {text}", "Use a workspace-relative path.");
            }

            if (text.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
            {
                Add(result, "privacy.path.traversal", options.PackageMode ? "breaking" : "warning", $"{filePath}:{jsonPath}", $"Path escapes the workspace: {text}", "Keep paths inside the workspace.");
            }
        }

        if (key.Equals("shortQuote", StringComparison.OrdinalIgnoreCase) && text.Length > options.MaxEvidenceExcerptLength)
        {
            Add(result, "privacy.evidence.tooLong", "warning", $"{filePath}:{jsonPath}", "Evidence excerpt is longer than the configured maximum.", "Keep only short excerpts, page numbers, or section references.");
        }

        if (SensitivePatternCatalog.StudentIdRegex.IsMatch(text))
        {
            Add(result, "privacy.personal.studentId", "warning", $"{filePath}:{jsonPath}", "Value looks like a real student id.", "Replace personal data with fictional or redacted values.");
        }

        if (SensitivePatternCatalog.PhoneRegex.IsMatch(text))
        {
            Add(result, "privacy.personal.phone", "warning", $"{filePath}:{jsonPath}", "Value looks like a phone number.", "Remove personal phone numbers from examples and release packages.");
        }

        if (SensitivePatternCatalog.EmailRegex.IsMatch(text) && !text.Contains("example.", StringComparison.OrdinalIgnoreCase))
        {
            Add(result, "privacy.personal.email", "warning", $"{filePath}:{jsonPath}", "Value looks like a non-example email address.", "Use example.invalid or redact the email.");
        }
    }

    internal static void Add(PrivacyGuardResult result, string code, string severity, string path, string message, string suggestedAction)
    {
        result.Findings.Add(new PrivacyFinding
        {
            Code = code,
            Severity = severity,
            Path = path.Replace('\\', '/'),
            Message = message,
            SuggestedAction = suggestedAction
        });
    }

    private static PrivacyGuardResult Finish(PrivacyGuardResult result)
    {
        result.Findings = result.Findings
            .OrderBy(f => f.Code, StringComparer.Ordinal)
            .ThenBy(f => f.Path, StringComparer.Ordinal)
            .ToList();
        result.IsValid = result.Findings.All(f => f.Severity != "breaking");
        result.BreakingCount = result.Findings.Count(f => f.Severity == "breaking");
        result.WarningCount = result.Findings.Count(f => f.Severity == "warning");
        return result;
    }

    private static bool IsUnderExamples(string path)
    {
        return path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("examples", StringComparer.Ordinal);
    }
}

public sealed class PrivacyGuardOptions
{
    public string Path { get; set; } = string.Empty;
    public int MaxEvidenceExcerptLength { get; set; } = 240;
    public bool PackageMode { get; set; }
}

public sealed class PrivacyGuardResult
{
    public string RootPath { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public int BreakingCount { get; set; }
    public int WarningCount { get; set; }
    public List<PrivacyFinding> Findings { get; set; } = [];
}

public sealed class PrivacyFinding
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string Path { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
}

public static class SensitivePatternCatalog
{
    public static readonly Regex StudentIdRegex = new(@"(?<!\d)(20\d{8,14}|[A-Z]{1,4}\d{7,14})(?!\d)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public static readonly Regex PhoneRegex = new(@"(?<!\d)(\+?\d{1,3}[- ]?)?1[3-9]\d{9}(?!\d)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public static readonly Regex EmailRegex = new(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase) { ".ttf", ".otf", ".ttc" };
}

public static class RedactionHelper
{
    public static JsonNode RedactRequirementCapture(JsonNode source, int maxEvidenceLength = 80)
    {
        var clone = source.DeepClone();
        RedactNode(clone, maxEvidenceLength);
        return clone;
    }

    private static void RedactNode(JsonNode node, int maxEvidenceLength)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(pair => pair.Key).ToList())
            {
                var child = obj[key];
                if (child is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    if (key.Equals("shortQuote", StringComparison.OrdinalIgnoreCase) && text.Length > maxEvidenceLength)
                    {
                        obj[key] = text[..maxEvidenceLength] + "...";
                    }
                    else if (key is "reviewer" or "preparedBy" or "reviewedBy" or "approvedBy")
                    {
                        obj[key] = "REDACTED";
                    }
                    else if (key.Equals("path", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(text) is ".pdf" or ".docx" or ".doc" or ".wps")
                    {
                        obj[key] = "REDACTED";
                    }
                }

                if (obj[key] is not null)
                {
                    RedactNode(obj[key]!, maxEvidenceLength);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    RedactNode(child, maxEvidenceLength);
                }
            }
        }
    }
}
