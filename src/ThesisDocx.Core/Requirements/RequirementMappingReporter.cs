using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Templates;

namespace ThesisDocx.Core.Requirements;

public sealed class RequirementMappingReporter
{
    public RequirementMappingReport Build(RequirementCapture capture, string? templatePath = null)
    {
        var validation = new RequirementCaptureValidator().Validate(capture);
        var byRequirement = capture.Mappings
            .GroupBy(mapping => mapping.RequirementId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var coverage = capture.Requirements
            .GroupBy(requirement => requirement.Category)
            .Select(group =>
            {
                var requirements = group.ToList();
                var mapped = requirements.Count(r => HasStatus(byRequirement, r.Id, RequirementMappingStatus.Mapped));
                var partial = requirements.Count(r => HasStatus(byRequirement, r.Id, RequirementMappingStatus.Partial));
                var unsupported = requirements.Count(r => HasStatus(byRequirement, r.Id, RequirementMappingStatus.NotSupported));
                return new RequirementCategoryCoverage
                {
                    Category = group.Key.ToString(),
                    Total = requirements.Count,
                    Mapped = mapped,
                    Partial = partial,
                    Unsupported = unsupported,
                    Unmapped = requirements.Count - mapped - partial - unsupported
                };
            })
            .OrderBy(c => c.Category, StringComparer.Ordinal)
            .ToList();

        var specPaths = capture.Requirements
            .Select(r => r.TargetSpecPath)
            .Concat(capture.Mappings.Select(m => m.SpecPath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var report = new RequirementMappingReport
        {
            IsValid = validation.IsValid,
            Errors = validation.Errors,
            Warnings = validation.Warnings,
            TotalRequirements = capture.Requirements.Count,
            MappedRequirements = capture.Requirements.Count(r => HasStatus(byRequirement, r.Id, RequirementMappingStatus.Mapped)),
            PartialRequirements = capture.Requirements.Count(r => HasStatus(byRequirement, r.Id, RequirementMappingStatus.Partial)),
            UnsupportedRequirements = capture.Requirements.Count(r => HasStatus(byRequirement, r.Id, RequirementMappingStatus.NotSupported)),
            CoverageByCategory = coverage,
            SuggestedSpecPaths = specPaths,
            BlockingIssues = validation.Errors.Select(e => e.ToString()).Order(StringComparer.Ordinal).ToList()
        };
        report.UnmappedRequirements = report.TotalRequirements - report.MappedRequirements - report.PartialRequirements - report.UnsupportedRequirements;

        if (!string.IsNullOrWhiteSpace(templatePath))
        {
            var resolution = new TemplateResolver().Resolve(templatePath);
            if (!resolution.IsValid)
            {
                report.BlockingIssues.AddRange(resolution.Errors.Select(e => e.ToString()));
            }
        }

        report.RecommendedNextActions = BuildActions(report);
        return report;
    }

    private static bool HasStatus(Dictionary<string, List<RequirementMapping>> mappings, string requirementId, RequirementMappingStatus status)
    {
        return mappings.TryGetValue(requirementId, out var values) && values.Any(mapping => mapping.MappingStatus == status);
    }

    private static List<string> BuildActions(RequirementMappingReport report)
    {
        var actions = new List<string>();
        if (report.UnmappedRequirements > 0)
        {
            actions.Add("Map approved requirements to ThesisFormatSpec or TemplatePackage paths.");
        }

        if (report.UnsupportedRequirements > 0)
        {
            actions.Add("Review unsupported requirements and record limitations before approval.");
        }

        if (report.Warnings.Any())
        {
            actions.Add("Review warnings, especially low-confidence approved requirements.");
        }

        if (actions.Count == 0)
        {
            actions.Add("Run template gate and template regression before publishing.");
        }

        return actions.Order(StringComparer.Ordinal).ToList();
    }
}

public sealed class RequirementMappingReport
{
    public string ReportVersion { get; set; } = "1.0.0";

    public bool IsValid { get; set; }

    public List<RequirementCaptureValidationIssue> Errors { get; set; } = [];

    public List<RequirementCaptureValidationIssue> Warnings { get; set; } = [];

    public List<UnifiedDiagnostic> Diagnostics => Errors
        .Select(error => UnifiedDiagnosticMapper.FromRequirementIssue(error, DiagnosticSeverity.Error, "RequirementMappingReporter"))
        .Concat(Warnings.Select(warning => UnifiedDiagnosticMapper.FromRequirementIssue(warning, DiagnosticSeverity.Warning, "RequirementMappingReporter")))
        .ToList();

    public int TotalRequirements { get; set; }

    public int MappedRequirements { get; set; }

    public int PartialRequirements { get; set; }

    public int UnsupportedRequirements { get; set; }

    public int UnmappedRequirements { get; set; }

    public List<RequirementCategoryCoverage> CoverageByCategory { get; set; } = [];

    public List<string> SuggestedSpecPaths { get; set; } = [];

    public List<string> BlockingIssues { get; set; } = [];

    public List<string> RecommendedNextActions { get; set; } = [];
}

public sealed class RequirementCategoryCoverage
{
    public string Category { get; set; } = string.Empty;

    public int Total { get; set; }

    public int Mapped { get; set; }

    public int Partial { get; set; }

    public int Unsupported { get; set; }

    public int Unmapped { get; set; }
}
