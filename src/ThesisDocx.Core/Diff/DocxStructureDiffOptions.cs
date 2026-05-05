namespace ThesisDocx.Core.Diff;

public sealed class DocxStructureDiffOptions
{
    public bool IncludeGenericXmlChanges { get; set; }

    public bool CompareCustomProperties { get; set; } = true;

    public HashSet<string> IgnoredPartNames { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "docProps/core.xml"
    };
}
