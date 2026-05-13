# Stage 1 Completion Vibe Coding Pack

This pack turns the current highest-value improvements into executable components for a single coordinated implementation pass.

The goal is not to promise perfection by wording. The goal is to make every workstream finishable, reviewable, and objectively gated.

## Components

1. `components/01-document-overrides.md`
2. `components/02-template-to-docx-closed-loop.md`
3. `components/03-schema-docs-typegen.md`
4. `components/04-web-ci-redline.md`
5. `components/05-rendering-painpoints.md`
6. `components/06-refactor-maintainability.md`
7. `components/07-web-performance.md`

## One Pass Protocol

Run these components in one integrated pass, but keep ownership boundaries clear:

1. Start from a clean baseline:

```bash
git status --short
dotnet test ThesisDocx.slnx --nologo
npm --prefix web run typecheck
npm --prefix web test
npm --prefix web run build
```

2. Implement contract-first changes before renderer or UI changes.
3. Add XML/API/schema/web tests in the same slice as the behavior they prove.
4. Keep fictional examples in `examples/`; keep real pilot evidence in ignored onboarding workspaces.
5. Run the final unified gate:

```bash
scripts/ci-quality-gate
```

For short local iteration only:

```bash
WEB_E2E=0 scripts/ci-quality-gate
```

## Global Definition Of Done

A component is done only when:

- the implementation is inside the component write scope;
- docs and examples match actual behavior;
- all new model/schema fields are covered in schema, examples, and docs;
- renderer behavior is proven by OpenXML XML assertions, not file existence;
- validation and diagnostics expose actionable codes for invalid input;
- snapshots or baselines normalize unstable OpenXML/package data where used;
- `scripts/ci-quality-gate` passes with `WEB_E2E=1` for final acceptance.

## Anti Goals

- Do not add AI parsing of messy documents.
- Do not add Word automation or screenshot-based DOCX comparison.
- Do not add Word-like manual layout controls to the web editor.
- Do not use real college files as committed fixtures.
- Do not call a task complete because a DOCX file exists.

