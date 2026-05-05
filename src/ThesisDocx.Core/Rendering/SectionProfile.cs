using ThesisDocx.Core.Models;

namespace ThesisDocx.Core.Rendering;

public enum SectionProfile
{
    Cover,
    FrontMatter,
    Body
}

public static class SectionProfileExtensions
{
    public static SectionProfile FromSectionKind(ThesisSectionKind kind)
    {
        return kind switch
        {
            ThesisSectionKind.Cover => SectionProfile.Cover,
            ThesisSectionKind.OriginalityStatement or ThesisSectionKind.Abstract or ThesisSectionKind.Toc => SectionProfile.FrontMatter,
            _ => SectionProfile.Body
        };
    }

    public static string SpecKey(this SectionProfile profile)
    {
        return profile switch
        {
            SectionProfile.Cover => "cover",
            SectionProfile.FrontMatter => "frontMatter",
            _ => "body"
        };
    }
}
