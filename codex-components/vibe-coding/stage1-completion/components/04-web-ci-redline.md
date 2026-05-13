# Component 04: Unified Web CI Redline

## Goal

Make web quality part of the same required gate as Core, CLI, API, templates, privacy, and onboarding.

## Owners

- CI-quality worker.
- Web editor worker for any test/build fixes.

## Write Scope

- `scripts/ci-quality-gate`
- `scripts/web-quality-gate`
- `ci/workflows/quality-gate.yml`
- `.github/workflows/quality-gate.yml`
- `web/playwright.config.ts`
- `web/README.md`

## Required Behavior

- CI installs Node dependencies reproducibly.
- CI runs web typecheck, unit tests, production build, and Playwright e2e.
- Web artifacts remain outside `out/ci` unless explicitly captured as reports.
- Local fast iteration may set `WEB_E2E=0`, but final acceptance uses e2e.

## Acceptance Gates

```bash
npm --prefix web run typecheck
npm --prefix web test
npm --prefix web run build
npm --prefix web run e2e
scripts/ci-quality-gate
```

Required test evidence:

- workflow includes Node setup and npm cache;
- `scripts/ci-quality-gate` invokes `scripts/web-quality-gate`;
- e2e runs against the isolated Playwright server, not an arbitrary existing local port.

## Boundaries

- Do not reduce CI to `dotnet test`.
- Do not let CI artifacts escape `out/ci` except standard npm/Vite build directories ignored by git.

