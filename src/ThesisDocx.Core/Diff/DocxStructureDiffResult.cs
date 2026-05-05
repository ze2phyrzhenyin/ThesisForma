namespace ThesisDocx.Core.Diff;

public sealed class DocxStructureDiffResult
{
    public string BasePath { get; set; } = string.Empty;

    public string TargetPath { get; set; } = string.Empty;

    public bool IsEqual => Changes.Count == 0;

    public List<DocxStructureDiffChange> Changes { get; set; } = [];
}
