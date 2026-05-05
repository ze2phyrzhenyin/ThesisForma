---
name: openxml-docx-rendering
description: Use when working on DOCX, WordprocessingML, DocumentFormat.OpenXml, styles.xml, numbering.xml, sections, headers/footers, tables, images, drawings, and field codes.
---

# OpenXML DOCX Rendering

Use strongly typed `DocumentFormat.OpenXml` classes where practical. Keep renderer logic layered:

- package creation;
- styles;
- numbering;
- section properties;
- header/footer;
- body blocks;
- field codes;
- tables;
- figures;
- captions;
- bibliography.

All OpenXML units must go through `UnitConverter`. Tests must inspect concrete XML nodes and attributes.
