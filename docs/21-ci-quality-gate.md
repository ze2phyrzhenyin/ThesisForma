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

The policy is the source of warning thresholds and narrow warning suppressions. The fictional example keeps `maxWarningCount` at `0`, suppresses known generated-artifact warnings, and whitelists only exact example fixture path prefixes that are intentionally noisy in repository-wide scans:

- `onboarding/example-engineering-pilot/artifacts/`
- `onboarding/example-engineering-pilot/reports/`
- `artifacts/`
- `reports/`
- `requirements/example-engineering-requirements-invalid.json`
- `template-regression/template-regression-suite.json`

The repository-relative prefixes cover `privacy scan --path examples`; the workspace-relative prefixes cover `onboarding package` and `onboarding summary` when they scan a copied workspace. Any new unsuppressed privacy warning fails the gate through `privacy.warningThreshold.exceeded`. Personal-data warnings remain non-suppressible, so the whitelist cannot hide likely emails, phone numbers, identity ids, or student ids. Do not add broad suppressions for real pilot material; move source files into private onboarding workspaces instead.
