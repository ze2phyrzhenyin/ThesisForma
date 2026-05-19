# Structured Data Schema

`ThesisDocument` represents content, not college formatting. The root object requires `schemaVersion`. The renderer and validators currently accept `1.0.0`, `1.1.0`, and `1.2.0`.

Formal schemas live in `schemas/`. Generated reference docs live under `docs/generated/`; update them with `scripts/generate-schema-docs`.

`SchemaVersionSupport` centralizes the supported-version declaration for backend services:

- `ThesisDocument`: `1.0.0`, `1.1.0`, `1.2.0`
- `ThesisFormatSpec`: `1.0.0`, `1.1.0`, `1.2.0`
- `TemplatePackage`: `1.0.0`

Migration hooks exist as no-op interfaces for future versions. They do not automatically rewrite an older document into a newer schema version; unsupported future or old versions should produce diagnostics until an explicit migration is implemented and reviewed.

## Root

- `schemaVersion`: `"1.0.0"`, `"1.1.0"`, or `"1.2.0"`.
- `metadata`: title, subtitle, author, college, major, student id, advisor, date, language.
- `sections`: ordered list of thesis sections.

## Sections

Supported `kind` values:

- `cover`
- `originalityStatement`
- `abstract`
- `toc`
- `body`
- `acknowledgements`
- `bibliography`
- `appendix`
- `teacherComments`

Each section has optional `id`, optional `title`, `startOnNewPage`, and `blocks`.

## Block Nodes

Blocks use JSON polymorphism with `type`:

- `paragraph`
- `heading`
- `list`
- `figure`
- `table`
- `quote`
- `equation`
- `pageBreak`
- `sectionBreak`
- `bibliography`
- `footnote`
- `endnote`

Rendering supports paragraph, heading 1-3, ordered lists, figures, tables, quotes, OMML equations, page break, bibliography entries, and real Word footnote/endnote parts. Footnote and endnote nodes can appear inline or as block-level references.

## Equations

`schemaVersion: "1.1.0"` adds real equation blocks:

- `sourceType`: `omml`, `latex`, or `plain`.
- `omml`: controlled OMML XML with root `m:oMath` or `m:oMathPara`.
- `plainText`: rendered as `m:oMath/m:r/m:t`.
- `latex`: limited subset for simple superscript/subscript expressions; unsupported input can fall back to plain OMML when the format spec allows it.
- `numbering`: optional `enabled`, `label`, `format`, and `restartByHeadingLevel`.
- `bookmarkId`: creates the target used by `reference` inlines.

OMML is safety-checked before rendering. Unknown namespaces, `altChunk`, relationship-like attributes, and script-like content are rejected by semantic validation.

## Advanced Tables

`table` supports the original normal and three-line cases plus `1.1.0` fields:

- table `bookmarkId`, `captionPosition`, `style`, `width`, `alignment`, `layout`, `allowRowBreakAcrossPages`, `repeatHeaderRows`, `borders`, and `cellMargins`;
- row `isHeader`, `cantSplit`, and `heightPt`;
- cell `gridSpan`, `verticalMerge`, `width`, `alignment`, `verticalAlignment`, `shading`, `borders`, `cellMargins`, `font`, and `paragraph`.

Cells may use `blocks` for a bounded nested subset: paragraph, heading, quote, figure, table, list, footnote, and endnote. Semantic validation rejects unsupported cell block surfaces so the renderer does not produce invalid table XML.

Semantic validation checks logical column counts, vertical merge chains, vertical merge span consistency, header row ordering, gridSpan bounds, table widths, cell margins, border overrides, duplicate caption bookmarks, nested cell block support, and table reference targets.

## Figures

`figure.crop` optionally records source-rectangle crop percentages from the original image. The renderer emits the values as DrawingML `a:srcRect`; semantic validation requires each side to be between 0 and 100 and prevents horizontal or vertical crops from removing the full image.

## Preserved Objects

`preservedObject` represents source DOCX drawing objects that do not fit the normal thesis block model, such as text boxes, shapes, charts, SmartArt, or other drawings. It records `objectType`, `preservationMode`, optional `rawXml`, relationship ids, dimensions, extracted text, and the intake evidence path.

`preservationMode` is explicit:

- `reviewOnly` keeps the object as structured evidence and renders a review marker when no extracted text is available;
- `extractText` renders the extracted text as a normal body paragraph;
- `passthrough` writes relationship-free `w:drawing` or `w:pict` raw XML back into the DOCX after a namespace and attribute safety check.

In `1.2.0`, `passthrough` can also carry a bounded `parts` graph for reviewed internal drawing relationships. The renderer recreates allowed image, chart, chart style, and SmartArt diagram parts with deterministic relationship ids, rewrites the raw XML relationship references, and rejects external or unsupported relationship types. External links, embedded workbooks, OLE objects, and macro/script-like surfaces remain out of scope and must stay `reviewOnly` or `extractText`.

## Inline Nodes

Inline nodes use `type`:

- `text`
- `hyperlink`
- `citation`
- `bookmark`
- `reference`
- `footnote`
- `endnote`

`text` supports bold, italic, underline, subscript, and superscript. `reference` renders a Word `REF` field. Headings can also create bookmarks by `bookmarkName`.

## Example

See `examples/simple-thesis/document.json`.

Formal validation is defined in `schemas/thesis-document.schema.json`. JSON Schema handles shape and scalar rules; `ThesisInputValidator` handles semantic rules such as duplicate ids, dangling references, bibliography keys, heading level jumps, empty paragraph warnings, image source existence, inline base64 image safety, note id/content validity, and format values that would produce invalid layout.
For `1.1.0` it also validates equation source consistency, OMML safety, equation numbering formats, table grids, vertical merges, and advanced table references. For `1.2.0` it also validates preserved object part graphs before relationship-backed passthrough rendering.

## Template Interaction

Templates do not change `ThesisDocument`. Cover/declaration page templates read document metadata through source paths such as `metadata.title`, `metadata.author`, and `metadata.studentId`, then render those values into generated page content. CLI variable overrides are template inputs, not document mutations.

## Schema Evolution Rule

Every new model field must be:

- serializable by `System.Text.Json`;
- documented here;
- represented in at least one example when user-facing;
- covered by renderer or validation tests if it changes output.
