# Codex Components

This repository includes Codex skills and agent configuration in `codex-components/`.

The intended runtime paths are:

- `.agents/skills/...`
- `.codex/config.toml`
- `.codex/agents/*.toml`
- `.codex/hooks/README.md`

In this sandbox, creating root `.agents` and `.codex` is blocked with `Operation not permitted`. The mirror directory keeps the content versioned and reviewable without blocking normal development.

## Install In A Normal Environment

Run:

```bash
scripts/install-codex-components
```

The script copies:

- `codex-components/agents/skills` to `.agents/skills`
- `codex-components/codex/config.toml` to `.codex/config.toml`
- `codex-components/codex/agents` to `.codex/agents`
- `codex-components/codex/hooks` to `.codex/hooks`

If the local filesystem has the same dot-directory restriction, the script will fail early and leave the mirror intact.

## Multi Worker Use

Workers should read `AGENTS.md` and `docs/06-worker-development-guide.md` first.

Use the mirrored skills as role guidance:

- `openxml-docx-rendering`: renderer and WordprocessingML work.
- `thesis-format-schema`: models, schemas, and examples.
- `docx-validation`: validators and inspect output.
- `docx-regression-testing`: snapshot and XML assertion work.
- `template-package-system`: template loader/resolver/merge/variables/assets.
- `page-template-layout`: cover/declaration layout block rendering.
- `template-validation-diff`: template validation, resolved-spec diff, and coverage matrix.
- `docx-structure-diff`: DOCX package/XML structural diff.
- `layout-signature`: OpenXML layout signature extraction and comparison.
- `template-regression-gate`: template regression suites and onboarding gates.
- `requirement-capture`: manually reviewed requirement capture models, schema, examples, and mapping reports.
- `baseline-management`: template/fixture baseline manifests, compare/update flows, and update reasons.
- `template-diagnostics`: diagnostic reports and fix hints for gate/regression/diff/validator failures.
- `template-authoring-quality`: authoring reports, checklist readiness, and real college onboarding quality workflow.
- `ci-quality-gate`: CI scripts, workflow drafts, artifacts, and aggregate quality reports.
- `negative-fixtures`: expected-failure fixture suites and runner behavior.
- `fix-hint-rule-catalog`: maintainable fix hint JSON rules and schema.
- `diagnostic-markdown-report`: concise Markdown reports for PR comments.
- `onboarding-workspace`: private pilot workspaces, scaffolding, validation, and summary reports.
- `privacy-guard`: source document isolation, redaction, and package/example privacy findings.
- `template-pilot-package`: deterministic pilot ZIPs, manifests, checksums, and package validation.
- `real-college-pilot`: human-reviewed real college onboarding flow from requirement capture to pilot package.
- `docx-intake-extraction`: OpenXML extraction from uploaded DOCX files into evidence JSON.
- `thesis-structure-mapping`: rule-assisted mapping from extraction evidence to `ThesisDocument` drafts.
- `llm-structure-review`: Codex review prompt discipline for structure mapping without rewriting content.
- `web-structured-thesis-editor`: React structured thesis editor, block model, `ThesisDocument` JSON serialization.
- `thesis-editor-design-system`: professional, restrained three-column editor UI and shared design tokens.
- `web-editor-api`: ASP.NET Core editor API, runtime file storage, render/validate/assets endpoints, and path safety.

The `.toml` files define suggested worker boundaries for schema, renderer, validation, and docs agents.
