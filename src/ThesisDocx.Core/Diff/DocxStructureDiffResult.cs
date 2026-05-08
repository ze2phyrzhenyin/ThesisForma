namespace ThesisDocx.Core.Diff;

public sealed class DocxStructureDiffResult
{
    public string ReportVersion { get; set; } = "1.0.0";

    public string BasePath { get; set; } = string.Empty;

    public string TargetPath { get; set; } = string.Empty;

    public bool IsEqual => Changes.Count == 0;

    public List<DocxStructureDiffChange> Changes { get; set; } = [];
}
