using System.Text.Json.Serialization;

namespace ThesisDocx.Core.Onboarding;

public sealed class OnboardingWorkspaceManifest
{
    public string SchemaVersion { get; set; } = OnboardingWorkspaceSchemaVersions.Current;

    public string WorkspaceId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public OnboardingInstitution Institution { get; set; } = new();

    public OnboardingWorkspacePaths Paths { get; set; } = new();

    public OnboardingPrivacyPolicy Privacy { get; set; } = new();

    public OnboardingQualityPolicy Quality { get; set; } = new();

    public OnboardingWorkspaceStatus Status { get; set; } = new();

    [JsonIgnore]
    public string? WorkspaceRoot { get; set; }
}

public static class OnboardingWorkspaceSchemaVersions
{
    public const string Version100 = "1.0.0";
    public const string Current = Version100;

    public static bool IsSupported(string? version) => version == Version100;
}

public sealed class OnboardingInstitution
{
    public string School { get; set; } = string.Empty;

    public string College { get; set; } = string.Empty;

    public string DegreeType { get; set; } = "master";

    public string Locale { get; set; } = "zh-CN";

    public bool IsRealInstitution { get; set; }

    public string RedactionPolicy { get; set; } = "fictionalExample";
}

public sealed class OnboardingWorkspacePaths
{
    public string SourceDocumentsDir { get; set; } = "source-documents";

    public string RequirementsPath { get; set; } = "requirements/requirements.json";

    public string TemplateDir { get; set; } = "template";

    public string FixturesDir { get; set; } = "fixtures";

    public string BaselinesDir { get; set; } = "baselines";

    public string ReportsDir { get; set; } = "reports";

    public string ArtifactsDir { get; set; } = "artifacts";
}

public sealed class OnboardingPrivacyPolicy
{
    public bool AllowRealInstitutionNamesInWorkspace { get; set; } = true;

    public bool AllowSourceDocumentsInWorkspace { get; set; } = true;

    public bool ProhibitSourceDocumentsInRelease { get; set; } = true;

    public bool ProhibitPersonalDataInExamples { get; set; } = true;

    public int MaxEvidenceExcerptLength { get; set; } = 240;

    public int MaxBase64Length { get; set; } = 200_000;

    public int? MaxWarningCount { get; set; }

    public List<string> SuppressedWarningCodes { get; set; } = [];

    public List<string> SuppressedWarningPathPrefixes { get; set; } = [];
}

public sealed class OnboardingQualityPolicy
{
    public double CoverageThreshold { get; set; } = 0.85;

    public double LayoutThreshold { get; set; } = 0.99;

    public List<string> RequiredReports { get; set; } = ["gate", "diagnostic", "authoring", "summary"];
}

public sealed class OnboardingWorkspaceStatus
{
    public OnboardingPhase Phase { get; set; } = OnboardingPhase.Initialized;

    public string? LastCheckedAt { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public enum OnboardingPhase
{
    Initialized,
    RequirementsDrafted,
    TemplateScaffolded,
    FixturesScaffolded,
    BaselinesInitialized,
    GatePassed,
    ReadyForReview,
    Released
}

public sealed class OnboardingWorkspace
{
    public string Root { get; set; } = string.Empty;

    public OnboardingWorkspaceManifest Manifest { get; set; } = new();
}
