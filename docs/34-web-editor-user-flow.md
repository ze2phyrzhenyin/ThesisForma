# Web Editor User Flow

## Create A Thesis

1. Open `/`.
2. The home page shows recent drafts (from browser `localStorage`) and three primary actions: 新建论文, 选择模板, 导入 JSON.
3. The page explicitly states frontend-only mode capabilities.
4. Choose 新建论文 or select a template from 选择模板.
5. Fill metadata: title, author, college, major, student id, advisor, and date.
6. Recent drafts are listed automatically. Clicking a draft reopens it without losing the current editor state.

ThesisForma is a **structured thesis editor**, not a Word replacement or a free-layout WYSIWYG tool. All visual formatting (fonts, sizes, margins, borders, captions, TOC styles) is controlled by the selected TemplatePackage, not by the frontend.

## Edit Structure

The editor is organized as:

- left: thesis outline and section navigation
- center: structured document canvas
- right: Properties, Validation, References, and Template tabs

Users add content from `+ 插入内容`.

Groups:

- Common: heading, paragraph, table, figure
- Academic elements: equation, footnote, citation, cross reference
- Structure: page break, bibliography entry, appendix helper

The menu creates structure blocks. It does not expose font, size, margin, line spacing, or border styling.

## Table Editing

When inserting a table, users choose:

- caption
- row count
- column count
- whether the first row is a header

Inside the table block, users can add/delete rows and columns and edit cell text. The UI reminds users that table borders and three-line-table rules are template-owned.

## Figure Editing

When inserting a figure, users create a figure block, then upload or replace the image in the block. The browser stores a local preview and asset metadata in frontend-only mode. Final DOCX image embedding requires a backend asset service.

## Bibliography And Citations

Bibliography entries live in the right `引用` tab. Each entry has:

- key
- type
- display text
- referenced/unreferenced state

Paragraph blocks can insert citation markers from existing bibliography keys. Dangling keys are shown in validation.

## Cross References

Paragraph blocks list headings, figures, tables, and equations as reference targets. The exported ThesisDocument preserves reference inline nodes for the renderer.

## TOC

The TOC section is preview-only and generated from heading blocks. Users should not manually edit TOC text; DOCX rendering emits the Word TOC field behavior supported by Core.

## Export JSON

`导出 JSON` runs local validation first. If errors exist, the editor still exports a draft but shows a warning toast. This supports review workflows without silently deleting content.

## Generate DOCX

In frontend-only Vercel mode, the DOCX button is disabled and explains that a backend rendering service is required. If `VITE_ENABLE_DOCX_RENDER=true` and `VITE_API_BASE_URL` is configured, the editor saves, validates, renders, and exposes the DOCX download result.
