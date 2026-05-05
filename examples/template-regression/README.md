# Template Regression Example

This suite renders the fictional Example University Engineering template, validates the DOCX, extracts inspect/layout data, compares the layout signature to a baseline, and compares the normalized snapshot to a baseline.

Run:

```bash
dotnet run --project src/ThesisDocx.Cli -- template regression \
  --suite examples/template-regression/template-regression-suite.json \
  --out out/template-regression-report.json
```

The baselines are structural OpenXML artifacts. They are not screenshots and do not depend on Microsoft Word.

Manage committed baselines with:

```bash
dotnet run --project src/ThesisDocx.Cli -- baseline list \
  --suite examples/template-regression/template-regression-suite.json

dotnet run --project src/ThesisDocx.Cli -- baseline compare \
  --suite examples/template-regression/template-regression-suite.json \
  --out out/baseline-compare-report.json
```

Use `baseline update --reason` only after a reviewer accepts an intentional change.
