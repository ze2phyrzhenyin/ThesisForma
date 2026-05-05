---
name: ci-quality-gate
description: Use when work involves scripts/ci-quality-gate, CI workflow drafts, out/ci artifacts, ci quality-report, or template quality gates in CI.
---

# CI Quality Gate

CI must run more than `dotnet test`.

Rules:

- Keep artifacts under `out/ci`.
- Run build, test, schema checks, example render/validate, requirements, baseline compare, template regression, gate, diagnose, authoring report, and negative fixtures.
- Scripts should be bash plus dotnet and avoid machine-specific absolute paths.
- Failing quality checks must return non-zero.
- Workflow drafts should upload `out/ci` artifacts.
