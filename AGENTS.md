# AGENTS.md

This repository builds a deterministic DOCX rendering engine for Chinese graduation thesis formatting.

## Product Boundary

Stage 1 only implements:

`ThesisDocument + ThesisFormatSpec -> valid, format-conformant DOCX`

Template rendering is still Stage 1:

`ThesisDocument + TemplatePackage -> resolved ThesisFormatSpec + page templates -> valid DOCX`

Stage 1 does not parse messy user documents, does not use AI to infer structure, and does not rewrite thesis content.

## Hard Rules

1. Never hardcode a specific university or college format inside renderer logic.
2. All formatting decisions must come from `ThesisFormatSpec`, `TemplatePackage`, or a documented default.
3. Every new format feature must include tests that inspect OpenXML XML, not just file existence.
4. Every new model field must update examples and docs.
5. Use shared unit conversion helpers for twips, EMU, half-points, centimeters, millimeters, inches, and points.
6. Avoid broad catch blocks and silent fallbacks.
7. OpenXML part creation must be explicit and deterministic.
8. Snapshot tests must normalize unstable data such as timestamps, relationship ids, generated ids, and package ordering.
9. Renderer classes should be small and composable.
10. Prefer styles and numbering definitions over repeated direct formatting unless direct formatting is explicitly required by the format spec.
11. DOCX diff and layout signature tools are structural gates, not screenshot or semantic diff tools.
12. Real college templates require human review before being treated as accepted fixtures.
13. Real institution source files belong in private onboarding workspaces, not in `examples/`.
14. Pilot packages must be privacy-scanned and must not include source DOCX/PDF files or font binaries.
15. DOCX intake evidence and draft thesis JSON may contain user content; keep them in ignored onboarding workspaces and out of examples/docs.
16. Structure mapping must preserve original thesis text and record uncertainty instead of guessing.

## Architecture

Core data flow:

`ThesisDocument + ThesisFormatSpec -> DocxRenderer -> OpenXML package parts -> DOCX -> OpenXmlValidator -> FormatConformanceValidator -> snapshot / XML assertions`

`TemplatePackage -> TemplateResolver -> resolved ThesisFormatSpec + DocxRenderContext -> DocxRenderer`

## Preferred Implementation Style

Use strongly typed `DocumentFormat.OpenXml` classes where practical. Raw XML is allowed only when the SDK class is awkward or unavailable, and must be wrapped in a named helper with tests.

## Testing

Minimum acceptable test for a rendering feature:

1. Render fixture document.
2. Open generated docx.
3. Locate expected part.
4. Assert expected XML element and attribute.
5. Run `OpenXmlValidator`.
6. Add normalized snapshot when useful.

Never add a test that only checks that `*.docx` exists.

## Worker Boundaries

Schema workers own Models, JSON schema, examples, and docs about structured data.

Renderer workers own Rendering and OpenXml folders.

Validation workers own Validation, OpenXmlAssertions, SnapshotNormalizer, and tests.

Template workers own Models/Templates, Templates, template schemas, examples/templates, page template docs, diff, coverage, and template CLI behavior.

Diff/regression workers own `src/ThesisDocx.Core/Diff`, layout signatures, template regression suites, template gates, and format fixtures. They must not introduce Word automation or screenshot-based comparison.

Quality-workbench workers own requirement capture, baseline management, diagnostics, fix hints, and authoring reports. They must keep evidence short, require baseline update reasons, and treat fix hints as advisory.

CI-quality workers own scripts, workflow drafts, negative fixtures, fix-hint rules, markdown reports, and aggregate CI quality reports. They must keep CI artifacts under `out/ci` and must not reduce CI to only `dotnet test`.

Onboarding workers own `src/ThesisDocx.Core/Onboarding`, `src/ThesisDocx.Core/Privacy`, onboarding schemas, private-workspace docs, and pilot package tooling. They must keep examples fictional and must not commit real college source files.

Intake workers own `src/ThesisDocx.Core/Extraction`, `src/ThesisDocx.Core/Structuring`, DOCX extraction schemas, and intake CLI tests. They must not modify input DOCX files and must not rewrite thesis semantics.

Web editor workers own `web/` and `src/ThesisDocx.Api`. They must keep the editor structure-first: users edit metadata, sections, blocks, citations, references, tables, figures, and bibliography, while formatting remains controlled by `TemplatePackage` and `ThesisFormatSpec`. Do not add manual font, font-size, margin, line-spacing, or Word-like free layout controls.

Docs workers own README and docs, but must not claim unimplemented capabilities.

## CLI

The CLI supports:

- `render`
- `validate`
- `inspect`
- `snapshot`
- `template list`
- `template inspect`
- `template validate`
- `template resolve`
- `template diff`
- `template coverage`
- `template regression`
- `template gate`
- `template diagnose`
- `template authoring-report`
- `requirements validate`
- `requirements report`
- `baseline list`
- `baseline init`
- `baseline compare`
- `baseline update`
- `negative-fixtures run`
- `ci quality-report`
- `privacy scan`
- `onboarding init`
- `onboarding inspect`
- `onboarding validate`
- `onboarding scaffold-requirements`
- `onboarding scaffold-template`
- `onboarding scaffold-fixtures`
- `onboarding baseline-init`
- `onboarding run-gate`
- `onboarding diagnose`
- `onboarding authoring-report`
- `onboarding summary`
- `onboarding package`
- `onboarding package-validate`
- `extract docx`
- `structure draft`
- `structure prompt`
- `intake docx`
- `docx diff`
- `docx layout-signature`
- `docx layout-compare`

The CLI remains the developer and test harness. The web editor MVP lives in `web/` and calls `src/ThesisDocx.Api`; it must not bypass Core validation or write user artifacts into `examples/`.
