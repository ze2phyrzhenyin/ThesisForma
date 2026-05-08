# Privacy Guard

`PrivacyGuard` performs conservative repository and package hygiene checks before a pilot template is reviewed or released.

It checks for:

- real-institution onboarding workspaces under `examples/`;
- source files such as `.pdf`, `.docx`, `.doc`, and `.wps` in public examples;
- long evidence excerpts;
- absolute paths and path traversal, including Windows, macOS, and Linux-style paths;
- forbidden font binaries;
- likely student ids, China identity-number-shaped values, phone numbers, and non-example emails;
- generated DOCX/PDF artifacts in release or package paths;
- oversized base64 blobs in JSON fields.

Privacy findings include `code`, normalized `severity`, `path`, `message`, `suggestedAction`, and, when useful, a `redactedExcerpt`. Findings do not echo full personal values or local absolute paths.

Generated artifacts under onboarding `artifacts/` and `reports/` are ignored for the source-document rule because CI creates them during validation, but they are still reported as hygiene warnings and pilot packages reject them.

PrivacyGuard is not a substitute for human privacy review. It is a quality gate that catches common mistakes.
