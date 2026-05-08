# Template Pilot Package

`TemplatePilotPackage` is a deterministic ZIP artifact for reviewing a pilot template. It is not a Word `.dotx` template.

Package contents:

- `manifest.json`
- `template/template.json`
- `template/format-spec.json`
- `template/assets/`
- `requirements/requirements.redacted.json`
- `requirements/mapping-report.json`
- `reports/gate.json`
- `reports/diagnostic.json`
- `reports/authoring.json`
- `reports/onboarding-summary.json`
- `baselines/`
- `checksums.json`

The package excludes source documents, generated DOCX files, system fonts, absolute paths, and long evidence excerpts. `package-validate` checks forbidden extensions and SHA-256 checksums and returns normalized privacy diagnostics for invalid paths, forbidden entries, missing manifests, and checksum mismatches.

```bash
dotnet run --project src/ThesisDocx.Cli -- onboarding package \
  --workspace examples/onboarding/example-engineering-pilot \
  --out out/example-engineering-pilot.template-pilot.zip

dotnet run --project src/ThesisDocx.Cli -- onboarding package-validate \
  --package out/example-engineering-pilot.template-pilot.zip
```
