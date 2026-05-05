---
name: docx-intake-extraction
description: Use for extracting uploaded DOCX files with OpenXML into structured evidence JSON, plain text, and markdown without modifying the source file.
---

# DOCX Intake Extraction

- Use OpenXML/ZIP/WordprocessingML only; do not use Microsoft Word automation.
- Preserve document order with stable paragraph/table/figure indexes.
- Keep user content in ignored onboarding workspaces, not examples or docs.
- Extract evidence, not conclusions: paragraphs, runs, styles, numbering, tables, fields, notes, bookmarks, and sections.
- Save image binaries as artifacts and reference paths from JSON.
