---
name: privacy-guard
description: Use for PrivacyGuard, redaction, source document isolation, forbidden asset checks, and examples/package hygiene.
---

# Privacy Guard

- Treat PrivacyGuard as a conservative quality gate, not a replacement for human privacy review.
- Do not commit real PDFs, DOCX manuals, personal data, or long copyrighted excerpts.
- Package mode must reject source documents and font binaries.
- Findings need stable `code`, `severity`, `path`, `message`, and `suggestedAction`.
- Tests should assert finding codes and severities, not just pass/fail.
