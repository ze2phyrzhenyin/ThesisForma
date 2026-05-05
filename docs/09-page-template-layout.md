# Page Template Layout

Page templates implement a stable thesis-oriented layout DSL for common cover and declaration pages. They do not implement a free-form desktop publishing engine.

## Targets

Supported `targetSectionType` values:

- `cover`
- `declaration`
- `abstract`
- `toc`
- `body`
- `appendix`

Supported `insertPosition` values:

- `beforeSection`
- `afterSection`
- `replaceSectionContent`

The current examples replace cover and declaration section content.

## Blocks

Implemented layout block types:

- `spacer`: creates a paragraph with vertical spacing.
- `text`: renders a styled paragraph with variable placeholders.
- `metadataField`: renders label/value text from metadata, variables, or a template expression.
- `fieldTable`: renders a stable table of metadata fields.
- `image`: renders a template image asset as DrawingML.
- `declarationText`: renders declaration paragraphs and signature/date fields.
- `pageBreak`: emits a Word page break.

All blocks render as normal WordprocessingML. The renderer does not use `altChunk` and does not default to absolute positioning.

## Page Setup Override

`pageSetupOverride` can override section page setup for the section profile reached by the page template. For example, a cover template can set cover margins independently from the body profile. The override is written to `w:sectPr` by `SectionBuilder`.

## Adding A Block

1. Add a `PageLayoutBlock` derived model.
2. Add the discriminator to `TemplatePageLayout.cs`.
3. Extend `PageTemplateRenderer`.
4. Add schema coverage in `template-package.schema.json`.
5. Add XML-level tests.
6. Update this document and examples.

