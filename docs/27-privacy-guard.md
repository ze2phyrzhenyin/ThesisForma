# Privacy Guard

`PrivacyGuard` performs conservative repository and package hygiene checks before a pilot template is reviewed or released.

It checks for:

- real-institution onboarding workspaces under `examples/`;
- source files such as `.pdf`, `.docx`, `.doc`, and `.wps` in public examples;
- long evidence excerpts;
- absolute paths and path traversal;
- forbidden font binaries;
- likely student ids, phone numbers, and non-example emails.

Privacy findings include `code`, `severity`, `path`, `message`, and `suggestedAction`.

Generated artifacts under onboarding `artifacts/` and `reports/` are ignored for the source-document rule because CI creates them during validation. Pilot packages still reject source documents and font binaries.

PrivacyGuard is not a substitute for human privacy review. It is a quality gate that catches common mistakes.
