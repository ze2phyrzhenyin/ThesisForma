---
name: fix-hint-rule-catalog
description: Use when work involves resources/fix-hint-rules.json, FixHintRuleCatalog, FixHintRuleMatcher, or mapping diagnostic codes to fix hints.
---

# Fix Hint Rule Catalog

Fix hints are advisory, reviewable suggestions.

Rules:

- Update `resources/fix-hint-rules.json`, schema, tests, and docs together.
- Each rule needs a docs ref or fixture ref.
- Keep ordering deterministic.
- Do not turn fix hints into automatic template rewrites.
- Use precise spec/template paths when known.
