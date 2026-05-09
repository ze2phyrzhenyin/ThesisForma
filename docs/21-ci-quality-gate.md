# CI Quality Gate

The CI quality gate runs the template quality chain, not only `dotnet test`.

Local command:

```bash
scripts/ci-quality-gate
```

Artifact directory:

```text
out/ci
```

For local runs that should not write into the repository output tree, set `CI_OUT_DIR` or pass `--out`:

```bash
CI_OUT_DIR=/tmp/thesisforma-ci scripts/ci-quality-gate
scripts/ci-quality-gate --out /tmp/thesisforma-ci
```

The gate runs build, tests, schema/input checks, example rendering, template validation, requirement reports, baseline compare, template regression, template gate, template diagnose, template authoring report, negative fixtures, and the aggregate `ci quality-report`.

The workflow draft is in `ci/workflows/quality-gate.yml`. In a GitHub repository, copy it to `.github/workflows/quality-gate.yml`.

The gate does not call Microsoft Word, does not do screenshot diff, and does not verify thesis semantics.
## Onboarding Checks

`scripts/ci-quality-gate` now also runs:

- `privacy scan --path examples`
- `onboarding validate`
- `onboarding summary`
- `onboarding package`
- `onboarding package-validate`

The default CI artifact directory remains `out/ci`. The onboarding checks use the fictional `examples/onboarding/example-engineering-pilot` workspace and verify that the real-college pilot workflow remains executable without committing private source documents.

## Privacy Policy

The privacy scan in `scripts/ci-quality-gate` reads the onboarding workspace policy:

```bash
dotnet run --project src/ThesisDocx.Cli -- privacy scan \
  --path examples \
  --policy examples/onboarding/example-engineering-pilot/onboarding.json \
  --out "$OUT_DIR/privacy-scan-examples.json"
```

The policy is the source of warning thresholds and narrow warning suppressions. The fictional example currently allows up to 25 warnings and suppresses known generated-artifact warnings while keeping personal-data warnings non-suppressible. Do not add broad suppressions for real pilot material; move source files into private onboarding workspaces instead.
