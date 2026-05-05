---
name: requirement-capture
description: Use when work involves RequirementCapture, manual college requirement intake, evidence, requirement mapping, requirement schemas, or requirements CLI commands.
---

# Requirement Capture

RequirementCapture is a manually reviewed audit file, not AI parsing output.

Rules:

- Keep source paths relative.
- Keep evidence short: page, section, short quote, or screenshot placeholder only.
- Do not paste long copyrighted requirement text.
- Approved requirements need evidence and a mapped, partial, or notSupported mapping.
- notSupported mappings need notes.
- Update `schemas/requirement-capture.schema.json`, examples, docs, and tests together.
- Mapping reports should group coverage by category and return actionable next steps.
