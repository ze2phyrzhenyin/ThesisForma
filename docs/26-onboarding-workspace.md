# Onboarding Workspace

`OnboardingWorkspace` is a private pilot workspace for turning one institution's manually reviewed formatting requirements into a template candidate.

The workspace may contain sensitive source files and review notes, so real pilots should live under `onboarding-workspaces/<slug>/` or another private directory. The repository `examples/onboarding/example-engineering-pilot` is fictional and exists only to test the workflow.

Typical layout:

- `onboarding.json`: workspace manifest and privacy/quality policy.
- `source-documents/`: private PDFs/DOCX/manual files, not committed.
- `requirements/requirements.json`: manually entered `RequirementCapture`.
- `template/`: pilot `TemplatePackage` and `format-spec.json`.
- `fixtures/`: redacted structured thesis fixtures.
- `baselines/`: pilot baseline manifest and layout/snapshot baselines.
- `reports/`: gate, diagnostic, authoring, and summary reports.
- `artifacts/`: generated DOCX, inspect JSON, layout signatures, and snapshots.

CLI:

```bash
dotnet run --project src/ThesisDocx.Cli -- onboarding init \
  --workspace onboarding-workspaces/pilot-example \
  --school "Example University" \
  --college "Example Engineering College" \
  --degree-type master \
  --locale zh-CN
```

Scaffold commands create manual-entry templates only. They do not parse source files and do not use AI.
