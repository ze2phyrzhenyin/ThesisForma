---
name: baseline-management
description: Use when work involves template regression baselines, format fixture baselines, baseline manifests, baseline compare/init/update, snapshots, or layout baseline thresholds.
---

# Baseline Management

Baselines are structural OpenXML artifacts, not visual screenshots.

Rules:

- Manifest paths must be relative.
- Baseline compare output must identify case id, fixture id when applicable, category, path, expected, and actual.
- Baseline update must require a human reason.
- Do not silently overwrite committed baselines.
- Keep output deterministic.
- Use layout signatures and normalized snapshots; do not call Microsoft Word.
- Update schemas, examples, docs, and tests when baseline manifest fields change.
