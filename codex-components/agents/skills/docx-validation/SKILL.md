---
name: docx-validation
description: Use when working on OpenXmlValidator, format conformance checks, DOCX inspection, and validator CLI behavior.
---

# DOCX Validation

Validation must inspect DOCX package parts and WordprocessingML nodes. Use `OpenXmlValidator` first, then project-specific checks in `FormatConformanceValidator`.

Do not accept file-exists checks as validation. Report failures by part and node where possible.
