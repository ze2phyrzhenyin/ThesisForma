# Component 02: Template To Finished DOCX Closed Loop

## Goal

Reduce product risk by proving that declarative templates produce valid DOCX outputs across more thesis-format variants.

## Owners

- Template worker for templates and template schemas.
- Renderer worker for any missing declarative rendering behavior.
- Validation/regression worker for gates, baselines, and snapshots.
- Onboarding/privacy worker for real pilot workspace discipline.

## Write Scope

- `examples/templates`
- `examples/format-fixtures`
- `examples/template-regression`
- `examples/onboarding` for fictional examples only
- `docs/16-real-college-template-onboarding.md`
- `docs/29-first-real-college-pilot.md`
- `src/ThesisDocx.Core/Templates`
- `tests/ThesisDocx.Tests`

## Required Behavior

- Add at least one new fictional template package that differs meaningfully from existing templates.
- Add a full render fixture document for that template.
- Add template regression suite coverage.
- Add format fixture baseline or layout signature coverage when a new rule surface is introduced.
- Keep real college pilots in ignored `onboarding-workspaces/`, never in `examples/`.

## Acceptance Gates

```bash
dotnet run --project src/ThesisDocx.Cli -- template validate --template examples/templates/<template-id>
dotnet run --project src/ThesisDocx.Cli -- render --document examples/full-thesis/document.json --template examples/templates/<template-id> --out out/<template-id>.docx
dotnet run --project src/ThesisDocx.Cli -- validate --docx out/<template-id>.docx --template examples/templates/<template-id>
dotnet run --project src/ThesisDocx.Cli -- template gate --template examples/templates/<template-id> --document examples/full-thesis/document.json --out out/<template-id>.gate.json
scripts/ci-quality-gate
```

Required test evidence:

- template schema validation;
- resolved spec inspection;
- render output OpenXML validation;
- gate report with no blocking issues;
- privacy scan clean for committed examples.

## Boundaries

- Do not commit real college PDFs, DOCX files, screenshots, long excerpts, or generated DOCX artifacts.
- Do not claim real-college compliance without human review.

