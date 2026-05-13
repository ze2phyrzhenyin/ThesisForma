# Baseline Management

`TemplateBaselineManager` manages regression baselines for template suites and format fixtures. Baselines are structural artifacts:

- normalized snapshot text;
- layout signature JSON;
- inspect JSON;
- a baseline manifest.

They are not visual screenshots and do not require Microsoft Word.

## Rules

- Baseline paths must be relative.
- `baseline update` requires `--reason`.
- Baselines are deterministic; generated timestamps use a stable value in committed examples.
- A failed compare reports case id, fixture id when available, diff category, path, expected value, and actual value.
- Do not silently overwrite baselines.

## CLI

```bash
dotnet run --project src/ThesisDocx.Cli -- baseline list \
  --suite examples/template-regression/template-regression-suite.json

dotnet run --project src/ThesisDocx.Cli -- baseline init \
  --suite examples/template-regression/template-regression-suite.json \
  --out examples/template-regression/baselines/baseline-manifest.json

dotnet run --project src/ThesisDocx.Cli -- baseline compare \
  --suite examples/template-regression/template-regression-suite.json \
  --out out/baseline-compare-report.json

dotnet run --project src/ThesisDocx.Cli -- baseline compare \
  --fixtures examples/format-fixtures \
  --out out/format-fixtures-baseline-compare.json

dotnet run --project src/ThesisDocx.Cli -- baseline init \
  --fixtures examples/format-fixtures \
  --out examples/format-fixtures/baselines/format-fixture-baseline-manifest.json
```

Only update a baseline after a reviewer accepts the rule change:

```bash
dotnet run --project src/ThesisDocx.Cli -- baseline update \
  --suite examples/template-regression/template-regression-suite.json \
  --case example-university-engineering-full \
  --reason "Accepted intentional heading spacing change" \
  --out out/baseline-update-report.json
```
