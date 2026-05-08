# ThesisForma

ThesisForma is the product name for this repository. The existing .NET projects keep the `ThesisDocx.*` names for compatibility with the rendering engine.

ThesisDocx is a Stage 1 deterministic DOCX rendering engine for graduation thesis formatting.

The current boundary is:

`ThesisDocument + ThesisFormatSpec -> valid DOCX`

The fourth-round engine also accepts reusable template packages:

`ThesisDocument + TemplatePackage -> resolved ThesisFormatSpec + page templates + valid DOCX`

This stage does not infer structure with AI or rewrite thesis content. The repository includes a DOCX intake prototype, but the supported contract remains structured content plus template data feeding the deterministic renderer.

## Why This Shape

DOCX files are OpenXML packages made of WordprocessingML parts. Treating thesis content and thesis formatting as structured data gives us a stable contract:

- `ThesisDocument` describes content and structure.
- `ThesisFormatSpec` describes college formatting rules.
- `DocxRenderer` writes explicit OpenXML parts.
- validators and tests inspect XML rather than trusting Word to repair layout.

This keeps college-specific rules declarative and keeps renderer code reusable.

## Quick Start

```bash
dotnet build ThesisDocx.slnx
dotnet test ThesisDocx.slnx
```

Render the sample thesis:

```bash
dotnet run --project src/ThesisDocx.Cli -- render \
  --document examples/simple-thesis/document.json \
  --format examples/format-specs/basic-cn-thesis.json \
  --out out/simple.docx
```

Validate structured input:

```bash
dotnet run --project src/ThesisDocx.Cli -- validate-input \
  --document examples/simple-thesis/document.json \
  --format examples/format-specs/basic-cn-thesis.json
```

Validate and inspect:

```bash
dotnet run --project src/ThesisDocx.Cli -- validate \
  --docx out/simple.docx \
  --format examples/format-specs/basic-cn-thesis.json

dotnet run --project src/ThesisDocx.Cli -- inspect \
  --docx out/simple.docx \
  --out out/simple.inspect.json
```

`render` runs JSON Schema validation and semantic input validation by default. `--skip-input-validation` exists for developer experiments but is not recommended. `validate` also supports `--json` for machine-readable diagnostics.

Use a template package instead of a raw format spec:

```bash
dotnet run --project src/ThesisDocx.Cli -- template list \
  --templates examples/templates

dotnet run --project src/ThesisDocx.Cli -- template validate \
  --template examples/templates/example-university-engineering

dotnet run --project src/ThesisDocx.Cli -- render \
  --document examples/full-thesis/document.json \
  --template examples/templates/example-university-engineering \
  --var variables.defenseDate=2026-06-01 \
  --out out/template-full.docx

dotnet run --project src/ThesisDocx.Cli -- validate \
  --docx out/template-full.docx \
  --template examples/templates/example-university-engineering
```

Template utilities:

```bash
dotnet run --project src/ThesisDocx.Cli -- template inspect --template examples/templates/example-university-engineering
dotnet run --project src/ThesisDocx.Cli -- template resolve --template examples/templates/example-university-engineering --out out/resolved-format-spec.json
dotnet run --project src/ThesisDocx.Cli -- template diff --base examples/templates/example-university-engineering --target examples/templates/example-university-engineering-variant --json
dotnet run --project src/ThesisDocx.Cli -- template coverage --template examples/templates/example-university-engineering --out out/template.coverage.json
```

The scripts set NuGet cache paths inside the repo, which is useful in sandboxed environments:

```bash
scripts/generate-example-docx
scripts/inspect-docx out/simple.docx out/simple.inspect.json
scripts/normalize-docx-for-snapshot out/simple.docx out/simple.snapshot.txt
```

## Example JSON

Minimal document:

```json
{
  "schemaVersion": "1.0.0",
  "metadata": {
    "title": "论文题目",
    "author": "张三",
    "college": "计算机学院",
    "major": "软件工程",
    "studentId": "20260001",
    "advisor": "李四",
    "date": "2026年5月",
    "language": "zh-CN"
  },
  "sections": [
    {
      "kind": "body",
      "blocks": [
        {
          "type": "heading",
          "level": 1,
          "inlines": [{ "type": "text", "text": "绪论" }]
        }
      ]
    }
  ]
}
```

Minimal format rule:

```json
{
  "schemaVersion": "1.0.0",
  "pageSetup": {
    "paperSize": "a4",
    "topMarginCm": 2.54,
    "bottomMarginCm": 2.54,
    "leftMarginCm": 3.0,
    "rightMarginCm": 2.5
  },
  "defaultFont": {
    "eastAsia": "宋体",
    "latin": "Times New Roman",
    "sizePt": 12
  }
}
```

Full examples live in `examples/simple-thesis`, `examples/full-thesis`, `examples/format-specs`, and `examples/templates`.

Formal schemas live in `schemas/` and are validated with the `NJsonSchema` NuGet package. `ThesisDocument` accepts `schemaVersion` `1.0.0` and `1.1.0`; `ThesisFormatSpec` accepts `1.0.0`, `1.1.0`, and `1.2.0`; `TemplatePackage` uses `templateSchemaVersion` `1.0.0`.

## Supported Now

- A4 page setup, margins, header/footer distance.
- Cover/front matter/body section profiles.
- Cover without page number, front matter lower Roman page numbers, body decimal page numbers restarting at 1.
- Default East Asia and Latin fonts.
- body paragraph style, line spacing, spacing before/after, first-line indent, alignment.
- heading 1-3 styles, outline levels, and multilevel numbering.
- header text, header bottom line, footer PAGE field.
- TOC field code.
- normal and three-line tables.
- advanced tables with gridSpan, vMerge, repeat header rows, cantSplit rows, fixed/auto layout, percent/dxa/auto width, table/cell margins, vertical alignment, and cell border overrides.
- image insertion from base64 or path, DrawingML inline drawing, EMU sizing.
- figure/table captions.
- OMML equation blocks from controlled OMML, plain text, or a small LaTeX subset/fallback.
- equation numbering, bookmarks, and `REF` cross references.
- bibliography numbered list with hanging indent.
- bookmarks and REF field cross references.
- real `footnotes.xml` and `endnotes.xml` parts with body references and separators.
- CLI render/validate/inspect/snapshot.
- OpenXmlValidator wrapper, format conformance checks, XML assertions, normalized snapshot tests.
- JSON Schema validation and semantic input validation.
- enhanced inspect output for package parts, styles, numbering, sections, fields, notes, equations, advanced tables, bibliography, and OpenXML error count.
- reusable `TemplatePackage` examples with inheritance and deterministic format-spec merge.
- template variables resolved from CLI values, thesis metadata, and defaults.
- template image assets for cover layouts; font assets are metadata-only.
- cover and declaration page layout DSL rendered as real WordprocessingML.
- template CLI list/inspect/validate/resolve/diff/coverage.
- template-aware validation and custom document properties for renderer/template metadata.
- conservative format rule coverage matrix.
- structured DOCX package/XML diff that ignores volatile OpenXML data.
- layout signature extraction and threshold comparison without Word or screenshots.
- template regression suites and template gate reports for onboarding quality checks.
- requirement capture files for manually reviewed college rules.
- requirement mapping reports against template/spec paths.
- baseline list/init/compare/update for template regression and format fixtures.
- diagnostic reports with fix hints for template authors.
- template authoring reports with publish readiness checklist.
- CI quality gate scripts and aggregate `ci quality-report`.
- negative fixtures for expected failure diagnostics.
- Markdown diagnostic/authoring/CI reports for PR review.
- private onboarding workspaces for real-college pilot intake.
- privacy scan for examples, onboarding workspaces, and pilot packages.
- deterministic template pilot package ZIPs with redacted requirements and checksums.
- onboarding summary, diagnose, gate, authoring-report, and package CLI commands.
- DOCX intake extraction and rule-assisted structure draft generation for second-stage prototypes.

## Not Supported Yet

- AI parsing of messy user documents.
- Web upload UI.
- Word field updating after generation.
- full LaTeX-to-OMML conversion beyond the small safe subset.
- visual diff of rendered DOCX output.
- screenshot-level or pixel-level layout comparison.
- arbitrary page layout positioning engine.
- embedded font distribution or font embedding.
- template marketplace.

## Structural Diff And Gates

```bash
dotnet run --project src/ThesisDocx.Cli -- docx diff \
  --base out/template-full.docx \
  --target out/template-full.docx \
  --json \
  --out out/template-full.self-diff.json

dotnet run --project src/ThesisDocx.Cli -- docx layout-signature \
  --docx out/template-full.docx \
  --out out/template-full.layout.json

dotnet run --project src/ThesisDocx.Cli -- docx layout-compare \
  --base out/template-full.layout.json \
  --target out/template-full.layout.json \
  --threshold 0.99 \
  --out out/template-full.layout-compare.json

dotnet run --project src/ThesisDocx.Cli -- template regression \
  --suite examples/template-regression/template-regression-suite.json \
  --out out/template-regression-report.json

dotnet run --project src/ThesisDocx.Cli -- template gate \
  --template examples/templates/example-university-engineering \
  --document examples/full-thesis/document.json \
  --out out/template-gate-report.json

dotnet run --project src/ThesisDocx.Cli -- requirements validate \
  --requirements examples/requirements/example-engineering-requirements.json

dotnet run --project src/ThesisDocx.Cli -- requirements report \
  --requirements examples/requirements/example-engineering-requirements.json \
  --template examples/templates/example-university-engineering \
  --out out/example-engineering.requirements-report.json

dotnet run --project src/ThesisDocx.Cli -- baseline compare \
  --suite examples/template-regression/template-regression-suite.json \
  --out out/baseline-compare-report.json

dotnet run --project src/ThesisDocx.Cli -- template diagnose \
  --template examples/templates/example-university-engineering \
  --document examples/full-thesis/document.json \
  --requirements examples/requirements/example-engineering-requirements.json \
  --suite examples/template-regression/template-regression-suite.json \
  --out out/template-diagnostic-report.json

dotnet run --project src/ThesisDocx.Cli -- template authoring-report \
  --template examples/templates/example-university-engineering \
  --document examples/full-thesis/document.json \
  --requirements examples/requirements/example-engineering-requirements.json \
  --suite examples/template-regression/template-regression-suite.json \
  --threshold 0.85 \
  --out out/template-authoring-report.json

dotnet run --project src/ThesisDocx.Cli -- negative-fixtures run \
  --manifest examples/negative-fixtures/negative-fixture-manifest.json \
  --out out/negative-fixtures-report.json

dotnet run --project src/ThesisDocx.Cli -- ci quality-report \
  --template examples/templates/example-university-engineering \
  --document examples/full-thesis/document.json \
  --requirements examples/requirements/example-engineering-requirements.json \
  --suite examples/template-regression/template-regression-suite.json \
  --negative-fixtures examples/negative-fixtures/negative-fixture-manifest.json \
  --threshold 0.85 \
  --out out/ci/quality-report.json \
  --markdown out/ci/quality-report.md
```

These tools compare OpenXML structure and extracted layout signatures. They do not call Microsoft Word, do not parse messy documents, do not use AI, and do not guarantee thesis content semantics.

Run the full local gate:

```bash
scripts/ci-quality-gate
```

CI artifacts are written to `out/ci`. The workflow draft is `ci/workflows/quality-gate.yml`; copy it to `.github/workflows/quality-gate.yml` in a GitHub repository.

The template authoring workflow is documented in `docs/17-requirement-capture.md` through `docs/25-template-authoring-ci.md`.

## Onboarding Pilot Workflow

Real college source material belongs in a private workspace, not in `examples/`.

```bash
dotnet run --project src/ThesisDocx.Cli -- onboarding init \
  --workspace onboarding-workspaces/pilot-example \
  --school "Example University" \
  --college "Example Engineering College" \
  --degree-type master \
  --locale zh-CN

dotnet run --project src/ThesisDocx.Cli -- privacy scan \
  --path examples \
  --out out/privacy-scan-examples.json

dotnet run --project src/ThesisDocx.Cli -- onboarding summary \
  --workspace examples/onboarding/example-engineering-pilot \
  --out out/onboarding.summary.json \
  --markdown out/onboarding.summary.md

dotnet run --project src/ThesisDocx.Cli -- onboarding package \
  --workspace examples/onboarding/example-engineering-pilot \
  --out out/example-engineering-pilot.template-pilot.zip

dotnet run --project src/ThesisDocx.Cli -- onboarding package-validate \
  --package out/example-engineering-pilot.template-pilot.zip
```

`examples/onboarding/example-engineering-pilot` is fictional. A real pilot package excludes source PDF/DOCX files, generated DOCX artifacts, system font binaries, absolute paths, and long evidence excerpts. See `docs/26-onboarding-workspace.md` through `docs/29-first-real-college-pilot.md`.

## DOCX Intake Prototype

The second-stage prototype starts from a private workspace and extracts structure evidence from an uploaded DOCX without modifying the file:

```bash
dotnet run --project src/ThesisDocx.Cli -- extract docx \
  --input onboarding-workspaces/docx-structure-pilot/input/input.docx \
  --out onboarding-workspaces/docx-structure-pilot/extraction/extraction.json \
  --text onboarding-workspaces/docx-structure-pilot/extraction/plain-text.txt \
  --markdown onboarding-workspaces/docx-structure-pilot/extraction/extracted.md

dotnet run --project src/ThesisDocx.Cli -- structure draft \
  --extraction onboarding-workspaces/docx-structure-pilot/extraction/extraction.json \
  --out onboarding-workspaces/docx-structure-pilot/structured/thesis-document.draft.json \
  --report onboarding-workspaces/docx-structure-pilot/structured/structure-mapping-report.json \
  --unresolved onboarding-workspaces/docx-structure-pilot/structured/unresolved-items.json

dotnet run --project src/ThesisDocx.Cli -- intake docx \
  --input onboarding-workspaces/docx-structure-pilot/input/input.docx \
  --workspace onboarding-workspaces/docx-structure-pilot \
  --template examples/templates/example-university-engineering
```

Extraction evidence and draft JSON may contain the full user thesis. Keep them in ignored onboarding workspaces and do not copy them into `examples` or docs. See `docs/30-docx-intake-and-structuring.md`.

## Codex Components

This sandbox blocks creating root `.agents` and `.codex` directories. The project keeps a portable mirror in `codex-components/`. In a normal environment run `scripts/install-codex-components` to copy it into `.agents` and `.codex`. See `docs/codex-components.md`.

## Development Rules

Read `AGENTS.md` first. The main rule is that renderer logic must not hardcode a university format. Add format fields to `ThesisFormatSpec`, update examples and docs, then write XML-level tests.
