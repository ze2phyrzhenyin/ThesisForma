# Core Service Facade Contract

The Core service facade is the reusable application boundary for CLI, future API wrappers, and CI tooling. It is not a deployed production API and does not change the Stage 1 product boundary:

`ThesisDocument + ThesisFormatSpec -> valid DOCX`

`ThesisDocument + TemplatePackage -> resolved ThesisFormatSpec + page templates -> valid DOCX`

## Current Facades

- `ThesisValidateService`
- `ThesisRenderService`
- `TemplateResolveService`
- `TemplateWorkflowService`
- `RequirementsWorkflowService`
- `NegativeFixturesWorkflowService`
- `PrivacyWorkflowService`
- `OnboardingPackageWorkflowService`
- `CiQualityReportService`

CLI commands should parse arguments, call a facade, write the existing report shape on success, and write the facade result only when the underlying report cannot be produced.

## Result Shape

Facade results share these fields:

- `success`: operation-level success.
- `errorCount`: normalized error count.
- `warningCount`: normalized warning count.
- `diagnostics`: machine-readable diagnostics using `code`, `severity`, `path`, `message`, `fixHint`, `category`, and `source`.
- `versionReport`: present on schema-aware document/template operations.

Specific facades may also return:

- `isValid`
- `passed`
- `report`
- `validation`
- `scan`
- `artifact`
- `resolution`

## Failure Behavior

Bad requests return a structured error result instead of throwing through CLI handlers. Typical request errors use stable codes such as:

- `service.input.missing`
- `service.template.missing`
- `service.template.gate.request.invalid`
- `service.requirements.missing`
- `service.negativeFixtures.manifestMissing`
- `service.privacy.pathMissing`
- `service.onboarding.packageMissing`

Unexpected execution failures are mapped to `service.*Failed` diagnostics with truncated details. Consumers should match `diagnostics[].code` and `diagnostics[].severity`, not message wording.

## Artifact Rules

Facade results must not include raw DOCX bytes, ZIP bytes, large base64 payloads, or copied source material. Artifact metadata should be minimal and stable. Where possible, artifact paths are filenames or caller-provided report paths rather than local absolute paths.

Generated files belong under explicit output directories such as `/tmp`, ignored onboarding workspaces, or configured CI output directories. Facades must not write user artifacts into `examples/`.

## CLI Compatibility

For successful commands, CLI output keeps the historical report shape:

- `template gate` writes `TemplateGateReport`.
- `template diagnose` writes `DiagnosticReport`.
- `template authoring-report` writes `TemplateAuthoringReport`.
- `requirements validate` writes `RequirementCaptureValidationResult`.
- `requirements report` writes `RequirementMappingReport`.
- `negative-fixtures run` writes `NegativeFixtureRunResult`.
- `privacy scan` writes `PrivacyGuardResult`.
- `onboarding package-validate` writes `TemplatePilotPackageValidationResult`.

The facade result wrapper is emitted only when a command cannot produce its normal report.
