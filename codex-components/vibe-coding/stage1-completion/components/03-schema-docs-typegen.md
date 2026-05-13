# Component 03: Schema Documentation And Frontend Types

## Goal

Stop schema, C# models, TypeScript types, and docs from drifting by generating schema documentation and frontend type surfaces from `schemas/*.json`.

## Owners

- Schema worker for schema generation and docs.
- Web editor worker for generated TypeScript integration.
- Docs worker for published generated-doc placement and review guidance.

## Write Scope

- `schemas/*.schema.json`
- `scripts/generate-schema-docs`
- `scripts/generate-web-types`
- `docs/generated`
- `web/src/types/generated`
- `web/src/types`
- `tests/ThesisDocx.Tests`
- `web/src/tests`

## Required Behavior

- Generated docs include schema title, version, required fields, enum values, defaults where present, and discriminator mappings.
- Generated TypeScript is reproducible and checked into the repo or verified by a diff-check script.
- CI fails when generated docs/types are stale.
- Handwritten adapter types are allowed only when they wrap generated types with documented editor conveniences.

## Acceptance Gates

```bash
scripts/generate-schema-docs --check
scripts/generate-web-types --check
dotnet test ThesisDocx.slnx --nologo --filter "FullyQualifiedName~Schema"
npm --prefix web run typecheck
npm --prefix web test
scripts/ci-quality-gate
```

Required test evidence:

- generator output is deterministic;
- schema examples validate after generation;
- frontend imports generated types without losing existing round-trip fixture coverage.

## Boundaries

- Do not weaken schema constraints to make generation easier.
- Do not make generated files depend on local absolute paths.

