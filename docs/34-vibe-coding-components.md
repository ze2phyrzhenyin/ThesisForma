# Vibe Coding Components

The repository keeps reusable Codex components in `codex-components/`. The Stage 1 completion pack turns the current product-improvement backlog into implementation components with explicit owner boundaries and gates.

Pack path:

```text
codex-components/vibe-coding/stage1-completion/
```

Use this pack when a single implementation push should cover:

- `DocumentOverrides` backend/API/renderer integration;
- broader fictional template-to-DOCX proof;
- generated schema docs and frontend types;
- unified Core/Web CI;
- footnote/endnote, table-cell, and page-template rendering pain points;
- large-file maintainability refactors;
- web bundle and lazy-loading performance.

Final acceptance is gate based, not assertion based:

```bash
scripts/ci-quality-gate
```

For fast local iteration only:

```bash
WEB_E2E=0 scripts/ci-quality-gate
```

Do not use the fast path as the final acceptance for web-facing changes.

