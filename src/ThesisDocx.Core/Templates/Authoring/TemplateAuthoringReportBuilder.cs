using System.Text.Json;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Requirements;
using ThesisDocx.Core.Templates.Baselines;
using ThesisDocx.Core.Templates.Gate;
using ThesisDocx.Core.Templates.Regression;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation.FormatRuleCoverage;

namespace ThesisDocx.Core.Templates.Authoring;

public sealed class TemplateAuthoringReportBuilder
{
    public TemplateAuthoringReport Build(TemplateAuthoringReportOptions options)
    {
        var resolution = new TemplateResolver().Resolve(options.TemplatePath);
        var document = LoadDocument(options.DocumentPath);
        var requirements = LoadRequirements(options.RequirementsPath);
        var requirementReport = requirements is null
            ? null
            : new RequirementMappingReporter().Build(requirements, options.TemplatePath);
        var gate = new TemplateGateService().Run(new TemplateGateOptions
        {
            TemplatePath = options.TemplatePath,
            DocumentPath = options.DocumentPath,
            OutputDirectory = Path.Combine(options.OutputDirectory, "gate"),
            CoverageThreshold = options.CoverageThreshold
        });
        var regression = string.IsNullOrWhiteSpace(options.SuitePath)
            ? null
            : new TemplateRegressionRunner().Run(options.SuitePath, Path.Combine(options.OutputDirectory, "regression"));
        var baseline = string.IsNullOrWhiteSpace(options.SuitePath)
            ? null
            : new TemplateBaselineManager().CompareSuite(options.SuitePath, Path.Combine(options.OutputDirectory, "baseline"));
        var coverage = new FormatRuleCoverageReporter().Build(options.TemplatePath);
        var diagnostic = new DiagnosticReportBuilder().Build(gate, regression, baseline, requirementReport, artifacts: gate.Artifacts);

        var report = new TemplateAuthoringReport
        {
            TemplateId = resolution.Template?.Id ?? string.Empty,
            TemplateVersion = resolution.Template?.Version ?? string.Empty,
            TemplateSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["isResolved"] = resolution.IsValid,
                ["name"] = resolution.Template?.Name ?? string.Empty,
                ["school"] = resolution.Template?.School ?? string.Empty,
                ["college"] = resolution.Template?.College ?? string.Empty,
                ["pageTemplateCount"] = resolution.PageTemplates.Count,
                ["assetCount"] = resolution.Assets.Count,
                ["pageTemplateElementCoverage"] = BuildPageTemplateElementCoverage(resolution.PageTemplates)
            },
            RequirementMappingSummary = BuildRequirementSummary(requirementReport),
            ValidationSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["gateStatus"] = gate.Status.ToString(),
                ["failedGateChecks"] = gate.Checks.Count(check => check.Status == TemplateGateCheckStatus.Fail),
                ["diagnosticStatus"] = diagnostic.Status
            },
            RegressionSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["passed"] = regression?.Passed ?? true,
                ["caseCount"] = regression?.Cases.Count ?? 0,
                ["failedCases"] = regression?.FailedCases.Count ?? 0
            },
            BaselineSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["passed"] = baseline?.Passed ?? true,
                ["caseCount"] = baseline?.Cases.Count ?? 0,
                ["failedCases"] = baseline?.Cases.Count(c => !c.Passed) ?? 0
            },
            CoverageSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["coverageRatio"] = gate.CoverageRatio,
                ["threshold"] = options.CoverageThreshold,
                ["ruleCount"] = coverage.Rules.Count,
                ["formatSpecCoverage"] = BuildFormatSpecCoverage(resolution.FormatSpec),
                ["documentFeatureCoverage"] = BuildDocumentFeatureCoverage(document)
            },
            DiagnosticSummary = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["status"] = diagnostic.Status,
                ["issueCount"] = diagnostic.IssueCount,
                ["breakingCount"] = diagnostic.BreakingCount,
                ["warningCount"] = diagnostic.WarningCount
            },
            BlockingIssues = diagnostic.Issues.Where(issue => UnifiedDiagnosticMapper.IsError(issue.Severity)).ToList(),
            Warnings = diagnostic.Issues.Where(issue => UnifiedDiagnosticMapper.IsWarning(issue.Severity)).ToList(),
            RecommendedNextActions = diagnostic.TopRecommendedActions,
            RelatedArtifacts = diagnostic.RelatedArtifacts
        };

        report.Checklist = BuildChecklist(resolution, document, requirementReport, gate, regression, baseline, report).OrderBy(item => item.Code, StringComparer.Ordinal).ToList();
        report.FailedChecklistItems = report.Checklist.Where(item => item.Status == "fail").ToList();
        report.WarningChecklistItems = report.Checklist.Where(item => item.Status == "warning").ToList();
        report.BaselineStatus = baseline?.Passed ?? true ? "pass" : "fail";
        report.RequirementMappingStatus = requirementReport is null ? "notProvided" : requirementReport.IsValid && requirementReport.UnmappedRequirements == 0 ? "pass" : "fail";
        report.RegressionStatus = regression?.Passed ?? true ? "pass" : "fail";
        report.GateStatus = gate.Status == TemplateGateStatus.Fail ? "fail" : gate.Status == TemplateGateStatus.PassWithWarnings ? "warning" : "pass";
        report.DiagnosticStatus = diagnostic.Status;
        report.PublishReadiness = report.BlockingIssues.Count > 0 || report.Checklist.Any(item => item.Status == "fail")
            ? "notReady"
            : report.Warnings.Count > 0 || report.Checklist.Any(item => item.Status == "warning")
                ? "readyWithWarnings"
                : "ready";
        report.QualityScore = ComputeQualityScore(report);
        report.SuggestedMergeDecision = report.BlockingIssues.Count > 0 || report.FailedChecklistItems.Count > 0
            ? "reject"
            : report.Warnings.Count > 0 || report.WarningChecklistItems.Count > 0
                ? "approveWithWarnings"
                : "approve";
        report.ReadinessReasons = BuildReadinessReasons(report);
        return report;
    }

    private static RequirementCapture? LoadRequirements(string? requirementsPath)
    {
        return string.IsNullOrWhiteSpace(requirementsPath)
            ? null
            : new RequirementCaptureLoader().Load(requirementsPath);
    }

    private static ThesisDocument LoadDocument(string documentPath)
    {
        return JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options)
            ?? new ThesisDocument();
    }

    private static Dictionary<string, object> BuildRequirementSummary(RequirementMappingReport? report)
    {
        if (report is null)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provided"] = false
            };
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["provided"] = true,
            ["isValid"] = report.IsValid,
            ["totalRequirements"] = report.TotalRequirements,
            ["mappedRequirements"] = report.MappedRequirements,
            ["partialRequirements"] = report.PartialRequirements,
            ["unsupportedRequirements"] = report.UnsupportedRequirements,
            ["unmappedRequirements"] = report.UnmappedRequirements
        };
    }

    private static IEnumerable<TemplateAuthoringChecklistItem> BuildChecklist(
        TemplateResolutionResult resolution,
        ThesisDocument document,
        RequirementMappingReport? requirements,
        TemplateGateReport gate,
        TemplateRegressionResult? regression,
        TemplateBaselineCompareResult? baseline,
        TemplateAuthoringReport report)
    {
        var templateResolved = resolution.IsValid;
        yield return Item("template.schema", "template schema valid", gate.Checks.Any(c => c.Code == "template.validate" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("format.resolved", "resolved format spec valid", templateResolved && gate.Checks.Any(c => c.Code == "format.schema" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("requirements.valid", "requirement capture valid", requirements?.IsValid ?? true, requirements is null ? "No requirements file provided." : string.Empty);
        yield return Item("requirements.mapped", "approved requirements mapped", requirements is null || requirements.UnmappedRequirements == 0);
        yield return Item("openxml.clean", "OpenXmlValidator clean", gate.Checks.Any(c => c.Code == "openxml" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("format.clean", "FormatConformanceValidator clean", gate.Checks.Any(c => c.Code == "format.conformance" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("layout.generated", "layout signature generated", gate.Checks.Any(c => c.Code == "layout.signature" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("baseline.passed", "baseline compare passed", baseline?.Passed ?? true);
        yield return Item("regression.passed", "regression suite passed", regression?.Passed ?? true);
        yield return Item("gate.passed", "gate passed", gate.Status != TemplateGateStatus.Fail);
        yield return Item("coverage.threshold", "coverage threshold passed", gate.Checks.Any(c => c.Code == "coverage" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("pageTemplates.rendered", "page templates rendered", gate.Artifacts.ContainsKey("docx"));
        yield return Item("assets.rendered", "required assets rendered", gate.Checks.Any(c => c.Code == "assets.forbidden" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("customProperties.written", "custom properties written", gate.Checks.Any(c => c.Code == "customProperties" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("limitations.documented", "limitations documented", gate.Checks.Any(c => c.Code == "limitations" && c.Status == TemplateGateCheckStatus.Pass));
        yield return Item("docs.present", "docs present", true);
        yield return Item("diagnostics.clean", "diagnostics clean", report.BlockingIssues.Count == 0);

        foreach (var item in PageTemplateElementChecklist(resolution.PageTemplates))
        {
            yield return item;
        }

        foreach (var item in FormatSpecChecklist(resolution.FormatSpec))
        {
            yield return item;
        }

        foreach (var item in DocumentFeatureChecklist(document))
        {
            yield return item;
        }
    }

    private static TemplateAuthoringChecklistItem Item(string code, string title, bool passed, string? message = null)
    {
        return new TemplateAuthoringChecklistItem
        {
            Code = code,
            Title = title,
            Status = passed ? "pass" : "fail",
            Message = passed ? "passed" : string.IsNullOrWhiteSpace(message) ? "failed" : message
        };
    }

    private static IEnumerable<TemplateAuthoringChecklistItem> PageTemplateElementChecklist(IReadOnlyList<TemplatePageLayout> pageTemplates)
    {
        yield return Item("pageTemplate.spacer", "page template spacer element covered", HasBlock<SpacerLayoutBlock>(pageTemplates), "Add a spacer block or document why it is unnecessary.");
        yield return Item("pageTemplate.text", "page template text element covered", HasBlock<TextLayoutBlock>(pageTemplates), "Add a text block or document why it is unnecessary.");
        yield return Item("pageTemplate.metadataField", "page template metadataField element covered", HasBlock<MetadataFieldLayoutBlock>(pageTemplates), "Add a metadataField block or document why it is unnecessary.");
        yield return Item("pageTemplate.image", "page template image element covered", HasBlock<ImageLayoutBlock>(pageTemplates), "Add an image block bound to a declared asset or document why it is unnecessary.");
        yield return Item("pageTemplate.fieldTable", "page template fieldTable element covered", HasBlock<FieldTableLayoutBlock>(pageTemplates), "Add a fieldTable block or document why it is unnecessary.");
        yield return Item("pageTemplate.declarationText", "page template declarationText element covered", HasBlock<DeclarationTextLayoutBlock>(pageTemplates), "Add a declarationText block or document why it is unnecessary.");
        yield return Item("pageTemplate.pageBreak", "page template pageBreak element covered", HasBlock<PageBreakLayoutBlock>(pageTemplates), "Add a pageBreak block or document why it is unnecessary.");
    }

    private static IEnumerable<TemplateAuthoringChecklistItem> FormatSpecChecklist(ThesisFormatSpec? format)
    {
        yield return Item("formatSpec.pageSetup", "format spec page setup covered", format?.PageSetup is not null, "Define pageSetup.");
        yield return Item("formatSpec.defaultFont", "format spec default font covered", format?.DefaultFont is not null, "Define defaultFont.");
        yield return Item("formatSpec.bodyParagraph", "format spec paragraph covered", format?.BodyParagraph is not null, "Define bodyParagraph.");
        yield return Item("formatSpec.heading1to3", "format spec heading 1-3 covered", format?.Headings.ContainsKey(1) == true && format.Headings.ContainsKey(2) && format.Headings.ContainsKey(3), "Define headings 1, 2, and 3.");
        yield return Item("formatSpec.tableDefaults", "format spec table defaults covered", format?.Tables is not null, "Define tables.");
        yield return Item("formatSpec.captions", "format spec captions covered", format?.Captions is not null, "Define captions.");
        yield return Item("formatSpec.pageNumbering", "format spec page numbering covered", format?.Sections.ContainsKey("cover") == true && format.Sections.ContainsKey("frontMatter") && format.Sections.ContainsKey("body"), "Define cover/frontMatter/body section page numbering.");
        yield return Item("formatSpec.headerFooter", "format spec header/footer covered", format?.HeaderFooter is not null, "Define headerFooter.");
        yield return Item("formatSpec.bibliography", "format spec bibliography covered", format?.Bibliography is not null, "Define bibliography.");
    }

    private static IEnumerable<TemplateAuthoringChecklistItem> DocumentFeatureChecklist(ThesisDocument document)
    {
        var blocks = document.Sections.SelectMany(section => section.Blocks).ToList();
        var inlines = FlattenInlines(blocks).ToList();
        var tables = blocks.OfType<TableBlock>().ToList();
        yield return Item("document.headings", "document fixture includes headings", blocks.OfType<HeadingBlock>().Any(), "Add heading blocks to the gate fixture.");
        yield return Item("document.paragraphs", "document fixture includes paragraphs", blocks.OfType<ParagraphBlock>().Any(), "Add paragraph blocks to the gate fixture.");
        yield return Item("document.tables", "document fixture includes tables", tables.Count > 0, "Add a table block to the gate fixture.");
        yield return Item("document.advancedTable", "document fixture includes advanced table merge fields", tables.Any(HasAdvancedTableFields), "Add gridSpan or verticalMerge to an advanced table fixture.");
        yield return Item("document.figures", "document fixture includes figures", blocks.OfType<FigureBlock>().Any(), "Add a figure block to the gate fixture.");
        yield return Item("document.equations", "document fixture includes equations", blocks.OfType<EquationBlock>().Any(), "Add equation blocks to the gate fixture.");
        yield return Item("document.crossReferences", "document fixture includes cross references", inlines.OfType<ReferenceInline>().Any(), "Add a reference inline to the gate fixture.");
        yield return Item("document.bibliography", "document fixture includes bibliography", blocks.OfType<BibliographyBlock>().Any(), "Add a bibliography block to the gate fixture.");
        yield return Item("document.notes", "document fixture includes footnotes/endnotes", inlines.OfType<FootnoteInline>().Any() || inlines.OfType<EndnoteInline>().Any() || blocks.OfType<FootnoteBlock>().Any() || blocks.OfType<EndnoteBlock>().Any(), "Add a footnote or endnote to the gate fixture.");
    }

    private static Dictionary<string, bool> BuildPageTemplateElementCoverage(IReadOnlyList<TemplatePageLayout> pageTemplates)
    {
        return new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["spacer"] = HasBlock<SpacerLayoutBlock>(pageTemplates),
            ["text"] = HasBlock<TextLayoutBlock>(pageTemplates),
            ["metadataField"] = HasBlock<MetadataFieldLayoutBlock>(pageTemplates),
            ["image"] = HasBlock<ImageLayoutBlock>(pageTemplates),
            ["fieldTable"] = HasBlock<FieldTableLayoutBlock>(pageTemplates),
            ["declarationText"] = HasBlock<DeclarationTextLayoutBlock>(pageTemplates),
            ["pageBreak"] = HasBlock<PageBreakLayoutBlock>(pageTemplates)
        };
    }

    private static Dictionary<string, bool> BuildFormatSpecCoverage(ThesisFormatSpec? format)
    {
        return new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["pageSetup"] = format?.PageSetup is not null,
            ["defaultFont"] = format?.DefaultFont is not null,
            ["bodyParagraph"] = format?.BodyParagraph is not null,
            ["heading1to3"] = format?.Headings.ContainsKey(1) == true && format.Headings.ContainsKey(2) && format.Headings.ContainsKey(3),
            ["tables"] = format?.Tables is not null,
            ["captions"] = format?.Captions is not null,
            ["pageNumbering"] = format?.Sections.ContainsKey("cover") == true && format.Sections.ContainsKey("frontMatter") && format.Sections.ContainsKey("body"),
            ["headerFooter"] = format?.HeaderFooter is not null,
            ["bibliography"] = format?.Bibliography is not null
        };
    }

    private static Dictionary<string, bool> BuildDocumentFeatureCoverage(ThesisDocument document)
    {
        var blocks = document.Sections.SelectMany(section => section.Blocks).ToList();
        var inlines = FlattenInlines(blocks).ToList();
        var tables = blocks.OfType<TableBlock>().ToList();
        return new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["headings"] = blocks.OfType<HeadingBlock>().Any(),
            ["paragraphs"] = blocks.OfType<ParagraphBlock>().Any(),
            ["tables"] = tables.Count > 0,
            ["advancedTable"] = tables.Any(HasAdvancedTableFields),
            ["figure"] = blocks.OfType<FigureBlock>().Any(),
            ["equation"] = blocks.OfType<EquationBlock>().Any(),
            ["crossReference"] = inlines.OfType<ReferenceInline>().Any(),
            ["bibliography"] = blocks.OfType<BibliographyBlock>().Any(),
            ["notes"] = inlines.OfType<FootnoteInline>().Any() || inlines.OfType<EndnoteInline>().Any() || blocks.OfType<FootnoteBlock>().Any() || blocks.OfType<EndnoteBlock>().Any()
        };
    }

    private static bool HasBlock<T>(IEnumerable<TemplatePageLayout> pageTemplates) where T : PageLayoutBlock
    {
        return pageTemplates.SelectMany(page => page.Blocks).OfType<T>().Any()
            || pageTemplates.SelectMany(page => page.Blocks).OfType<FieldTableLayoutBlock>().SelectMany(table => table.Rows).SelectMany(row => row).OfType<T>().Any()
            || pageTemplates.SelectMany(page => page.Blocks).OfType<DeclarationTextLayoutBlock>().SelectMany(block => block.SignatureFields).OfType<T>().Any();
    }

    private static bool HasAdvancedTableFields(TableBlock table)
    {
        return table.Rows.SelectMany(row => row.Cells).Any(cell => cell.GridSpan > 1 || cell.VerticalMerge is VerticalMergeKind.Restart or VerticalMergeKind.Continue);
    }

    private static IEnumerable<InlineNode> FlattenInlines(IEnumerable<BlockNode> blocks)
    {
        foreach (var block in blocks)
        {
            foreach (var inline in BlockInlines(block))
            {
                yield return inline;
                foreach (var nested in NestedInlines(inline))
                {
                    yield return nested;
                }
            }

            foreach (var child in ChildBlocks(block))
            {
                foreach (var inline in FlattenInlines([child]))
                {
                    yield return inline;
                }
            }
        }
    }

    private static IEnumerable<InlineNode> BlockInlines(BlockNode block)
    {
        return block switch
        {
            ParagraphBlock paragraph => paragraph.Inlines,
            HeadingBlock heading => heading.Inlines,
            QuoteBlock quote => quote.Inlines,
            FootnoteBlock footnote => footnote.Inlines,
            EndnoteBlock endnote => endnote.Inlines,
            _ => []
        };
    }

    private static IEnumerable<BlockNode> ChildBlocks(BlockNode block)
    {
        return block switch
        {
            ListBlock list => list.Items.SelectMany(item => item.Blocks),
            TableBlock table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks),
            _ => []
        };
    }

    private static IEnumerable<InlineNode> NestedInlines(InlineNode inline)
    {
        var children = inline switch
        {
            BookmarkInline bookmark => bookmark.Inlines,
            FootnoteInline footnote => footnote.Inlines,
            EndnoteInline endnote => endnote.Inlines,
            _ => []
        };

        foreach (var child in children)
        {
            yield return child;
            foreach (var nested in NestedInlines(child))
            {
                yield return nested;
            }
        }
    }

    private static int ComputeQualityScore(TemplateAuthoringReport report)
    {
        var score = 100;
        score -= report.FailedChecklistItems.Count * 10;
        score -= report.WarningChecklistItems.Count * 4;
        score -= report.BlockingIssues.Count * 8;
        score -= report.Warnings.Count * 3;
        if (report.CoverageSummary.TryGetValue("coverageRatio", out var ratioValue) && Convert.ToDouble(ratioValue) < 0.9)
        {
            score -= 5;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static List<string> BuildReadinessReasons(TemplateAuthoringReport report)
    {
        var reasons = new List<string>();
        if (report.FailedChecklistItems.Count > 0)
        {
            reasons.Add($"{report.FailedChecklistItems.Count} checklist item(s) failed.");
        }

        if (report.BlockingIssues.Count > 0)
        {
            reasons.Add($"{report.BlockingIssues.Count} blocking diagnostic issue(s) remain.");
        }

        if (report.WarningChecklistItems.Count > 0 || report.Warnings.Count > 0)
        {
            reasons.Add("Warnings require reviewer acknowledgement.");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("All quality checks passed.");
        }

        return reasons.Order(StringComparer.Ordinal).ToList();
    }
}

public sealed class TemplateAuthoringReportOptions
{
    public string TemplatePath { get; set; } = string.Empty;

    public string DocumentPath { get; set; } = string.Empty;

    public string? RequirementsPath { get; set; }

    public string? SuitePath { get; set; }

    public string OutputDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "ThesisDocx.Authoring");

    public double CoverageThreshold { get; set; } = 0.85;
}
