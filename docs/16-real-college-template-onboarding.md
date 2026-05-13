# Real College Template Onboarding

This project can model real college requirements as data, but real templates must be reviewed manually.

## Process

1. Convert requirements into `ThesisFormatSpec` and optionally `TemplatePackage`.
2. Create a `RequirementCapture` file with short evidence references.
3. Map approved requirements to `ThesisFormatSpec` / `TemplatePackage` paths.
4. Use a neutral template id and avoid hardcoding the school in renderer code.
5. Add a fixture document that exercises the rules.
6. Run schema validation and semantic input validation.
7. Render DOCX and run OpenXML validation.
8. Run format conformance validation.
9. Extract layout signature and initialize a baseline.
10. Add a template regression case.
11. Run template regression and baseline compare.
12. Run template gate.
13. Run template diagnose.
14. Generate `template authoring-report`.
15. Have a human reviewer compare the source requirement to the encoded rules.

Before using a real college source, rehearse the same closed loop with a fictional template such as `examples/templates/example-university-humanities`: template validation, render, format validation, regression baseline, baseline compare, and gate. This keeps new renderer/schema work out of private pilot evidence.

## Boundaries

- No AI parsing is implemented in Stage 1.
- No Microsoft Word automation is used.
- No screenshot or pixel-level visual diff is used.
- Diff and gate reports are quality gates, not legal or academic certification.
- Diagnostic reports and fix hints help locate issues but do not automatically repair a template.
## Private Pilot Workspace

Current real-college pilots should start in an `OnboardingWorkspace`, usually under `onboarding-workspaces/<slug>/`.
Do not put private source PDFs, DOCX manuals, author names, student ids, or long evidence excerpts under `examples/`. Public-source real examples are allowed only with `publicSourceExample` manifest attestations and a passing privacy scan.

Use the onboarding CLI:

```bash
dotnet run --project src/ThesisDocx.Cli -- onboarding init --workspace onboarding-workspaces/pilot-example --school "Example University" --college "Example Engineering College" --degree-type master --locale zh-CN
dotnet run --project src/ThesisDocx.Cli -- onboarding summary --workspace onboarding-workspaces/pilot-example --out out/onboarding.summary.json --markdown out/onboarding.summary.md
dotnet run --project src/ThesisDocx.Cli -- onboarding package --workspace onboarding-workspaces/pilot-example --out out/pilot-example.template-pilot.zip
```

The pilot package is an audit artifact; it excludes source documents and must pass `onboarding package-validate` before review.
