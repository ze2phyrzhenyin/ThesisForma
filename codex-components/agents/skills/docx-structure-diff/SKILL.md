---
name: docx-structure-diff
description: Use when work involves DocxStructureDiffEngine, canonical DOCX package comparison, volatile OpenXML normalization, structural diff severities, or docx diff CLI behavior.
---

# DOCX Structure Diff

Compare OpenXML structure, not screenshots.

- Normalize volatile data such as rsid, relationship ids, docPr ids, timestamps, and package ordering.
- Prefer structured markers for margins, fields, table borders, drawing sizes, notes, and custom properties.
- Keep output deterministic and JSON serializable.
- Do not call Microsoft Word.
- Do not reduce the implementation to raw string comparison.

