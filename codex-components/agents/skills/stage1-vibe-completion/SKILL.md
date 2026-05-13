---
name: stage1-vibe-completion
description: Use when coordinating a one-pass Stage 1 completion push across DocumentOverrides, template-to-DOCX fixtures, schema docs/type generation, unified CI, common rendering pain points, maintainability refactors, and web performance.
---

# Stage 1 Vibe Completion

This skill coordinates a large integrated implementation pass. It is not permission to blur ownership boundaries or skip verification.

Primary component pack:

- `codex-components/vibe-coding/stage1-completion/README.md`
- `codex-components/vibe-coding/stage1-completion/components/*.md`

Rules:

- Preserve the Stage 1 product boundary: structured data plus declarative format/template input renders deterministic DOCX.
- Do not hardcode a school, college, or template id in renderer logic.
- Treat "done" as gate-passing evidence, not a claim of perfection.
- Keep implementation slices independently reviewable even when they are delivered in one pass.
- Every schema/model feature must update docs, examples, API or web contracts where applicable, and tests.
- Every rendering feature must include XML-level assertions and `OpenXmlValidator` coverage.
- Do not put real institution source files, generated DOCX, user thesis content, font binaries, or long source excerpts in `examples/`.
- Run the unified gate before final handoff:

```bash
scripts/ci-quality-gate
```

Set `WEB_E2E=0` only for fast local iteration; do not use that as final acceptance for web-facing changes.

