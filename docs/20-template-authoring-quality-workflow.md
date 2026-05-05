# Template Authoring Quality Workflow

`TemplateAuthoringReport` is the CLI quality workbench summary for a candidate college template. It combines requirement mapping, gate, regression, baseline, coverage, diagnostics, and a publish checklist.

## Real College Onboarding Flow

1. Collect the source format requirements.
2. Create a `RequirementCapture` file with short evidence references.
3. Map approved requirements to `TemplatePackage` and `ThesisFormatSpec` paths.
4. Create or update format fixtures for edge cases.
5. Initialize baselines.
6. Run template regression.
7. Run template gate.
8. Run template diagnose.
9. Generate an authoring report.
10. Perform human review.
11. Publish only after the report is `ready` or an accepted `readyWithWarnings`.

## CLI

```bash
dotnet run --project src/ThesisDocx.Cli -- template authoring-report \
  --template examples/templates/example-university-engineering \
  --document examples/full-thesis/document.json \
  --requirements examples/requirements/example-engineering-requirements.json \
  --suite examples/template-regression/template-regression-suite.json \
  --threshold 0.85 \
  --out out/template-authoring-report.json
```

The report does not prove semantic correctness of thesis content and does not replace human interpretation of a real college rule.

The report includes a conservative `qualityScore` and `suggestedMergeDecision`. These are engineering gate signals for review, not visual scores and not automatic approval.
## Onboarding Extension

For a real-college pilot, run the quality workflow inside an onboarding workspace:

1. capture requirements manually;
2. scaffold a template from an existing base template;
3. scaffold redacted fixtures;
4. initialize baselines with a reason;
5. run gate, diagnose, authoring-report, privacy scan, and package validation.

The onboarding summary combines privacy, requirement, template, fixture, baseline, gate, diagnostic, and authoring status into a release-readiness decision.
