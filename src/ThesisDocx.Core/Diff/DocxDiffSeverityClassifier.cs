namespace ThesisDocx.Core.Diff;

public sealed class DocxDiffSeverityClassifier
{
    public DocxDiffSeverity Classify(string category, string path, DocxStructureDiffChangeType changeType)
    {
        if (category is "pageSetup" or "headingStyle" or "toc" or "pageNumber" or "table" or "figure" or "notes")
        {
            return DocxDiffSeverity.Breaking;
        }

        if (category is "customProperties")
        {
            return DocxDiffSeverity.Warning;
        }

        if (changeType is DocxStructureDiffChangeType.Added or DocxStructureDiffChangeType.Removed)
        {
            return DocxDiffSeverity.Warning;
        }

        return path.Contains("rsid", StringComparison.OrdinalIgnoreCase)
            ? DocxDiffSeverity.Info
            : DocxDiffSeverity.Warning;
    }
}
