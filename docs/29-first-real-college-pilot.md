# First Real College Pilot

Start with one real college, not a batch.

Run the full fictional template loop first, using `examples/templates/example-university-humanities` or another committed fictional package. Private real source files should enter only after the declarative field needed by the pilot is already represented in schema, renderer, validation, and regression tests. Public-source real examples additionally require manifest attestations and privacy scan.

1. Put source PDFs/DOCX/manual notes in `onboarding-workspaces/<slug>/source-documents/`; do not commit them.
2. Run `onboarding init`.
3. Manually fill `RequirementCapture`; keep evidence excerpts short.
4. Run `requirements validate` and `requirements report`.
5. Run `onboarding scaffold-template` from the closest reviewed base template.
6. Run `onboarding scaffold-fixtures` with redacted structured thesis fixtures.
7. Run `onboarding baseline-init --reason "..."`
8. Run `onboarding run-gate`.
9. Run `onboarding diagnose`.
10. Run `onboarding authoring-report`.
11. Run `privacy scan`.
12. Run `onboarding package`.
13. Run `onboarding package-validate`.
14. Perform human review of the reports and pilot package.
15. Only after review, copy the sanitized template into the template library.

If a rule cannot be expressed in `ThesisFormatSpec` or `TemplatePackage`, extend the schema/renderer/validator first. Do not hardcode a school-specific branch in the renderer.

This workflow still does not do unattended AI parsing, does not depend on Microsoft Word, and does not promise pixel-level visual equivalence. Any Codex-assisted intake review stays inside the private workspace and remains evidence-backed prototype output until human review.
