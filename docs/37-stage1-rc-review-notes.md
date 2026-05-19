# Stage 1 RC Review Notes

Use this as the pull request body for `stage1-rc`.

## PR Title

Stage 1 RC: deterministic DOCX rendering, overrides, template gates, and pilot evidence

## Summary

This branch turns the Stage 1 implementation into a release candidate for the supported contract:

`ThesisDocument + ThesisFormatSpec/TemplatePackage + optional DocumentOverrides -> valid, format-conformant DOCX`

The branch does not add AI parsing to the deterministic renderer contract, semantic rewriting, Word automation, screenshot diffing, or Word-like free layout controls. Formatting still flows through `ThesisFormatSpec`, `TemplatePackage`, documented defaults, and the bounded `DocumentOverrides` layer. Codex-assisted DOCX intake review remains an explicit private-workspace prototype step with evidence and content-preservation gates.

## Review Order

1. `bdc06c5 docs: define stage1 rc completion plan`
2. `86e4f4d schema: generate docs and web types`
3. `0961273 rendering: support overrides and thesis format pain points`
4. `f3fcbb5 web: persist overrides and preview effective format`
5. `06c96cd intake: generate reviewed format candidates`
6. `57593b6 templates: add humanities regression and public-source pilot`
7. `2a78eac ci: add stage1 release gate`
8. `535d21a chore: keep local public-source binaries ignored`

## Main Changes

- Adds generated schema documentation and generated Web type surfaces.
- Makes `DocumentOverrides` a persisted API/Web/render input with validation and effective-format preview evidence.
- Extends renderer coverage for exact line spacing, note styles, table cell nested blocks, page-template rules, TOC scoping, section-specific headers/footers, and page-number overrides.
- Adds DOCX effective-format extraction, format chaos analysis, candidate format reports, candidate decision scaffolding, and reviewed template proposal tooling.
- Adds humanities template/regression coverage and a public-source onboarding example with privacy/package gates.
- Adds unified Stage 1 release gate and Web CI redline, including Playwright e2e.
- Keeps committed examples free of source DOCX/PDF/font binaries. Public-source source-document directories commit only review notes and metadata unless explicitly allowed and scanned.

## Acceptance Evidence

Command run from repo root:

```bash
scripts/stage1-release-gate
```

Observed result:

- `dotnet build ThesisDocx.slnx`: passed with 0 warnings and 0 errors.
- `dotnet test ThesisDocx.slnx`: 643 passed, 0 failed, 0 skipped.
- `npm --prefix web run typecheck`: passed.
- `npm --prefix web test`: 13 files passed, 75 tests passed.
- `npm --prefix web run build`: passed.
- `npm --prefix web run e2e`: 8 passed.
- `scripts/generate-schema-docs --check`: generated output is up to date.
- `scripts/generate-web-types --check`: generated output is up to date.
- CLI render and validate example DOCX outputs: passed.
- Template regression, baseline compare, template gate, diagnostics, and authoring report: passed.
- Public-source privacy scans: passed.
- Public-source onboarding package validation: passed.
- Negative fixtures: 24 cases passed.
- Example onboarding package validation: passed.
- Final CI quality report: `status: pass`, `merge decision: approve`, `quality score: 100`.

## Reviewer Checklist

- Confirm renderer logic has no school-specific hardcoding.
- Confirm every new formatting behavior is driven by `ThesisFormatSpec`, `TemplatePackage`, `DocumentOverrides`, or documented defaults.
- Confirm new renderer behavior is covered by OpenXML XML-level tests, not file-existence tests.
- Confirm `DocumentOverrides` remains bounded and does not expose arbitrary Word-like layout controls in the Web editor.
- Confirm generated docs/types are reproducible and checked by CI.
- Confirm baseline changes are tied to renderer/template behavior in this branch.
- Confirm public-source onboarding manifests include attestations and privacy/package validation.
- Confirm no source DOCX/PDF/font binaries are tracked.
- Confirm intake candidate tooling records uncertainty and review decisions instead of silently applying inferred formats.
- Confirm Web export and render paths call Core validation and do not bypass renderer contracts.

## Privacy And Artifact Notes

- `git ls-files | rg '\.(docx|doc|pdf|wps|ttf|otf|woff|woff2)$'` returns no tracked files.
- `.gitignore` still ignores generated DOCX/PDF broadly.
- `.gitignore` keeps the local SHNU public-source source DOCX/PDF ignored while preserving the documented allow-list pattern required for attested public-source examples.
- CI artifacts are generated under `out/ci` and remain untracked.

## Known Non-Blocking Output

- npm prints `Unknown user config "python"` warnings in this environment.
- Playwright worker processes print `NO_COLOR` and `FORCE_COLOR` environment warnings.
- These warnings did not affect the release gate result.

## Merge Recommendation

Merge when reviewers are satisfied with the bounded override contract, public-source/privacy treatment, and baseline drift rationale. After merge, tag the candidate as `stage1-rc.1` and use it for private pilot package review.
