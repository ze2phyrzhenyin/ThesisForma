# Component 06: Maintainability Refactor

## Goal

Reduce future change risk by splitting current large files without changing behavior.

## Owners

- CLI/API/Core workers according to touched file ownership.
- Web editor worker for frontend contract and panel splits.

## Write Scope

- `src/ThesisDocx.Cli`
- `src/ThesisDocx.Core/Services`
- `web/src/editor/documentContract.ts`
- `web/src/editor/panels/OverridesPanel.tsx`
- related tests only when imports or behavior require updates.

## Required Behavior

- Split CLI command handlers by command group.
- Split service request/result DTOs from service implementations.
- Split web document normalization, cleaning, validation, and walking helpers.
- Split override panel form primitives from the container.
- Preserve public CLI/API JSON contracts.

## Acceptance Gates

```bash
dotnet test ThesisDocx.slnx --nologo
npm --prefix web run typecheck
npm --prefix web test
scripts/ci-quality-gate
```

Required test evidence:

- no contract snapshots change unless explicitly documented;
- CLI JSON contract tests still pass;
- web serialization and fixture round-trip tests still pass.

## Boundaries

- No behavior changes in this component unless a test exposes an existing bug and the fix is called out separately.
- Do not combine broad refactors with new renderer features in the same commit unless the one-pass delivery requires it and tests isolate the behavior change.

