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

- `reportVersion`: stable machine-readable report contract version. Current value is `1.0.0`.
- `success`: operation-level success.
- `errorCount`: normalized error count.
- `warningCount`: normalized warning count.
- `diagnostics`: machine-readable diagnostics using `code`, `severity`, `path`, `message`, `fixHint`, `category`, and `source`.
- `versionReport`: present on schema-aware document/template operations.

`versionReport` uses `SchemaVersionSupport` and has a stable `reportVersion`, `isValid`, `checks[]`, and `diagnostics[]`. `checks[].direction` is one of `current`, `supported`, `old`, `future`, `missing`, `unsupported`, or `unknown`. Schema-aware CLI JSON consumers should read this report instead of reimplementing supported-version checks. Unsupported versions also surface as normalized diagnostics when they affect operation success, and the same shape is documented by `schemas/version-report.schema.json`.

The same version report is now carried through the long-running template quality reports:

- `template gate`
- `template diagnose`
- `template authoring-report`
- `ci quality-report`

These reports include `reportVersion: "1.0.0"` and checks for `thesisDocument`, `templatePackage`, and the resolved `thesisFormatSpec` when all inputs are available. This is additive: the historical report root object is preserved, and `versionReport` is a stable nested contract for machine consumers.

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

- `render --json` writes `RenderResult` to stdout and keeps the DOCX path in `artifact`.
- `render --template ... --validate-template --json` runs the full `TemplateValidationService` before rendering. This catches template-authoring errors such as page-template variable references that plain template resolution does not need to evaluate.
- `template gate` writes `TemplateGateReport`.
- `template diagnose` writes `DiagnosticReport`.
- `template authoring-report` writes `TemplateAuthoringReport`.
- `requirements validate` writes `RequirementCaptureValidationResult`.
- `requirements report` writes `RequirementMappingReport`.
- `negative-fixtures run` writes `NegativeFixtureRunResult`.
- `privacy scan` writes `PrivacyGuardResult`.
- `onboarding package-validate` writes `TemplatePilotPackageValidationResult`.

The facade result wrapper is emitted only when a command cannot produce its normal report.

When a command writes JSON to stdout, human summaries must go to stderr or be suppressed. This keeps CLI output parseable for CI and future API wrappers. Commands that write JSON to `--out` may still print a short human "Wrote ..." summary to stdout.

## Versioning Boundaries

Migration hooks are intentionally no-op today. A supported older version may pass with `direction: "supported"`, but the facade does not rewrite it to the current version. Unsupported old, future, missing, or malformed versions report `versionReport.isValid: false` and populate `versionReport.diagnostics[]` with stable codes such as `thesis.schemaVersion.unsupported`, `format.schemaVersion.unsupported`, or `template.schemaVersion.unsupported`.

Future API wrappers should pass through `versionReport` unchanged. They should not infer version support from message text or from enum ordering in the web client.
