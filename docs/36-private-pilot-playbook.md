# Real-College Pilot Playbook

Real college pilots stay outside versioned examples by default. Use `onboarding-workspaces/<slug>/`, which is ignored by git. A real public-source example may be committed under `examples/onboarding/<slug>/` only after the repository owner records a `publicSourceExample` manifest and exact source attestations.

## Inputs

Put private source material in:

```text
onboarding-workspaces/<slug>/source-documents/
```

Allowed private inputs:

- reviewed formatting requirements;
- screenshots or manually written observations;
- source DOCX/PDF files that are needed for human review.

Never commit private source DOCX/PDF files, font binaries, intake evidence with thesis content, or draft thesis JSON containing user content. Public-source examples may commit only attested `.docx`/`.pdf` inputs and sanitized derived fixtures.

## Command Sequence

```bash
dotnet run --project src/ThesisDocx.Cli -- onboarding init \
  --workspace onboarding-workspaces/<slug> \
  --school "<school>" \
  --college "<college>" \
  --degree-type master \
  --locale zh-CN

dotnet run --project src/ThesisDocx.Cli -- onboarding scaffold-requirements --workspace onboarding-workspaces/<slug>
dotnet run --project src/ThesisDocx.Cli -- onboarding scaffold-template --workspace onboarding-workspaces/<slug>
dotnet run --project src/ThesisDocx.Cli -- onboarding scaffold-fixtures --workspace onboarding-workspaces/<slug>
dotnet run --project src/ThesisDocx.Cli -- onboarding baseline-init --workspace onboarding-workspaces/<slug> --reason "Initial reviewed pilot baseline"
dotnet run --project src/ThesisDocx.Cli -- onboarding run-gate --workspace onboarding-workspaces/<slug> --out out/<slug>.gate.json
dotnet run --project src/ThesisDocx.Cli -- onboarding diagnose --workspace onboarding-workspaces/<slug> --out out/<slug>.diagnose.json --markdown out/<slug>.diagnose.md
dotnet run --project src/ThesisDocx.Cli -- onboarding authoring-report --workspace onboarding-workspaces/<slug> --out out/<slug>.authoring.json --markdown out/<slug>.authoring.md
dotnet run --project src/ThesisDocx.Cli -- onboarding package --workspace onboarding-workspaces/<slug> --out out/<slug>.template-pilot.zip
dotnet run --project src/ThesisDocx.Cli -- onboarding package-validate --package out/<slug>.template-pilot.zip --out out/<slug>.package-validate.json
```

## Acceptance

- `onboarding run-gate` passes or has reviewed, tracked limitations.
- `onboarding authoring-report` is `ready` before treating the template as a candidate.
- `onboarding package-validate` passes.
- Privacy scan has zero breaking findings.
- A human reviewer confirms the rendered DOCX against the institution rules.

## What To Bring Back

Bring back only sanitized work:

- schema or renderer feature gaps;
- fictional reproductions of format requirements;
- docs updates that describe implemented behavior;
- anonymous aggregate findings.

Do not bring back private source files, copied school documents, user thesis content, or font binaries.

## Public-Source Exception

If a school-published sample is intentionally committed to `examples/onboarding/`, keep the workspace small:

- include the source DOCX/PDF only under `source-documents/`;
- record `publicSourceAttestations` in `onboarding.json`;
- record `acceptance.reviewStatus`, `acceptance.acceptedScope`, and `acceptance.knownGaps`;
- omit raw extraction text, generated DOCX/PDF outputs, package zips, and font binaries;
- run `privacy scan --path examples` and `onboarding package-validate`.
