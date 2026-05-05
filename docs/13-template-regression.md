# Template Regression

Template regression suites live under `examples/template-regression`.

`TemplateRegressionRunner` executes each case:

1. render DOCX;
2. run `OpenXmlValidator`;
3. run `FormatConformanceValidator`;
4. write inspect JSON;
5. extract layout signature;
6. compare layout baseline;
7. compare normalized snapshot baseline;
8. check required custom properties and parts;
9. emit a deterministic batch report.

Schema: `schemas/template-regression-suite.schema.json`.

CLI:

```bash
dotnet run --project src/ThesisDocx.Cli -- template regression \
  --suite examples/template-regression/template-regression-suite.json \
  --out out/template-regression-report.json
```

Regression baselines are OpenXML structure artifacts. They are not screenshots and do not require Microsoft Word.

The report includes:

- `failedCases`;
- `caseDiagnostics`;
- `baselineSummary`;
- `nextActions`.

Use `baseline compare` for explicit baseline checks and `baseline update --reason` only after review.
