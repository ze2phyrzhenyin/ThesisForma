# Stage 1 Release Candidate

Stage 1 RC means the repository can repeatedly prove this contract:

`ThesisDocument + ThesisFormatSpec/TemplatePackage + optional DocumentOverrides -> valid, format-conformant DOCX`

It still does not parse messy user documents, infer structure with AI, or rewrite thesis content.

## Review Split

Use this order when splitting the current work into reviewable commits or PRs:

1. DocumentOverrides contract: schema, model, validation, API persistence, Web envelope, renderer merge.
2. Rendering pain points: note styles, rich table-cell blocks, page-template `rule`.
3. Fictional template closed loop: humanities template, regression suite, baseline updates.
4. Generated schema docs and Web generated types.
5. Unified CI: `.github/workflows`, `scripts/web-quality-gate`, e2e in the red line.
6. Maintainability split: CLI command group, service contracts, Web contract modules, override form controls.
7. Web bundle shape: lazy panels, manual chunks, bundle threshold and e2e coverage.
8. Release evidence: format preview endpoint/UI, RC gate script, pilot playbooks.

Each PR should include its focused test evidence. Do not mix baseline updates with unrelated behavior changes unless the baseline drift is caused by that same PR.

## RC Gate

Run:

```bash
scripts/stage1-release-gate
```

The gate runs whitespace checks, generated schema checks, .NET tests, Web quality gates, and the aggregate CI quality report. A release candidate is not accepted unless this script exits zero.

## Required Evidence

- `dotnet test tests/ThesisDocx.Tests/ThesisDocx.Tests.csproj`
- `npm --prefix web run typecheck`
- `npm --prefix web test`
- `npm --prefix web run build`
- `npm --prefix web run e2e`
- `scripts/ci-quality-gate`
- No local absolute paths in API success payloads.
- No private real institution source DOCX/PDF/font binaries in the repository; any public-source real institution example has manifest attestations and passes privacy scan.
- Baseline update reasons are recorded for template and format fixture drift.

## Exit Criteria

- OpenXML XML-level tests cover every new renderer feature.
- Generated docs/types are up to date.
- Template regression and baseline compare pass for fictional fixtures.
- Web editor can save, validate, preview effective format, render through API, and export JSON.
- Private pilot packages pass privacy scan and package validation before review.
