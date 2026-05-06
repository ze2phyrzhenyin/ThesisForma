# Web Editor User Flow

## Create A Thesis

1. Open `/`.
2. Choose "新建论文".
3. Select a template.
4. Fill metadata: title, author, college, major, student id, advisor, date.

## Edit Structure

Use the middle editor surface to add structured blocks:

- heading
- paragraph
- table
- figure
- equation
- page break

Use the right panel for bibliography, validation, template status, and render status. The table editor supports rows, columns, cell text, and header row flags. Figure blocks upload an image asset and store an asset reference rather than base64 content in JSON.

## References

Add bibliography entries in the right panel, then insert citation markers into paragraph blocks. Cross references list current headings, figures, tables, and equations.

## Table Of Contents

The TOC section is generated from heading blocks. Users should not manually edit TOC text; the renderer emits the DOCX field behavior already supported by the Core engine.

## Generate DOCX

Use "生成 DOCX". The editor saves the document, calls server-side validation, renders with the selected template, records the run, and exposes a DOCX download URL.
