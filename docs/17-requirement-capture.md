# Requirement Capture

`RequirementCapture` is the manually reviewed record of a college thesis format requirement. It is not an AI parser output. Future AI tooling may draft this file, but a template author must review the evidence, normalized value, confidence, mapping, and approval fields.

The schema is `schemas/requirement-capture.schema.json`; the example is `examples/requirements/example-engineering-requirements.json`.

## Workflow

1. Record source documents with relative paths only.
2. Add short evidence references: brief quote, page, section, or screenshot placeholder.
3. Normalize each rule into a concise value.
4. Map approved rules to `ThesisFormatSpec` or `TemplatePackage` paths.
5. Mark unsupported rules as `notSupported` with notes.
6. Run `requirements validate` and `requirements report`.

Do not paste long copyrighted source text into evidence. The file is an audit trail for human review, not a replacement for the source document.

## CLI

```bash
dotnet run --project src/ThesisDocx.Cli -- requirements validate \
  --requirements examples/requirements/example-engineering-requirements.json

dotnet run --project src/ThesisDocx.Cli -- requirements report \
  --requirements examples/requirements/example-engineering-requirements.json \
  --template examples/templates/example-university-engineering \
  --out out/example-engineering.requirements-report.json
```

The report groups coverage by category and lists blocking issues and recommended next actions.
