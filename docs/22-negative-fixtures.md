# Negative Fixtures

Negative fixtures are expected failures. They prove that validators, diagnostic reports, and fix hints identify known template-author mistakes.

Manifest:

```text
examples/negative-fixtures/negative-fixture-manifest.json
```

Run:

```bash
dotnet run --project src/ThesisDocx.Cli -- negative-fixtures run \
  --manifest examples/negative-fixtures/negative-fixture-manifest.json \
  --out out/negative-fixtures-report.json
```

The command passes only when every expected failure code, severity, and fix hint appears. If a negative fixture unexpectedly succeeds, the runner fails.

Negative fixtures are not bugs. They are regression assets for failure diagnostics.
