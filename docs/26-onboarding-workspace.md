# Onboarding Workspace

`OnboardingWorkspace` is a pilot workspace for turning one institution's manually reviewed formatting requirements into a template candidate.

The workspace may contain sensitive source files and review notes, so real pilots should live under `onboarding-workspaces/<slug>/` or another private directory by default. Public examples are fictional unless the manifest explicitly declares a reviewed `publicSourceExample` with matching `publicSourceAttestations`.

Typical layout:

- `onboarding.json`: workspace manifest and privacy/quality policy.
- `source-documents/`: private PDFs/DOCX/manual files, not committed.
- `requirements/requirements.json`: manually entered `RequirementCapture`.
- `template/`: pilot `TemplatePackage` and `format-spec.json`.
- `fixtures/`: redacted structured thesis fixtures.
- `baselines/`: pilot baseline manifest and layout/snapshot baselines.
- `reports/`: gate, diagnostic, authoring, and summary reports.
- `artifacts/`: generated DOCX, inspect JSON, layout signatures, and snapshots.

For public-source real-institution examples, `source-documents/` may contain only attested `.docx` or `.pdf` files that are listed in `onboarding.json.publicSourceAttestations`. Generated artifacts, raw extraction text, private thesis content, legacy `.doc`/`.wps` files, and font binaries remain out of examples and packages.

The manifest `acceptance` block records whether the workspace is only `machineChecked` or has been `humanAccepted`. Public-source examples must include the machine gate scope (`privacy`, `schema`, `inputValidation`, `render`, `formatConformance`, `templateRegression`, `baselineCompare`, and `package`) before they can pass onboarding validation. `knownGaps` are part of the acceptance evidence and must not be hidden in prose.

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
