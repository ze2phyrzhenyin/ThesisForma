# OpenXML Rendering Guide

## Package Parts

The renderer creates:

- main document part: `word/document.xml`
- styles part: `word/styles.xml`
- numbering part: `word/numbering.xml`
- settings part: `word/settings.xml`
- header/footer parts where configured
- image parts for figures
- footnote and endnote parts when notes are present
- custom document properties in `docProps/custom.xml`

Package properties are set deterministically where possible.

## Styles

The renderer defines:

- `Normal`
- `ThesisBody`
- `Heading1`
- `Heading2`
- `Heading3`
- `ThesisCaption`
- `ThesisBibliography`
- `ThesisTocTitle`
- `ThesisQuote`

Prefer styles for reusable formatting. Direct formatting is acceptable for inline bold/italic/underline/subscript/superscript and for feature-specific assertions like bibliography hanging indentation.

Paragraph style spacing is emitted through `w:spacing`. Normal multiple line spacing uses `w:lineRule="auto"` with Word's 240-based multiplier; exact line spacing uses `lineSpacingExactPt` converted through `UnitConverter.PointsToTwips` and emits `w:lineRule="exact"`.

## Numbering

`NumberingBuilder` defines:

- heading multilevel numbering with num id `1`;
- bibliography numbering with num id `2`;
- ordered list numbering with num id `3`.

Heading paragraphs carry `w:numPr` and `w:outlineLvl`.

## Sections

`SectionBuilder` writes `w:sectPr` with page size, margins, section type, header/footer refs, and page number type. Section breaks are explicit paragraph properties for preceding sections; the final section properties are appended to the body.

## Field Codes

The MVP uses `w:simpleField` for:

- `TOC \o "1-3" \h \z \u`
- `PAGE \* MERGEFORMAT`
- `REF bookmark \h`

Word must update TOC fields after opening the document.

## Equations

`EquationRenderer` writes real OMML inside `word/document.xml`.

- `sourceType: "omml"` is parsed through the OpenXML SDK math element constructors after `OmmlSafetyValidator` accepts the root and namespaces.
- `sourceType: "plain"` becomes `m:oMath/m:r/m:t`.
- `sourceType: "latex"` supports a small subset for superscript and subscript through `m:sSup` and `m:sSub`; unsupported expressions fall back to plain OMML only when the format spec permits it.
- numbered equations are rendered in a centered paragraph with a right tab stop and the number text, for example `(3.1)`.
- equation bookmarks wrap the rendered equation/number target, and inline references use `w:simpleField` with `REF`.

## Footnotes And Endnotes

`NoteManager` creates `FootnotesPart` and `EndnotesPart`. Body content uses `w:footnoteReference` and `w:endnoteReference`. Note parts include separator id `-1`, continuation separator id `0`, and generated content ids starting at `1`.

Note paragraph style ids, fonts, line spacing, and superscript reference mark behavior come from `ThesisFormatSpec.notes`. `StyleBuilder` writes those styles to `styles.xml`, and `NoteManager` applies them to the paragraphs in `footnotes.xml` and `endnotes.xml`.

Note content currently supports basic inline text and simple inline fallback rendering.

## Tables and Figures

Tables include `w:tblGrid`. Three-line tables render top, bottom, and inside horizontal borders, with nil left/right/inside-vertical borders.

Advanced table rendering writes:

- `w:gridSpan` for horizontal merges;
- `w:vMerge` restart/continue for vertical merges;
- `w:tblHeader` for repeated header rows;
- `w:cantSplit` for rows that must not break across pages;
- `w:tblLayout`, `w:tblW`, `w:tblCellMar`, `w:tcMar`, `w:vAlign`, and `w:tcBorders` where configured.

Cell content supports a bounded nested block subset: paragraphs, headings, quotes, lists, footnote blocks, and endnote blocks. Those paths reuse the normal inline renderer so hyperlinks, citations, references, and note references remain valid inside `w:tc`. Unsupported block surfaces are semantic validation errors for table cells.

Table bookmarks are inserted inside the first cell paragraph after `w:pPr`, preserving required WordprocessingML element ordering.

Figures use DrawingML inline drawings and image relationships. Sizes are converted to EMUs through `UnitConverter`.

## Page Templates

`PageTemplateRenderer` implements the stable subset of the page layout DSL for cover and declaration pages. It emits normal OpenXML paragraphs, tables, image drawings, and page breaks. It does not use `altChunk` and does not default to absolute positioning.

Supported layout blocks are `spacer`, `text`, `metadataField`, `fieldTable`, `image`, `declarationText`, `pageBreak`, and `rule`. `rule` emits a deterministic paragraph bottom border, which is useful for cover separators without introducing absolute positioning. `pageSetupOverride` is passed into `SectionBuilder`, so cover/front-matter/body section properties can receive template-specific margins and page setup.

## Custom Properties

Template rendering writes safe metadata only:

- `ThesisDocx.RendererVersion`
- `ThesisDocx.SchemaVersion`
- `ThesisDocx.TemplateId`
- `ThesisDocx.TemplateVersion`
- `ThesisDocx.ResolvedFormatSpecVersion`
- `ThesisDocx.RenderedPageTemplates`
- `ThesisDocx.RenderedVariables`
- `ThesisDocx.RenderedAssets`
