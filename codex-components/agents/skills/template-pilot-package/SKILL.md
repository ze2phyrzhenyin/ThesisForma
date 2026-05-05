---
name: template-pilot-package
description: Use for TemplatePilotPackage ZIPs, package manifests, redacted requirements, checksums, and package validation.
---

# Template Pilot Package

- Pilot packages are audit artifacts, not Word `.dotx` templates.
- ZIP output must be deterministic: stable file order, stable timestamps, and stable JSON.
- Include template files, redacted requirements, mapping reports, quality reports, baselines, manifest, and checksums.
- Exclude source documents, generated DOCX artifacts, system fonts, absolute paths, and sensitive personal data.
- Validate checksums and privacy before accepting a package.
