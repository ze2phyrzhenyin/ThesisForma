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
                Supported("notes", "footnotes/endnotes", "$.notes", "Real footnote/endnote parts with configured note styles."),
                Supported("bibliography", "bibliography", "$.bibliography", "Numbered bibliography with hanging indent."),
                Partial("page-templates", "cover/declaration page templates", "$.pageTemplates", "Stable cover/declaration DSL; no absolute positioning."),
                Supported("page-template-spacer", "page template elements", "$.pageTemplates[].blocks[type=spacer]", "Spacer blocks map to deterministic paragraph spacing."),
                Supported("page-template-text", "page template elements", "$.pageTemplates[].blocks[type=text]", "Text blocks resolve metadata and template variables."),
                Supported("page-template-metadata-field", "page template elements", "$.pageTemplates[].blocks[type=metadataField]", "Metadata fields resolve document metadata or variables."),
                Supported("page-template-image", "page template elements", "$.pageTemplates[].blocks[type=image]", "Image blocks resolve declared template assets."),
                Supported("page-template-field-table", "page template elements", "$.pageTemplates[].blocks[type=fieldTable]", "Field tables render structured metadata rows."),
                Supported("page-template-declaration-text", "page template elements", "$.pageTemplates[].blocks[type=declarationText]", "Declaration text blocks render declaration paragraphs and signature fields."),
                Supported("page-template-page-break", "page template elements", "$.pageTemplates[].blocks[type=pageBreak]", "Page break blocks render explicit Word page breaks."),
                Supported("page-template-rule", "page template elements", "$.pageTemplates[].blocks[type=rule]", "Rule blocks render deterministic paragraph borders."),
                Supported("format-page-setup", "format spec coverage", "$.pageSetup", "Page setup is schema, renderer, and conformance covered."),
                Supported("format-default-font", "format spec coverage", "$.defaultFont", "Default font is style and XML covered."),
                Supported("format-heading-1-3", "format spec coverage", "$.headings[1..3]", "Heading levels 1-3 are style, numbering, and snapshot covered."),
                Supported("format-table-defaults", "format spec coverage", "$.tables", "Table defaults, advanced table XML, and three-line tables are covered."),
                Supported("format-notes", "format spec coverage", "$.notes", "Footnote/endnote paragraph styles and reference mark behavior are covered."),
                Supported("format-captions", "format spec coverage", "$.captions", "Figure, table, and equation captions are covered."),
                Supported("format-page-numbering", "format spec coverage", "$.sections", "Section page numbering is covered."),
                Supported("format-bibliography", "format spec coverage", "$.bibliography", "Bibliography style and numbering are covered."),
                Supported("document-feature-advanced-table", "document feature coverage", "$.sections[].blocks[type=table]", "Advanced table merge structures are validated and rendered."),
                Supported("document-feature-table-cell-blocks", "document feature coverage", "$.sections[].blocks[type=table].rows[].cells[].blocks", "Approved nested table cell paragraphs, headings, lists, quotes, and notes render as valid WordprocessingML."),
                Supported("document-feature-notes", "document feature coverage", "$.sections[].blocks[].inlines[type=footnote|endnote]", "Inline footnotes and endnotes render to native note parts."),
                Supported("document-feature-cross-reference", "document feature coverage", "$.sections[].blocks[].inlines[type=reference]", "Cross references render as REF fields."),
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
