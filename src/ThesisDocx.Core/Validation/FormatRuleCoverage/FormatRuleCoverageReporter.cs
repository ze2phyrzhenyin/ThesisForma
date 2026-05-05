using ThesisDocx.Core.Templates;

namespace ThesisDocx.Core.Validation.FormatRuleCoverage;

public sealed class FormatRuleCoverageReporter
{
    public FormatRuleCoverageMatrix Build(string templatePath)
    {
        var template = new TemplateResolver().Resolve(templatePath).Template;
        return new FormatRuleCoverageMatrix
        {
            TemplateId = template?.Id ?? string.Empty,
            Rules =
            [
                Supported("page-setup", "page setup", "$.pageSetup", "Paper size, orientation, margins, gutter, header/footer distance."),
                Supported("section-page-numbering", "section/page numbering", "$.sections", "Cover/front matter/body page numbering profiles."),
                Supported("fonts", "fonts", "$.defaultFont", "Default East Asia and Latin fonts."),
                Supported("paragraph", "paragraph", "$.bodyParagraph", "Body line spacing, spacing, indentation, alignment."),
                Supported("headings", "headings", "$.headings", "Heading styles, outline levels, and numbering."),
                Supported("header-footer", "header/footer", "$.headerFooter", "Header text, header line, footer PAGE field."),
                Supported("toc", "TOC", "$.toc", "Word TOC field insertion."),
                Supported("figures", "figures", "$.figures", "Inline DrawingML images and captions."),
                Supported("tables", "tables", "$.tables", "Three-line and advanced table XML."),
                Supported("equations", "equations", "$.equations", "OMML equation rendering and numbering."),
                Supported("cross-references", "citations/cross references", "$.captions", "Bookmarks and REF fields."),
                Supported("notes", "footnotes/endnotes", "$.notes", "Real footnote and endnote parts."),
                Supported("bibliography", "bibliography", "$.bibliography", "Numbered bibliography with hanging indent."),
                Partial("page-templates", "cover/declaration page templates", "$.pageTemplates", "Stable cover/declaration DSL; no absolute positioning."),
                Partial("assets", "assets", "$.assets", "Image assets render; font assets are metadata only."),
                Supported("template-inheritance", "template inheritance", "$.extends", "Recursive deterministic template inheritance and merge.")
            ]
        };
    }

    private static FormatRuleCoverageRule Supported(string id, string category, string path, string description)
    {
        return new FormatRuleCoverageRule
        {
            RuleId = id,
            Category = category,
            SpecPath = path,
            Description = description,
            SchemaCovered = true,
            RendererCovered = true,
            ValidatorCovered = true,
            TestCovered = true,
            InspectCovered = true,
            Status = "supported"
        };
    }

    private static FormatRuleCoverageRule Partial(string id, string category, string path, string description)
    {
        return new FormatRuleCoverageRule
        {
            RuleId = id,
            Category = category,
            SpecPath = path,
            Description = description,
            SchemaCovered = true,
            RendererCovered = true,
            ValidatorCovered = true,
            TestCovered = true,
            InspectCovered = true,
            Status = "partial",
            Notes = "Implemented conservative subset."
        };
    }
}
