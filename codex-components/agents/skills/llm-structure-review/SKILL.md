---
name: llm-structure-review
description: Use when Codex/LLM reviews extraction.json and extracted.md to improve structure mapping while preserving text and uncertainty.
---

# LLM Structure Review

- Read extraction artifacts, not `input.docx`.
- Do not invent metadata or facts.
- Do not polish, summarize, or delete thesis content.
- Use `evidenceLinks` for each section/block whenever possible.
- Keep all uncertainty in `unresolved-items.json`.
