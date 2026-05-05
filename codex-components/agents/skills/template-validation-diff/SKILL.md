---
name: template-validation-diff
description: Use when work involves template validate, template diff, resolved format-spec comparison, format rule coverage, template-aware validator checks, or template inspect metadata.
---

# Template Validation, Diff, And Coverage

Validation must be diagnostic and conservative:

- validate template schema and resolved format spec;
- report structured errors with code, path, and message;
- compare resolved specs for diff, not raw template JSON;
- keep diff order deterministic;
- classify changes by rule category where possible;
- never call coverage `supported` unless schema, renderer, validator, tests, and inspect are represented;
- distinguish structural rule diff from visual DOCX diff.

Template-aware DOCX validation should use custom properties and rendered OpenXML content, not assumptions about filenames.

