# Privacy Guard

`PrivacyGuard` performs conservative repository and package hygiene checks before a pilot template is reviewed or released.

It checks for:

- real-institution onboarding workspaces under `examples/` unless they are declared as reviewed public-source examples;
- source files such as `.pdf`, `.docx`, `.doc`, and `.wps` in public examples, with a narrow `.docx`/`.pdf` exception for attested public-source onboarding examples;
- long evidence excerpts;
- absolute paths and path traversal, including Windows, macOS, and Linux-style paths;
- forbidden font binaries;
- likely student ids, China identity-number-shaped values, phone numbers, and non-example emails;
- generated DOCX/PDF artifacts in release or package paths;
- oversized base64 blobs in JSON fields.

Privacy findings include `code`, normalized `severity`, `path`, `message`, `suggestedAction`, and, when useful, a `redactedExcerpt`. Findings do not echo full personal values or local absolute paths.

Generated artifacts under onboarding `artifacts/` and `reports/` are ignored for the source-document rule because CI creates them during validation, but they are still reported as hygiene warnings and pilot packages reject them.

## Public-Source Examples

Real institution source files remain private by default. To commit a real public-source DOCX/PDF under `examples/onboarding/<slug>/source-documents/`, the workspace manifest must:

- set `institution.isRealInstitution` to `true`;
- set `institution.redactionPolicy` to `publicSourceExample`;
- set `privacy.allowPublicInstitutionSourceDocumentsInExamples` to `true`;
- restrict `privacy.publicSourceDocumentPathPrefixes` to the source-document directory;
- include a `publicSourceAttestations` entry whose `path` exactly matches the DOCX/PDF path and records `publicAccessBasis`, `reviewedBy`, `reviewedAt`, and `notes`.

The exception does not apply to legacy `.doc` or `.wps` files, private thesis drafts, raw extraction text, generated DOCX/PDF outputs, package zips, or font binaries. Pilot packages still reject source DOCX/PDF files even when the repository example is public-source.

Public-source examples also need an `acceptance` block. `machineChecked` means the repository gates passed; only `humanAccepted` means a human reviewer accepted the encoded format scope. Known gaps stay explicit in `acceptance.knownGaps`.

Known benign warnings can be suppressed narrowly when scanning generated example artifacts:

```bash
dotnet run --project src/ThesisDocx.Cli -- privacy scan \
  --path examples \
  --suppress-warning-code privacy.generatedArtifact.forbidden \
  --max-warnings 0 \
  --out out/ci/privacy-scan-examples.json
```

`--suppress-warning-code` and `--suppress-warning-path` accept repeated or comma-separated values. `--max-warnings` turns remaining unsuppressed warnings into an error-level `privacy.warningThreshold.exceeded` finding. Onboarding workspaces can set the same policy in `onboarding.json` under `privacy.suppressedWarningCodes`, `privacy.suppressedWarningPathPrefixes`, `privacy.maxWarningCount`, `privacy.maxEvidenceExcerptLength`, and `privacy.maxBase64Length`.

The CLI can also load the same policy from a standalone privacy policy JSON object or from a full onboarding manifest:

```bash
dotnet run --project src/ThesisDocx.Cli -- privacy scan \
  --path examples \
  --policy examples/onboarding/example-engineering-pilot/onboarding.json \
  --out out/ci/privacy-scan-examples.json
```

Explicit CLI flags override values loaded from `--policy`, which keeps CI defaults reusable while still allowing a one-off stricter local scan.

Suppression is deliberately limited. Error findings are never suppressed, and personal-data warnings such as `privacy.personal.email`, `privacy.personal.phone`, `privacy.personal.identityId`, and `privacy.personal.studentId` remain visible even when a broad path suppression is configured. Suppressions are intended only for known generated example artifacts, not for real source material or personal data.

The public example policy uses a zero-warning budget after suppression. Its path whitelist is deliberately narrow and covers only generated onboarding artifacts plus intentionally invalid/regression fixture files. If a new warning appears outside that whitelist, treat it as a quality-gate failure and either remove the finding or add a narrow, reviewable suppression with a documented reason.

Pilot package manifests include both `privacyScanSummary` and `privacyPolicySummary`. The policy summary records the configured excerpt/base64 limits, warning threshold, explicit suppressions, and non-suppressible warning prefixes so reviewers can see which privacy gate settings were used when the package was built.

PrivacyGuard is not a substitute for human privacy review. It is a quality gate that catches common mistakes.
