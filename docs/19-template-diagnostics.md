# Template Diagnostics

`DiagnosticReport` turns validator, gate, regression, baseline, diff, layout, and requirement-mapping failures into issues a template author can locate and review.

Each issue records:

- source;
- category;
- severity;
- part/path/spec/template location when known;
- expected and actual values when available;
- evidence;
- fix hints;
- related docs and fixtures.

`FixHintEngine` suggests likely spec paths and review actions. A hint is guidance, not an automatic fix and not a mandate to change a template without review.

## CLI

```bash
dotnet run --project src/ThesisDocx.Cli -- template diagnose \
  --template examples/templates/example-university-engineering \
  --document examples/full-thesis/document.json \
  --requirements examples/requirements/example-engineering-requirements.json \
  --suite examples/template-regression/template-regression-suite.json \
  --out out/template-diagnostic-report.json
```

The command runs gate, regression, baseline compare, requirement mapping, and fix hint generation. It returns non-zero when breaking issues remain.

Add `--markdown out/template-diagnostic-report.md` to generate a compact PR-review report. JSON remains the machine-readable contract; Markdown is for humans.
