# Validation and Regression

## Validation

`OpenXmlPackageValidator` wraps `DocumentFormat.OpenXml.Validation.OpenXmlValidator`.

`FormatConformanceValidator` returns structured diagnostics:

- `isValid`
- `errors`
- `warnings`
- `checkedRules`

Each error includes code, message, part name, path, expected value, and actual value when available.

It adds project-level checks:

- required styles exist;
- section properties exist;
- configured margins and page size are present;
- default fonts and body line spacing match;
- heading styles and heading numbering exist;
- lower Roman and decimal page numbering sections exist;
- TOC field exists.
- header/footer page number requirements are met;
- footnote/endnote references and note parts are consistent;
- bibliography hanging indent is present;
- three-line table borders are present;
- equation OMML, numbering format, bookmarks, and REF targets are consistent;
- advanced table layout, width, cell margins, repeated header rows, cantSplit rows, merge markers, and caption/reference bookmarks are present where required;
- figure drawing extents exist.
- template-aware rendering checks when `validate --template` is used, including template custom properties, rendered page templates, required assets, cover metadata fields, and declaration text.

`ThesisInputValidator` handles semantic input checks that JSON Schema cannot express.
For equations it checks source/field consistency, OMML safety, unsupported LaTeX fallback policy, numbering format, and restart level. For tables it checks gridSpan bounds, logical column counts, vertical merge chains, header row ordering, repeatHeaderRows bounds, and caption bookmark uniqueness.

## XML Assertions

Tests open the generated DOCX with `WordprocessingDocument` and inspect typed XML nodes:

- styles and run fonts;
- section page number formats;
- TOC field code;
- heading numbering properties;
- table borders;
- OMML nodes such as `m:oMath`, `m:r`, `m:t`, `m:sSup`, and `m:sSub`;
- advanced table nodes such as `w:gridSpan`, `w:vMerge`, `w:tblHeader`, `w:cantSplit`, `w:tblLayout`, `w:tblW`, `w:tcMar`, `w:vAlign`, and `w:tcBorders`;
- drawing/blip relationships;
- footnote/endnote parts and references;
- bibliography hanging indentation.

Do not accept a test that only checks file existence.

## Snapshot Normalization

`DocxSnapshotNormalizer` converts a DOCX into a stable textual snapshot. It normalizes volatile core-property package entry names and avoids depending on ZIP order.

Current snapshot contents include:

- package entry names;
- style ids;
- numbering level texts;
- field codes;
- section page number formats;
- bookmark names;
- equation summaries;
- advanced table summaries;
- paragraph/table/drawing/header/footer counts.
- custom document properties entry presence.

## Template Diff And Coverage

`TemplateDiffEngine` compares resolved format specs, not raw template JSON. Its output is a deterministic structural rules diff, not a visual DOCX diff.

`FormatRuleCoverageReporter` produces a conservative coverage matrix showing whether a rule category is represented by schema, renderer, validator, tests, and inspect output. Partial means a deliberately bounded subset is implemented.

## DOCX Structure Diff

`DocxStructureDiffEngine` compares DOCX package parts and canonicalized XML markers. It ignores volatile data such as rsid attributes, relationship id values, document property timestamps, DrawingML `docPr` ids, and ZIP entry order.

It is not a visual diff. It detects structural changes such as margins, heading styles, field codes, table borders, drawing extents, notes parts, and custom properties.

## Layout Signature

`DocxLayoutSignatureExtractor` reads OpenXML and produces an approximate layout signature: section setup, fonts/styles, table summaries, figure sizes, equation counts, fields, notes, bibliography, and custom properties. It does not call Microsoft Word and does not render pages to images.

## Template Regression

`TemplateRegressionRunner` renders template cases, runs OpenXML and format validation, writes inspect output, extracts layout signatures, compares layout baselines, compares normalized snapshots, and returns a deterministic report.

Regression reports now include failed case ids, case diagnostics, baseline summary, and next actions. These fields are intended for template authors and CI gates.

## Requirement Mapping And Authoring Diagnostics

`RequirementCaptureValidator` validates manually reviewed college requirement captures. It checks supported schema version, unique ids, relative source paths, approved-rule evidence, mapping status, not-supported notes, and target path syntax.

`RequirementMappingReporter` summarizes mapped, partial, unsupported, and unmapped requirements by category.

`DiagnosticReportBuilder` merges gate, regression, baseline, validator, and requirement-mapping issues. `FixHintEngine` suggests likely spec paths, docs, and related fixtures. Hints are review aids, not automatic fixes.

`TemplateAuthoringReportBuilder` produces a publish-readiness checklist for template authors. It does not prove semantic correctness; human approval remains required for real college templates.

Use:

```bash
scripts/normalize-docx-for-snapshot out/simple.docx out/simple.snapshot.txt
```
## Onboarding And Privacy Gates

The validation stack now includes onboarding-specific checks:

- `privacy scan` verifies that public examples do not contain real institution workspaces or source documents.
- `onboarding validate` checks workspace manifest shape, path hygiene, required directories, requirements, template, and fixtures.
- `onboarding summary` aggregates privacy, requirement, template, fixture, baseline, gate, diagnostic, and authoring status.
- `onboarding package-validate` verifies pilot package checksums and rejects forbidden source/font entries.

These checks are structural quality gates. They do not replace human review and do not perform screenshot-level visual diff.
