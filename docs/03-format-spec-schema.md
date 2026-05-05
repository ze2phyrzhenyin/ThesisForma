# Format Spec Schema

`ThesisFormatSpec` describes formatting rules. It is declarative and is the only place where college-specific formatting should live. The root object requires `schemaVersion`; current validators accept `1.0.0`, `1.1.0`, and `1.2.0`.

## Major Areas

- `pageSetup`: paper size, orientation, margins, gutter, header/footer distance, columns.
- `defaultFont`: East Asia font, Latin font, size, bold, italic.
- `bodyParagraph`: line spacing, spacing before/after, first-line indent, hanging indent, alignment, widow control.
- `headings`: per-level heading font, spacing, numbering, page break, outline level, alignment.
- `headerFooter`: header text, header line, page number alignment, first/odd/even settings.
- `toc`: title, min/max levels, field-code behavior.
- `tables`: width, layout, borders, cell margins, caption position, three-line table preference, repeat header rows, and row page-break behavior.
- `equations`: default alignment, font size metadata, numbering rules, spacing, caption style, LaTeX fallback behavior, and OMML safety mode.
- `figures`: default size, centering, caption position.
- `captions`: label and numbering format.
- `bibliography`: title, entry paragraph format, numbering pattern.
- `numbering`: heading/list/bibliography level text.
- `compatibility`: default language and Word compatibility intent.
- `sections`: page numbering behavior for cover, front matter, and body.
- `validation`: semantic validation switches, currently `allowHeadingLevelSkips`.

## Page Number Profiles

The MVP maps section kinds into three profiles:

- `cover`: no page number.
- `frontMatter`: lower Roman numbering, restarted at 1.
- `body`: decimal numbering, restarted at 1.

This mapping can be expanded, but should remain data-driven.

## Example

See `examples/format-specs/basic-cn-thesis.json`.

Formal validation is defined in `schemas/thesis-format-spec.schema.json`.

## Version 1.2.0

`1.2.0` is a compatible format-spec extension used by template examples. It does not remove any `1.0.0` or `1.1.0` behavior. The current implementation uses it for resolved template specs and for page-template-oriented examples.

## Equations

`equations.numbering.format` currently supports placeholders such as `({chapter}.{index})` and `({index})`. The renderer increments equation index in document order and resets it when the configured heading level is encountered. Numbered equations create a bookmark target when `bookmarkId` is present on the block, so inline `reference` nodes can render `REF bookmark \h`.

The LaTeX renderer intentionally supports only a small safe subset: plain text, simple superscript, and simple subscript. Full LaTeX conversion is not implemented.

## Tables

`tables.defaultBorders` and `tables.threeLineTableBorders` are declarative per-edge border specs. Three-line tables use top, header-bottom, and bottom horizontal rules with nil vertical rules. Per-cell border overrides are allowed through the document table cell model.

`defaultWidth` accepts `auto`, `percent`, and `dxa`. `defaultLayout` accepts `autofit` and `fixed`. `repeatHeaderRowsDefault` emits `w:tblHeader`; `allowRowBreakAcrossPagesDefault: false` emits `w:cantSplit` on rows unless a row explicitly overrides behavior.

## Template Merge Rule

When a `TemplatePackage` extends another template, resolved format specs are merged deterministically:

- objects merge recursively;
- scalar values in the child override the parent;
- arrays replace the parent array by default;
- `null` explicitly clears a field when the resulting model remains usable;
- inherited assets keep their original template directory for path resolution.

## Adding a College Template

1. Copy an existing format spec JSON.
2. Change only declarative fields.
3. Add a fixture document if the format uses new behavior.
4. Add XML-level tests for any new rule.
5. Update this document and README support notes.
