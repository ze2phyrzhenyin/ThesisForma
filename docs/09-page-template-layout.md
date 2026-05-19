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
- `acknowledgements`
- `bibliography`
- `teacherComments`

Supported `insertPosition` values:

- `beforeSection`
- `afterSection`
- `replaceSectionContent`

`targetSectionId` can narrow a page template to a specific structured section id, for example one Chinese abstract section among several abstract sections. The current examples replace cover and declaration section content and add an abstract-specific marker.

## Blocks

Implemented layout block types:

- `spacer`: creates a paragraph with vertical spacing.
- `text`: renders a styled paragraph with variable placeholders.
- `metadataField`: renders label/value text from metadata, variables, or a template expression.
- `fieldTable`: renders a stable table of metadata fields.
- `image`: renders a template image asset as DrawingML.
- `declarationText`: renders declaration paragraphs and signature/date fields.
- `pageBreak`: emits a Word page break.
- `rule`: emits a deterministic paragraph bottom border for separators.
- `handwritingArea`: emits a bordered table area for handwritten review, signature, or approval content.

All blocks render as normal WordprocessingML. The renderer does not use `altChunk` and does not default to absolute positioning.

`metadataField` and `fieldTable` can set `labelFont` and `valueFont` so a cover can use different label/value typefaces. `fieldTable.rowHeightPt` renders deterministic row height. `handwritingArea` supports label text, height, border color, border thickness, and label alignment.

## Page Setup Override

`pageSetupOverride` can override section page setup for the section profile reached by the page template. For example, a cover template can set cover margins independently from the body profile. The override is written to `w:sectPr` by `SectionBuilder`.

## Adding A Block

1. Add a `PageLayoutBlock` derived model.
2. Add the discriminator to `TemplatePageLayout.cs`.
3. Extend `PageTemplateRenderer`.
4. Add schema coverage in `template-package.schema.json`.
5. Add XML-level tests.
6. Update this document and examples.
