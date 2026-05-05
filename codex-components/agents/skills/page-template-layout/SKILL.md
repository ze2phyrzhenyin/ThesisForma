---
name: page-template-layout
description: Use when work involves cover pages, declaration pages, pageTemplates, layout blocks, field tables, metadata fields, variable rendering, or template image assets.
---

# Page Template Layout

Render page templates as stable WordprocessingML:

- use paragraphs, runs, tables, DrawingML images, section properties, and page breaks;
- do not use `altChunk`;
- do not default to absolute positioning;
- preserve OpenXML element ordering inside paragraphs, tables, rows, and cells;
- resolve variables before creating text nodes;
- use `RelationshipManager` for image assets;
- write XML-level tests for each new block type.

The current DSL is intentionally bounded to common thesis cover/declaration layouts.

