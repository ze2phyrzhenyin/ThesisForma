# Template Diff And Coverage

## Template Diff

`TemplateDiffEngine` compares resolved `ThesisFormatSpec` objects. It does not compare raw `template.json` files and it is not a visual DOCX diff.

Each change includes:

- `path`
- `changeType`: `added`, `removed`, or `modified`
- `baseValue`
- `targetValue`
- `category`
- `severity`

Categories are inferred from spec paths, including page setup, fonts, paragraph, headings, header/footer, TOC, tables, figures, equations, and bibliography.

Run:

```bash
dotnet run --project src/ThesisDocx.Cli -- template diff \
  --base examples/templates/example-university-engineering \
  --target examples/templates/example-university-engineering-variant \
  --json
```

## Coverage Matrix

`FormatRuleCoverageReporter` answers a conservative engineering question: whether a rule category is represented by schema, renderer, validator, tests, and inspect output.

Statuses:

- `supported`: implemented across the current pipeline.
- `partial`: an explicit bounded subset is implemented.
- `planned`: known but not implemented.
- `unsupported`: intentionally not supported.

Run:

```bash
dotnet run --project src/ThesisDocx.Cli -- template coverage \
  --template examples/templates/example-university-engineering \
  --out out/template.coverage.json
```

Coverage is not a conformance certificate for a real university. It is an internal matrix for avoiding accidental claims about unimplemented rules.

Authoring reports include the coverage ratio as one checklist input. A high coverage ratio means the renderer pipeline has checks for the rule classes; it does not mean a real college requirement was interpreted correctly.

## Relation To DOCX Diff

Template diff compares resolved rules. `DocxStructureDiffEngine` compares generated DOCX structure. Layout signature comparison compares extracted OpenXML layout summaries. None of these tools are screenshot-level visual diff.
