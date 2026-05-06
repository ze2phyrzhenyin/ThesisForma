---
name: web-structured-thesis-editor
description: Use when working on the React structured thesis editor, block editor state, ThesisDocument JSON serialization, citations, cross references, bibliography, figures, tables, validation panels, or render/download UI. The editor is not a Word-like rich text or free layout editor.
---

# Web Structured Thesis Editor

Build structure-first authoring surfaces only.

Rules:

- Users edit content and structure; templates control formatting.
- Do not expose manual font, font-size, line-spacing, page margin, header/footer, or page-number controls.
- Keep editor state as typed data; never use DOM content as the source of truth.
- Serialize through `ThesisDocument` JSON and validate through the API/Core validators.
- Keep all block ids, bookmarks, bibliography keys, and asset ids stable.
- Store uploads under runtime/workspace storage, not `examples/`.
- Tests should verify state changes, serialization, validation issues, API payloads, and block order.

Primary paths:

- `web/src/components/thesis-editor`
- `web/src/api`
- `src/ThesisDocx.Api`
