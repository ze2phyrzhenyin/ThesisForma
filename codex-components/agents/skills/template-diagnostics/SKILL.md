---
name: template-diagnostics
description: Use when work involves DiagnosticReport, DiagnosticIssue, FixHintEngine, template diagnose, failure triage, or converting validator/gate/regression errors into author-readable reports.
---

# Template Diagnostics

Diagnostic reports are for template authors and reviewers.

Rules:

- Preserve source, severity, category, part/path/spec/template location, expected, actual, and evidence when available.
- Attach fix hints as suggestions, not automatic fixes.
- Link related docs and fixtures.
- Keep issue ordering deterministic.
- Gate/regression failures must not be collapsed into one opaque line.
- Diagnostics do not prove thesis semantic correctness and do not replace human review.
