# Template Gate

`TemplateGateService` is the quality gate for bringing a candidate template into the repository.

Checks:

- template validation;
- resolved format spec schema validation;
- document input validation;
- render success;
- OpenXML validation;
- format conformance validation;
- custom template properties;
- coverage threshold;
- layout signature generation;
- snapshot generation;
- forbidden asset extensions;
- recorded limitations.

CLI:

```bash
dotnet run --project src/ThesisDocx.Cli -- template gate \
  --template examples/templates/example-university-engineering \
  --document examples/full-thesis/document.json \
  --out out/template-gate-report.json
```

Gate status is `pass`, `passWithWarnings`, or `fail`. Passing the gate means the template is structurally usable by this renderer. It does not guarantee that a real college rule has been interpreted correctly; human review is required.

Gate reports include diagnostics, fix hints, checklist entries, next actions, and artifact paths. These are designed to make a failing template actionable instead of returning a single opaque error.
