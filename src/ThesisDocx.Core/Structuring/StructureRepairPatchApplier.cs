using System.Text.RegularExpressions;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models;

namespace ThesisDocx.Core.Structuring;

public sealed class StructureRepairPatchApplier
{
    private static readonly Regex BlockPathPattern = new(@"^\$\.sections\[(?<section>[0-9]+)\]\.blocks\[(?<block>[0-9]+)\](?<suffix>.*)$", RegexOptions.CultureInvariant);

    public StructureRepairApplyReport Apply(
        ThesisDocument document,
        ThesisStructureMappingReport mappingReport,
        List<ThesisStructureUnresolvedItem> unresolvedItems,
        List<ThesisStructureEvidenceLink> evidenceLinks,
        StructureRepairPlan plan,
        StructureRepairApplyOptions? options = null)
    {
        options ??= new StructureRepairApplyOptions();
        var applyReport = new StructureRepairApplyReport { PlannedOperationCount = plan.Operations.Count };
        var bindings = BindEvidenceLinks(document, evidenceLinks);
        foreach (var operation in plan.Operations)
        {
            ApplyOperation(document, unresolvedItems, evidenceLinks, bindings, operation, applyReport, options);
        }

        RefreshEvidenceLinkPaths(document, bindings);
        mappingReport.EvidenceLinks = evidenceLinks;
        mappingReport.RuleBasedMappedCount = evidenceLinks.Count;
        mappingReport.UnresolvedCount = unresolvedItems.Count;
        mappingReport.LowConfidenceCount = evidenceLinks.Count(link => link.Confidence < 0.75);
        applyReport.RejectedOperationCount = applyReport.Operations.Count(operation => operation.Status == "rejected");
        applyReport.AppliedOperationCount = applyReport.Operations.Count(operation => operation.Status == "applied");
        applyReport.Status = applyReport.RejectedOperationCount == 0 ? "pass" : "fail";
        return applyReport;
    }

    private static void ApplyOperation(
        ThesisDocument document,
        List<ThesisStructureUnresolvedItem> unresolvedItems,
        List<ThesisStructureEvidenceLink> evidenceLinks,
        List<EvidenceBinding> bindings,
        StructureRepairOperation operation,
        StructureRepairApplyReport report,
        StructureRepairApplyOptions options)
    {
        if (!PassesBasicTrustGate(operation, report, options))
        {
            return;
        }

        switch (operation.Type)
        {
            case StructureRepairOperationType.MoveBlock:
                MoveBlock(document, evidenceLinks, bindings, operation, report, options);
                break;
            case StructureRepairOperationType.EnsureSection:
                EnsureSection(document, operation, report);
                break;
            case StructureRepairOperationType.AddUnresolvedItem:
                AddUnresolvedItem(unresolvedItems, operation, report);
                break;
            case StructureRepairOperationType.RemoveUnresolvedItem:
                RemoveUnresolvedItem(unresolvedItems, operation, report);
                break;
            case StructureRepairOperationType.UpdateHeadingLevel:
                UpdateHeadingLevel(document, bindings, operation, report);
                break;
            case StructureRepairOperationType.PromoteParagraphToHeading:
                PromoteParagraphToHeading(document, bindings, operation, report);
                break;
            case StructureRepairOperationType.DemoteHeadingToParagraph:
                DemoteHeadingToParagraph(document, bindings, operation, report);
                break;
            default:
                Reject(report, operation, "structure.repair.unsupportedOperation", $"Unsupported repair operation '{operation.Type}'.");
                break;
        }
    }

    private static bool PassesBasicTrustGate(StructureRepairOperation operation, StructureRepairApplyReport report, StructureRepairApplyOptions options)
    {
        if (operation.Confidence < options.MinimumOperationConfidence)
        {
            Reject(report, operation, "structure.repair.lowConfidence", $"Operation confidence {operation.Confidence:0.###} is below {options.MinimumOperationConfidence:0.###}.", trustRejected: true);
            return false;
        }

        if (operation.Type == StructureRepairOperationType.MoveBlock && operation.Confidence < options.MinimumMoveConfidence)
        {
            Reject(report, operation, "structure.repair.moveLowConfidence", $"moveBlock confidence {operation.Confidence:0.###} is below {options.MinimumMoveConfidence:0.###}.", trustRejected: true);
            return false;
        }

        return true;
    }

    private static void MoveBlock(
        ThesisDocument document,
        List<ThesisStructureEvidenceLink> evidenceLinks,
        List<EvidenceBinding> bindings,
        StructureRepairOperation operation,
        StructureRepairApplyReport report,
        StructureRepairApplyOptions options)
    {
        if (string.IsNullOrWhiteSpace(operation.SourceEvidencePath))
        {
            Reject(report, operation, "structure.repair.sourceMissing", "moveBlock requires sourceEvidencePath.");
            return;
        }

        var sourceBinding = bindings.FirstOrDefault(binding => string.Equals(binding.Link.EvidencePath, operation.SourceEvidencePath, StringComparison.Ordinal) && binding.Block is not null);
        if (sourceBinding?.Block is null)
        {
            Reject(report, operation, "structure.repair.sourceNotFound", $"No structured block is linked to evidence '{operation.SourceEvidencePath}'.");
            return;
        }

        var sourceLocation = FindBlock(document, sourceBinding.Block);
        if (sourceLocation is null)
        {
            Reject(report, operation, "structure.repair.sourceDetached", $"Block linked to evidence '{operation.SourceEvidencePath}' is no longer in the document.");
            return;
        }

        if (options.RequireAnchoredMove && string.IsNullOrWhiteSpace(operation.TargetEvidencePath)
            && string.IsNullOrWhiteSpace(operation.BeforeEvidencePath)
            && string.IsNullOrWhiteSpace(operation.AfterEvidencePath)
            && string.IsNullOrWhiteSpace(operation.TargetSectionId))
        {
            Reject(report, operation, "structure.repair.moveAnchorMissing", "moveBlock requires a target, before, after, or target section anchor.", trustRejected: true);
            return;
        }

        var targetSection = ResolveTargetSection(document, bindings, operation, sourceLocation.Value.Section);
        if (targetSection is null)
        {
            Reject(report, operation, "structure.repair.targetSectionNotFound", $"Target section '{operation.TargetSectionId}' was not found.");
            return;
        }

        var crossSectionMove = !ReferenceEquals(targetSection, sourceLocation.Value.Section);
        if (crossSectionMove && operation.Confidence < options.MinimumCrossSectionMoveConfidence)
        {
            Reject(report, operation, "structure.repair.crossSectionLowConfidence", $"Cross-section move confidence {operation.Confidence:0.###} is below {options.MinimumCrossSectionMoveConfidence:0.###}.", trustRejected: true);
            return;
        }

        if (crossSectionMove && options.RequireHeadingAnchorForCrossSectionMove && !HasHeadingAnchor(bindings, operation))
        {
            Reject(report, operation, "structure.repair.crossSectionHeadingAnchorMissing", "Cross-section move requires a heading or chapter anchor.", trustRejected: true);
            return;
        }

        var beforePath = PathFor(document, sourceLocation.Value.Section, sourceLocation.Value.BlockIndex);
        sourceLocation.Value.Section.Blocks.RemoveAt(sourceLocation.Value.BlockIndex);
        var insertIndex = ResolveInsertIndex(document, bindings, operation, targetSection);
        targetSection.Blocks.Insert(insertIndex, sourceBinding.Block);
        var afterLocation = FindBlock(document, sourceBinding.Block);
        var afterPath = afterLocation is null ? string.Empty : PathFor(document, afterLocation.Value.Section, afterLocation.Value.BlockIndex);
        evidenceLinks.Add(new ThesisStructureEvidenceLink
        {
            StructuredPath = afterPath,
            EvidencePath = operation.SourceEvidencePath,
            Reason = string.IsNullOrWhiteSpace(operation.Reason) ? "block moved by structure repair plan" : operation.Reason,
            Confidence = Math.Round(Math.Clamp(operation.Confidence, 0, 1), 3)
        });
        report.MovedBlockCount++;
        Applied(report, operation, beforePath, afterPath, "Moved evidence-linked block.");
    }

    private static ThesisSection? ResolveTargetSection(ThesisDocument document, List<EvidenceBinding> bindings, StructureRepairOperation operation, ThesisSection fallback)
    {
        if (!string.IsNullOrWhiteSpace(operation.TargetSectionId))
        {
            return document.Sections.FirstOrDefault(section => string.Equals(section.Id, operation.TargetSectionId, StringComparison.Ordinal));
        }

        var anchor = ResolveAnchorBlock(bindings, operation.BeforeEvidencePath, operation.AfterEvidencePath, operation.TargetEvidencePath);
        var anchorLocation = anchor is null ? null : FindBlock(document, anchor);
        return anchorLocation?.Section ?? fallback;
    }

    private static int ResolveInsertIndex(ThesisDocument document, List<EvidenceBinding> bindings, StructureRepairOperation operation, ThesisSection targetSection)
    {
        var before = ResolveAnchorBlock(bindings, operation.BeforeEvidencePath, string.Empty, string.Empty);
        if (before is not null)
        {
            var location = FindBlock(document, before);
            if (location?.Section == targetSection)
            {
                return location.Value.BlockIndex;
            }
        }

        var after = ResolveAnchorBlock(bindings, string.Empty, operation.AfterEvidencePath, operation.TargetEvidencePath);
        if (after is not null)
        {
            var location = FindBlock(document, after);
            if (location?.Section == targetSection)
            {
                return location.Value.BlockIndex + 1;
            }
        }

        return targetSection.Blocks.Count;
    }

    private static BlockNode? ResolveAnchorBlock(List<EvidenceBinding> bindings, string beforeEvidencePath, string afterEvidencePath, string targetEvidencePath)
    {
        var evidencePath = !string.IsNullOrWhiteSpace(beforeEvidencePath)
            ? beforeEvidencePath
            : !string.IsNullOrWhiteSpace(afterEvidencePath)
                ? afterEvidencePath
                : targetEvidencePath;
        return string.IsNullOrWhiteSpace(evidencePath)
            ? null
            : bindings.FirstOrDefault(binding => string.Equals(binding.Link.EvidencePath, evidencePath, StringComparison.Ordinal) && binding.Block is not null)?.Block;
    }

    private static bool HasHeadingAnchor(List<EvidenceBinding> bindings, StructureRepairOperation operation)
    {
        if (LooksLikeHeading(operation.TargetSectionTitle))
        {
            return true;
        }

        foreach (var evidencePath in AnchorEvidencePaths(operation))
        {
            var binding = bindings.FirstOrDefault(item => string.Equals(item.Link.EvidencePath, evidencePath, StringComparison.Ordinal));
            if (binding is null)
            {
                continue;
            }

            if (binding.Block is HeadingBlock)
            {
                return true;
            }

            if (binding.Link.Reason.Contains("heading", StringComparison.OrdinalIgnoreCase)
                || binding.Link.Reason.Contains("chapter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> AnchorEvidencePaths(StructureRepairOperation operation)
    {
        if (!string.IsNullOrWhiteSpace(operation.BeforeEvidencePath)) yield return operation.BeforeEvidencePath;
        if (!string.IsNullOrWhiteSpace(operation.AfterEvidencePath)) yield return operation.AfterEvidencePath;
        if (!string.IsNullOrWhiteSpace(operation.TargetEvidencePath)) yield return operation.TargetEvidencePath;
    }

    private static string AnchorEvidencePath(StructureRepairOperation operation)
    {
        return AnchorEvidencePaths(operation).FirstOrDefault() ?? string.Empty;
    }

    private static bool LooksLikeHeading(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("章", StringComparison.Ordinal)
            || value.Contains("节", StringComparison.Ordinal)
            || value.Contains("heading", StringComparison.OrdinalIgnoreCase)
            || value.Contains("chapter", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureSection(ThesisDocument document, StructureRepairOperation operation, StructureRepairApplyReport report)
    {
        if (string.IsNullOrWhiteSpace(operation.TargetSectionId))
        {
            Reject(report, operation, "structure.repair.sectionIdMissing", "ensureSection requires targetSectionId.");
            return;
        }

        var existing = document.Sections.FirstOrDefault(section => string.Equals(section.Id, operation.TargetSectionId, StringComparison.Ordinal));
        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(operation.TargetSectionTitle))
            {
                existing.Title = operation.TargetSectionTitle;
            }

            if (operation.TargetSectionKind.HasValue)
            {
                existing.Kind = operation.TargetSectionKind.Value;
            }

            Applied(report, operation, string.Empty, $"$.sections[{document.Sections.IndexOf(existing)}]", "Section already existed and was normalized.");
            return;
        }

        var section = new ThesisSection
        {
            Id = operation.TargetSectionId,
            Kind = operation.TargetSectionKind ?? ThesisSectionKind.Body,
            Title = string.IsNullOrWhiteSpace(operation.TargetSectionTitle) ? operation.TargetSectionId : operation.TargetSectionTitle,
            StartOnNewPage = true
        };
        document.Sections.Add(section);
        report.AddedSectionCount++;
        Applied(report, operation, string.Empty, $"$.sections[{document.Sections.Count - 1}]", "Added target section.");
    }

    private static void AddUnresolvedItem(List<ThesisStructureUnresolvedItem> unresolvedItems, StructureRepairOperation operation, StructureRepairApplyReport report)
    {
        if (string.IsNullOrWhiteSpace(operation.UnresolvedCode) || string.IsNullOrWhiteSpace(operation.UnresolvedMessage))
        {
            Reject(report, operation, "structure.repair.unresolvedFieldsMissing", "addUnresolvedItem requires unresolvedCode and unresolvedMessage.");
            return;
        }

        if (unresolvedItems.Any(item => string.Equals(item.Code, operation.UnresolvedCode, StringComparison.Ordinal)
            && string.Equals(item.EvidencePath, operation.SourceEvidencePath, StringComparison.Ordinal)))
        {
            Applied(report, operation, string.Empty, string.Empty, "Unresolved item already existed.");
            return;
        }

        unresolvedItems.Add(new ThesisStructureUnresolvedItem
        {
            Id = string.IsNullOrWhiteSpace(operation.Id) ? $"repair-unresolved-{unresolvedItems.Count + 1}" : operation.Id,
            Code = operation.UnresolvedCode,
            Severity = string.IsNullOrWhiteSpace(operation.Severity) ? DiagnosticSeverity.Warning : operation.Severity,
            Message = operation.UnresolvedMessage,
            EvidencePath = operation.SourceEvidencePath,
            RecommendedAction = operation.RecommendedAction
        });
        report.AddedUnresolvedCount++;
        Applied(report, operation, string.Empty, $"unresolvedItems[{unresolvedItems.Count - 1}]", "Added unresolved item.");
    }

    private static void RemoveUnresolvedItem(List<ThesisStructureUnresolvedItem> unresolvedItems, StructureRepairOperation operation, StructureRepairApplyReport report)
    {
        var index = unresolvedItems.FindIndex(item =>
            (!string.IsNullOrWhiteSpace(operation.UnresolvedCode) && string.Equals(item.Code, operation.UnresolvedCode, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(operation.Id) && string.Equals(item.Id, operation.Id, StringComparison.Ordinal)));
        if (index < 0)
        {
            Reject(report, operation, "structure.repair.unresolvedNotFound", "No unresolved item matched removeUnresolvedItem.");
            return;
        }

        unresolvedItems.RemoveAt(index);
        report.RemovedUnresolvedCount++;
        Applied(report, operation, $"unresolvedItems[{index}]", string.Empty, "Removed unresolved item.");
    }

    private static void UpdateHeadingLevel(ThesisDocument document, List<EvidenceBinding> bindings, StructureRepairOperation operation, StructureRepairApplyReport report)
    {
        if (!operation.HeadingLevel.HasValue)
        {
            Reject(report, operation, "structure.repair.headingLevelMissing", "updateHeadingLevel requires headingLevel.");
            return;
        }

        var binding = bindings.FirstOrDefault(binding => string.Equals(binding.Link.EvidencePath, operation.SourceEvidencePath, StringComparison.Ordinal) && binding.Block is HeadingBlock);
        if (binding?.Block is not HeadingBlock heading)
        {
            Reject(report, operation, "structure.repair.headingNotFound", $"No heading block is linked to evidence '{operation.SourceEvidencePath}'.");
            return;
        }

        heading.Level = Math.Clamp(operation.HeadingLevel.Value, 1, 6);
        report.UpdatedHeadingCount++;
        var location = FindBlock(document, heading);
        Applied(report, operation, string.Empty, location is null ? string.Empty : PathFor(document, location.Value.Section, location.Value.BlockIndex), $"Updated heading level to {heading.Level}.");
    }

    private static void PromoteParagraphToHeading(ThesisDocument document, List<EvidenceBinding> bindings, StructureRepairOperation operation, StructureRepairApplyReport report)
    {
        var binding = bindings.FirstOrDefault(binding => string.Equals(binding.Link.EvidencePath, operation.SourceEvidencePath, StringComparison.Ordinal) && binding.Block is ParagraphBlock);
        if (binding?.Block is not ParagraphBlock paragraph)
        {
            Reject(report, operation, "structure.repair.paragraphNotFound", $"No paragraph block is linked to evidence '{operation.SourceEvidencePath}'.");
            return;
        }

        var location = FindBlock(document, paragraph);
        if (location is null)
        {
            Reject(report, operation, "structure.repair.paragraphDetached", $"Paragraph linked to evidence '{operation.SourceEvidencePath}' is no longer in the document.");
            return;
        }

        var heading = new HeadingBlock
        {
            Id = paragraph.Id,
            Level = Math.Clamp(operation.HeadingLevel ?? 1, 1, 6),
            Inlines = paragraph.Inlines,
            Numbered = true
        };
        location.Value.Section.Blocks[location.Value.BlockIndex] = heading;
        binding.Block = heading;
        report.PromotedHeadingCount++;
        Applied(report, operation, PathFor(document, location.Value.Section, location.Value.BlockIndex), PathFor(document, location.Value.Section, location.Value.BlockIndex), "Promoted paragraph to heading.");
    }

    private static void DemoteHeadingToParagraph(ThesisDocument document, List<EvidenceBinding> bindings, StructureRepairOperation operation, StructureRepairApplyReport report)
    {
        var binding = bindings.FirstOrDefault(binding => string.Equals(binding.Link.EvidencePath, operation.SourceEvidencePath, StringComparison.Ordinal) && binding.Block is HeadingBlock);
        if (binding?.Block is not HeadingBlock heading)
        {
            Reject(report, operation, "structure.repair.headingNotFound", $"No heading block is linked to evidence '{operation.SourceEvidencePath}'.");
            return;
        }

        var location = FindBlock(document, heading);
        if (location is null)
        {
            Reject(report, operation, "structure.repair.headingDetached", $"Heading linked to evidence '{operation.SourceEvidencePath}' is no longer in the document.");
            return;
        }

        var paragraph = new ParagraphBlock
        {
            Id = heading.Id,
            Inlines = heading.Inlines
        };
        location.Value.Section.Blocks[location.Value.BlockIndex] = paragraph;
        binding.Block = paragraph;
        report.DemotedHeadingCount++;
        Applied(report, operation, PathFor(document, location.Value.Section, location.Value.BlockIndex), PathFor(document, location.Value.Section, location.Value.BlockIndex), "Demoted heading to paragraph.");
    }

    private static List<EvidenceBinding> BindEvidenceLinks(ThesisDocument document, List<ThesisStructureEvidenceLink> links)
    {
        return links.Select(link =>
        {
            var (block, suffix) = ResolveBlock(document, link.StructuredPath);
            return new EvidenceBinding(link, block, suffix);
        }).ToList();
    }

    private static (BlockNode? Block, string Suffix) ResolveBlock(ThesisDocument document, string structuredPath)
    {
        var match = BlockPathPattern.Match(structuredPath);
        if (!match.Success)
        {
            return (null, string.Empty);
        }

        var sectionIndex = int.Parse(match.Groups["section"].Value, System.Globalization.CultureInfo.InvariantCulture);
        var blockIndex = int.Parse(match.Groups["block"].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (sectionIndex < 0 || sectionIndex >= document.Sections.Count)
        {
            return (null, match.Groups["suffix"].Value);
        }

        var section = document.Sections[sectionIndex];
        return blockIndex < 0 || blockIndex >= section.Blocks.Count
            ? (null, match.Groups["suffix"].Value)
            : (section.Blocks[blockIndex], match.Groups["suffix"].Value);
    }

    private static void RefreshEvidenceLinkPaths(ThesisDocument document, List<EvidenceBinding> bindings)
    {
        foreach (var binding in bindings.Where(binding => binding.Block is not null))
        {
            var location = FindBlock(document, binding.Block!);
            if (location is not null)
            {
                binding.Link.StructuredPath = PathFor(document, location.Value.Section, location.Value.BlockIndex) + binding.Suffix;
            }
        }
    }

    private static BlockLocation? FindBlock(ThesisDocument document, BlockNode block)
    {
        for (var sectionIndex = 0; sectionIndex < document.Sections.Count; sectionIndex++)
        {
            var section = document.Sections[sectionIndex];
            for (var blockIndex = 0; blockIndex < section.Blocks.Count; blockIndex++)
            {
                if (ReferenceEquals(section.Blocks[blockIndex], block))
                {
                    return new BlockLocation(section, blockIndex);
                }
            }
        }

        return null;
    }

    private static string PathFor(ThesisDocument document, ThesisSection section, int blockIndex)
    {
        return $"$.sections[{document.Sections.IndexOf(section)}].blocks[{blockIndex}]";
    }

    private static void Applied(StructureRepairApplyReport report, StructureRepairOperation operation, string beforePath, string afterPath, string message)
    {
        report.Operations.Add(new StructureRepairOperationAudit
        {
            OperationId = operation.Id,
            OperationType = operation.Type.ToString(),
            Status = "applied",
            SourceEvidencePath = operation.SourceEvidencePath,
            TargetSectionId = operation.TargetSectionId,
            AnchorEvidencePath = AnchorEvidencePath(operation),
            BeforePath = beforePath,
            AfterPath = afterPath,
            Message = message,
            Reason = operation.Reason,
            Confidence = operation.Confidence
        });
    }

    private static void Reject(StructureRepairApplyReport report, StructureRepairOperation operation, string code, string message, bool trustRejected = false)
    {
        if (trustRejected)
        {
            report.RejectedByTrustCount++;
        }

        report.Operations.Add(new StructureRepairOperationAudit
        {
            OperationId = operation.Id,
            OperationType = operation.Type.ToString(),
            Status = "rejected",
            SourceEvidencePath = operation.SourceEvidencePath,
            TargetSectionId = operation.TargetSectionId,
            AnchorEvidencePath = AnchorEvidencePath(operation),
            Message = message,
            Reason = operation.Reason,
            Confidence = operation.Confidence
        });
        report.Diagnostics.Add(new UnifiedDiagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Error,
            Path = string.IsNullOrWhiteSpace(operation.SourceEvidencePath) ? "$.operations" : operation.SourceEvidencePath,
            Message = message,
            FixHint = "Correct the structure repair plan and rerun Codex review.",
            Category = DiagnosticCategory.Intake,
            Source = nameof(StructureRepairPatchApplier)
        });
    }

    private sealed record EvidenceBinding(ThesisStructureEvidenceLink Link, string Suffix)
    {
        public BlockNode? Block { get; set; }

        public EvidenceBinding(ThesisStructureEvidenceLink link, BlockNode? block, string suffix)
            : this(link, suffix)
        {
            Block = block;
        }
    }
    private readonly record struct BlockLocation(ThesisSection Section, int BlockIndex);
}
