# Schemas

This directory contains the formal JSON Schema contract for Stage 1 structured input.

- `thesis-document.schema.json`: validates `ThesisDocument` content JSON.
- `thesis-format-spec.schema.json`: validates `ThesisFormatSpec` rule JSON.
- `template-package.schema.json`: validates reusable template packages.
- `template-regression-suite.schema.json`: validates template regression suite definitions.
- `requirement-capture.schema.json`: validates manually reviewed requirement capture files.
- `template-baseline-manifest.schema.json`: validates baseline manifests for template regression and format fixtures.
- `negative-fixture-manifest.schema.json`: validates expected-failure fixture suites.
- `document-overrides.schema.json`: validates document-scoped formatting deltas stored outside `ThesisDocument`.
- `fix-hint-rules.schema.json`: validates the fix hint rule catalog.
- `diagnostic-report.schema.json`: documents the machine-readable diagnostic report shape.
- `version-report.schema.json`: validates the shared `versionReport` payload used by schema-aware CLI JSON and service facade results.
- `report-contract.schema.json`: validates the additive root `reportVersion` family used by service results, render JSON, template gate/diagnose/authoring reports, requirements mapping reports, privacy reports, DOCX diff/layout reports, diagnostic reports, and negative fixture reports.
- `onboarding-workspace.schema.json`: validates private or fictional onboarding workspace manifests.
- `template-pilot-package-manifest.schema.json`: validates deterministic pilot package manifests.
- `docx-extraction.schema.json`: validates OpenXML extraction evidence from uploaded DOCX files.
- `format-candidate-report.schema.json`: validates evidence reports for draft format specs inferred from DOCX extraction clusters.
- `format-candidate-decisions.schema.json`: validates human review decisions for candidate format fields.
- `template-candidate-proposal-report.schema.json`: validates proposal reports produced when reviewed candidate fields are applied to a copied template.
- `structure-repair-plan.schema.json`: constrains Codex-assisted structure repair plans before Core applies them deterministically.
- `intake-regression-manifest.schema.json`: validates private DOCX intake regression manifests. The manifest may be committed only when it contains no source DOCX paths or thesis content; normal use is in ignored private workspaces.

Both schemas require `schemaVersion`. Supported versions are:

- `1.0.0`: original Stage 1 contract.
- `1.1.0`: compatible document/format extension for OMML equations and advanced table fields, used by `examples/full-thesis`.
- `1.2.0`: compatible `ThesisDocument` extension for relationship-backed preserved object part graphs, and compatible `ThesisFormatSpec` extension used by template examples, declarative note styles, and bounded page-template blocks such as `rule`.

`TemplatePackage` has a separate `templateSchemaVersion`; the current supported value is `1.0.0`.

The project uses the `NJsonSchema` NuGet package for schema validation. JSON Schema validates shape and scalar constraints. Cross-document and cross-reference checks are handled separately by `ThesisInputValidator`.

Generated reference docs and frontend TypeScript types are produced from these schemas:

```bash
scripts/generate-schema-docs
scripts/generate-web-types
```

Use `--check` in CI or before committing to fail on stale generated files.

Backend schema validation attaches a `versionReport` for `ThesisDocument`, `ThesisFormatSpec`, and `TemplatePackage` inputs. CLI JSON output and service facade results use the same report shape so consumers can inspect supported, current, old, future, missing, malformed, and unsupported version states without parsing message text. The contract fixes `checks[].direction` to `current`, `supported`, `old`, `future`, `missing`, `unsupported`, or `unknown`; unsupported versions also appear in `versionReport.diagnostics[]`.

Machine-readable reports now carry `reportVersion: "1.0.0"` at the root when they are produced by Core facade/report contracts. Template quality reports (`template gate`, `template diagnose`, `template authoring-report`, and `ci quality-report`) preserve their existing root report shape and include `versionReport` as an additive machine-readable field. Consumers should treat `schemas/version-report.schema.json` as the canonical nested contract.

The document schema uses `type` discriminators for block and inline nodes. Equation blocks constrain `sourceType` to `omml`, `latex`, or `plain`; advanced table fields constrain width type, border style, vertical merge values, and basic numeric ranges.

`DocumentOverrides` is not part of `ThesisDocument`. It is an envelope-level formatting delta that the API can persist and the renderer can merge over a resolved `ThesisFormatSpec` before creating DOCX output.

The template schema constrains `id`, semver, inheritance, relative paths, variable types, asset types, target section types, insert positions, and layout block discriminators. Asset and `formatSpecRef` paths must be relative to the template directory.

Run validation through the CLI:

```bash
dotnet run --project src/ThesisDocx.Cli -- validate-input \
  --document examples/simple-thesis/document.json \
  --format examples/format-specs/basic-cn-thesis.json
```

Template regression suites define deterministic render/check/baseline cases. They require relative paths in committed examples so the suite can move with the repository.

Requirement capture schemas intentionally allow only short evidence fields and relative source paths. They are for human review and mapping, not AI-only parsing.

Baseline manifests also require relative artifact paths and layout thresholds between `0` and `1`. `baseline update` requires a human reason even when the schema is valid.

Negative fixture manifests describe expected failures. A passing negative fixture suite means the expected errors were detected.

Fix hint rules must link to at least one docs file or example fixture so every suggestion is reviewable.

Onboarding workspace manifests require relative paths and reject `../` traversal. Real institution workspaces should live outside `examples/`; the committed example is fictional.

Template pilot package manifests list included files and SHA-256 checksums. Package validation also rejects source documents and font binaries.

DOCX extraction schemas cover paragraphs, runs, tables, figures, notes, fields, bookmarks, style usage, numbering usage, section summaries, effective formatting, format chaos, format clusters, candidate roles, and stable evidence paths. Extraction evidence may contain user thesis text and should stay in an ignored onboarding workspace. Format candidate reports are review artifacts; generated fields must be accepted, modified, or rejected in a human decision file before becoming a proposed template copy.
