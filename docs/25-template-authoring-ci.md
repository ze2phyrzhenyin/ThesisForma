# Template Authoring CI

`ci quality-report` aggregates the authoring quality workflow:

```bash
dotnet run --project src/ThesisDocx.Cli -- ci quality-report \
  --template examples/templates/example-university-engineering \
  --document examples/full-thesis/document.json \
  --requirements examples/requirements/example-engineering-requirements.json \
  --suite examples/template-regression/template-regression-suite.json \
  --negative-fixtures examples/negative-fixtures/negative-fixture-manifest.json \
  --threshold 0.85 \
  --out out/ci/quality-report.json \
  --markdown out/ci/quality-report.md
```

The report includes checks, artifacts, blocking issues, warnings, recommended actions, quality score, and merge decision.

`qualityScore` is an engineering gate metric. It is not a visual similarity score and not a final thesis-format certification.

`suggestedMergeDecision` is `approve`, `approveWithWarnings`, or `reject`. A human reviewer must still approve real college templates.
## Pilot Package Artifact

Template authoring CI may attach `*.template-pilot.zip` files created by `onboarding package`.
These packages contain the candidate template, resolved reports, redacted requirement summaries, baselines, and checksums.
They must not contain source PDFs/DOCX files, generated DOCX artifacts, system font files, or sensitive personal information.
