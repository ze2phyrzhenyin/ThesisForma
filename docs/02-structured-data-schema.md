# Structured Data Schema

`ThesisDocument` represents content, not college formatting. The root object requires `schemaVersion`. The renderer and validators currently accept `1.0.0` and `1.1.0`.

## Root

- `schemaVersion`: `"1.0.0"` or `"1.1.0"`.
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

Semantic validation checks logical column counts, vertical merge chains, vertical merge span consistency, header row ordering, gridSpan bounds, table widths, cell margins, border overrides, duplicate caption bookmarks, and table reference targets.

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
For `1.1.0` it also validates equation source consistency, OMML safety, equation numbering formats, table grids, vertical merges, and advanced table references.

## Template Interaction

Templates do not change `ThesisDocument`. Cover/declaration page templates read document metadata through source paths such as `metadata.title`, `metadata.author`, and `metadata.studentId`, then render those values into generated page content. CLI variable overrides are template inputs, not document mutations.

## Schema Evolution Rule

Every new model field must be:

- serializable by `System.Text.Json`;
- documented here;
- represented in at least one example when user-facing;
- covered by renderer or validation tests if it changes output.
