# Worker Development Guide

Every worker must first read:

- `README.md`
- `AGENTS.md`
- `docs/01-architecture.md`
- `docs/02-structured-data-schema.md`
- `docs/03-format-spec-schema.md`

## Boundaries

Schema worker:
: Owns models, JSON schema documentation, examples, and docs about structured data. Does not implement OpenXML rendering.

Template worker:
: Owns `Models/Templates`, `Templates`, template schemas, example template packages, template CLI commands, and template docs. Does not hardcode a school into renderer code.

Renderer worker:
: Owns rendering and OpenXML folders, including OMML equations and advanced table XML. Does not hardcode college rules and does not expand schema without syncing docs and tests.

Validation worker:
: Owns validators, snapshot normalizer, inspect summaries, XML assertion helpers, and test robustness. Does not change rendering behavior unless fixing a clear bug.

Docs worker:
: Owns README and docs. Must document real code behavior, not planned behavior as if it exists.

## Parallel Rules

- Use independent branches or explicit file boundaries.
- Do not revert changes made by other workers.
- Schema changes require examples and docs.
- Renderer changes require XML-level tests.
- Equation changes must assert concrete OMML nodes.
- Table changes must assert concrete WordprocessingML nodes for width, layout, merges, borders, and row behavior.
- Template changes must test loader/resolver/merger/variables/assets/page rendering/diff/coverage at the appropriate layer.
- Validation changes must identify the OpenXML part and node being checked.
- Docs must be synchronized with code and tests.
- Codex components live in `codex-components/` in restricted environments; install them with `scripts/install-codex-components` when root dot-directories are allowed.

## Merge Checklist

Before merging:

```bash
dotnet build ThesisDocx.slnx
dotnet test ThesisDocx.slnx
scripts/generate-example-docx
dotnet run --project src/ThesisDocx.Cli -- validate --docx out/simple.docx --format examples/format-specs/basic-cn-thesis.json
dotnet run --project src/ThesisDocx.Cli -- validate-input --document examples/full-thesis/document.json --format examples/format-specs/strict-cn-thesis.json
dotnet run --project src/ThesisDocx.Cli -- template validate --template examples/templates/example-university-engineering
dotnet run --project src/ThesisDocx.Cli -- template diff --base examples/templates/example-university-engineering --target examples/templates/example-university-engineering-variant --json
```
