# Template System

`TemplatePackage` makes format rules reusable without moving college-specific behavior into renderer code.

The data flow is:

```text
TemplatePackage
-> TemplateResolver
-> resolved ThesisFormatSpec + variables + assets + pageTemplates
-> DocxRenderer
```

## Package Contents

A template package includes:

- `templateSchemaVersion`: currently `1.0.0`;
- identity metadata: `id`, `name`, `version`, `locale`, `school`, `college`, `degreeType`, `tags`;
- optional `extends`;
- `formatSpec` or `formatSpecRef`;
- `variables`;
- `assets`;
- `pageTemplates`;
- `complianceRules`;
- notes.

Examples live under `examples/templates`. The Example University templates are fictional fixtures.

## Inheritance

`TemplateResolver` recursively resolves `extends.templateId` among sibling template directories. It detects circular inheritance and returns structured errors.

Merge rules:

- object fields merge recursively;
- child scalar values override parent values;
- arrays replace parent arrays by default;
- `null` is an explicit clear when the result remains usable;
- inherited assets keep their original base directory;
- final resolved specs are deterministic and serializable.

## Variables

Variable priority:

```text
CLI --var / --vars
> ThesisDocument.metadata sourcePath
> TemplateVariable.defaultValue
```

Supported placeholders include:

- `{{metadata.title}}`
- `{{metadata.author}}`
- `{{variables.defenseDate}}`
- `{{template.school}}`
- `{{template.college}}`
- `{{date:yyyy-MM-dd}}`

Missing required variables are errors. Missing optional variables become warnings and render as empty text.

## Assets

Assets must use relative paths. Image assets can be rendered in page templates. Font assets are metadata-only in this round; the renderer does not embed or distribute font files.

## CLI

```bash
dotnet run --project src/ThesisDocx.Cli -- template list --templates examples/templates
dotnet run --project src/ThesisDocx.Cli -- template validate --template examples/templates/example-university-engineering
dotnet run --project src/ThesisDocx.Cli -- template resolve --template examples/templates/example-university-engineering --out out/resolved-format-spec.json
```

Render with a template:

```bash
dotnet run --project src/ThesisDocx.Cli -- render \
  --document examples/full-thesis/document.json \
  --template examples/templates/example-university-engineering \
  --var variables.defenseDate=2026-06-01 \
  --out out/template-full.docx
```

`--format` and `--template` are mutually exclusive.

## Regression And Gate

Template regression suites live in `examples/template-regression`. A suite renders one or more cases and checks OpenXML validity, format conformance, inspect output, layout signatures, snapshot baselines, custom properties, and required package parts.

Template gate is the onboarding quality gate for a candidate template. It checks template validation, resolved format spec validation, input validation, render, OpenXML, format conformance, custom properties, coverage ratio, layout signature generation, snapshot generation, forbidden assets, and recorded limitations.

These checks do not prove a real college rule has been correctly interpreted. Real college templates still require human review.
