# Component 05: Common Thesis Rendering Pain Points

## Goal

Extend renderer capability where Chinese thesis formatting commonly breaks: note styles, rich table cell blocks, and bounded page-template blocks.

## Owners

- Schema worker for declarative fields.
- Renderer worker for OpenXML output.
- Validation worker for conformance and inspection.
- Template worker for examples.

## Write Scope

- `src/ThesisDocx.Core/Models/ThesisFormatSpec.cs`
- `schemas/thesis-format-spec.schema.json`
- `src/ThesisDocx.Core/Rendering`
- `src/ThesisDocx.Core/Validation`
- `examples/format-fixtures`
- `examples/templates`
- `docs/03-format-spec-schema.md`
- `docs/04-openxml-rendering-guide.md`
- `tests/ThesisDocx.Tests`

## Required Behavior

- Footnote/endnote style settings come from `ThesisFormatSpec`.
- Table cells can render approved nested block surfaces without invalid OpenXML.
- Page-template block additions remain bounded and deterministic.
- Inspect output exposes enough evidence for regression and authoring reports.

## Acceptance Gates

```bash
dotnet test ThesisDocx.slnx --nologo --filter "FullyQualifiedName~Footnote|FullyQualifiedName~Endnote|FullyQualifiedName~Table|FullyQualifiedName~PageTemplate"
dotnet test ThesisDocx.slnx --nologo
scripts/ci-quality-gate
```

Required test evidence:

- XML assertions for `styles.xml`, `footnotes.xml`, `endnotes.xml`, `document.xml`, and page-template output;
- `OpenXmlValidator` passes;
- fixture examples validate through CLI.

## Boundaries

- Do not use raw XML unless wrapped in a named helper with tests.
- Do not add platform-specific converters or Word automation.

