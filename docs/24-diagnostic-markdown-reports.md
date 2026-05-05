# Diagnostic Markdown Reports

`template diagnose` writes machine-readable JSON and can also write a compact Markdown report:

```bash
dotnet run --project src/ThesisDocx.Cli -- template diagnose \
  --template examples/templates/example-university-engineering \
  --document examples/full-thesis/document.json \
  --requirements examples/requirements/example-engineering-requirements.json \
  --suite examples/template-regression/template-regression-suite.json \
  --out out/template-diagnostic-report.json \
  --markdown out/template-diagnostic-report.md
```

Markdown is intended for PR comments and template author review. It prioritizes status, blocking issues, top actions, and first fix hints. The JSON report remains the stable machine contract.
