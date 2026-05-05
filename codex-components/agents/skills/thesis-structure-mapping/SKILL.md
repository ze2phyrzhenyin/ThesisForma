---
name: thesis-structure-mapping
description: Use for mapping extraction evidence into ThesisDocument draft JSON, evidence links, mapping reports, and unresolved items.
---

# Thesis Structure Mapping

- Preserve original thesis text; do not rewrite semantics.
- Map sections and blocks from evidence paths.
- If unsure, create an unresolved item with a review action.
- Draft output must validate against `thesis-document.schema.json` when possible.
- Renderer must consume only structured JSON, not the original Word file.
