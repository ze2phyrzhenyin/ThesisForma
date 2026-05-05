---
name: thesis-format-schema
description: Use when working on ThesisDocument, ThesisFormatSpec, JSON schema, examples, and college formatting rule modeling.
---

# Thesis Format Schema

Keep schema declarative and JSON-serializable. The renderer must not branch on a university or college name. Add fields to `ThesisFormatSpec` when a formatting requirement needs new data.

For every schema change:

- update examples;
- update `docs/02-structured-data-schema.md` or `docs/03-format-spec-schema.md`;
- add or adjust tests if output changes.
