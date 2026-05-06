# Web Structured Editor

The web editor is a structure-first authoring surface for `ThesisDocument` JSON. It is not a Word replacement and does not expose manual font, font size, line spacing, page margin, header/footer, or page-number controls.

## Purpose

Users choose a template, fill thesis metadata, create sections and blocks, manage references, validate the structured document, and ask the existing renderer to generate DOCX.

The format path remains:

`Editor state -> ThesisDocument JSON -> validate-input -> TemplatePackage -> DocxRenderer -> DOCX`

## Current MVP

Implemented frontend path: `web/`

Implemented backend path: `src/ThesisDocx.Api`

The MVP supports:

- template list and template status
- metadata form
- three-column editor layout
- outline navigation
- heading blocks
- paragraph blocks with citation and cross-reference markers
- abstract blocks with keywords
- table blocks with row/column editing and header row flag
- figure blocks with image upload, caption, alt text, and thumbnail
- equation text blocks
- bibliography manager
- TOC preview generated from headings
- local validation panel with jump actions
- API validation
- DOCX render and download
- Playwright browser E2E coverage for the full authoring/render/download flow

## Non Goals

The MVP does not provide rich Word-like free layout, online DOCX preview, collaborative editing, AI parsing, visual diff, or database persistence.

## Browser E2E

Run:

```bash
cd web
npm run e2e
```

The current Playwright test uses a real browser and real editor UI interactions. API responses are mocked at the browser network layer for deterministic CI behavior; the real ASP.NET Core render/download path is covered by `WebEditorApiTests`.

The config targets the local Google Chrome channel. On CI, install a compatible Playwright browser or provide Chrome before running this command.

## Runtime Storage

The API stores user drafts and generated artifacts in `runtime/`, which is ignored by git. Do not store user uploads in `examples/` or docs.
