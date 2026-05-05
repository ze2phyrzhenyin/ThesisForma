# Format Fixtures

Format fixtures live under `examples/format-fixtures`.

Current fixture groups:

- `pagination-edge-cases`
- `header-footer-edge-cases`
- `heading-numbering-edge-cases`
- `table-edge-cases`
- `figure-equation-crossref-edge-cases`
- `bibliography-citation-edge-cases`
- `cover-declaration-edge-cases`

Each fixture contains `document.json`, either `format-spec.json` or `template.json`, and a README. Tests render every fixture, run validation, and extract layout signatures.

Fixtures are regression assets. They should be small enough to diagnose but broad enough to cover one class of formatting behavior.

`examples/format-fixtures/baselines` stores a baseline manifest plus layout signatures and normalized snapshots for all seven fixture groups. Use:

```bash
dotnet run --project src/ThesisDocx.Cli -- baseline compare \
  --fixtures examples/format-fixtures \
  --out out/format-fixtures-baseline-compare.json
```

Failed fixture baseline reports include the fixture id so the owner can inspect the focused README and artifact.
