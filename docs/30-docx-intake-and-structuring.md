# DOCX Intake And Structuring

DOCX intake starts the second phase without changing the renderer contract.

The flow is:

1. `extract docx` reads `input.docx` with OpenXML and writes extraction evidence.
2. `structure draft` maps evidence into a draft `ThesisDocument`.
3. `structure prompt` creates a Codex review prompt for human-guided structure review.
4. `validate-input` checks the draft against the existing schema and semantic validator.
5. `render` can produce a formatted DOCX from the draft and a template.

Extraction is factual. It records paragraphs, runs, styles, tables, images, fields, notes, bookmarks, numbering, section properties, and candidate roles with evidence paths such as `paragraphs[12]` or `tables[0].rows[1].cells[2]`.

Structuring is interpretive. It classifies sections and blocks, but it must not rewrite thesis text, polish language, summarize paragraphs, or delete content. Uncertain metadata, heading levels, bibliography boundaries, captions, and section mappings belong in `unresolved-items.json`.

Codex or another LLM may review `extraction.json` and `extracted.md`, but it should not read or modify `input.docx` directly. The LLM output must include evidence links and must preserve original body text.

Why not ask an LLM to output DOCX directly:

- deterministic rendering already exists;
- OpenXML packages contain many volatile implementation details;
- schema validation and evidence links are easier to review than a binary DOCX;
- renderer output can be regression-tested.

Privacy:

- `onboarding-workspaces/docx-structure-pilot/input/input.docx` may contain user thesis content.
- `extraction/plain-text.txt` and `structured/thesis-document.draft.json` may contain the full thesis.
- Keep these files in ignored onboarding workspaces.
- Do not copy user content into `examples` or docs.

Safety boundaries:

- `extract docx` rejects missing inputs, non-`.docx` inputs, empty files, invalid ZIP/OpenXML packages, packages without `word/document.xml`, unsafe ZIP entry paths, unsafe relationship targets, excessive entry counts, excessive expanded size, and high compression ratios.
- Internal relationship targets are normalized inside the package root. External relationship targets are limited to `http`, `https`, and `mailto`; local file and UNC-style targets fail intake.
- Intake workspace outputs stay inside the configured private workspace.
- Extraction, structure-draft, and intake failure reports include `reportVersion: "1.0.0"` and use the normalized `diagnostics[]` contract with `category: "intake"` and severity `error`, `warning`, or `info`.
- The draft document is a rule-assisted prototype artifact. It is not a formal DOCX import feature and must not be presented as AI inference or free-document parsing.

Current limits:

- no OCR by default;
- no Microsoft Word dependency;
- no screenshot-level visual diff;
- rule-based structuring is a draft, not a guarantee;
- uncertain items require human review.

Commands:

```bash
dotnet run --project src/ThesisDocx.Cli -- extract docx \
  --input onboarding-workspaces/docx-structure-pilot/input/input.docx \
  --out onboarding-workspaces/docx-structure-pilot/extraction/extraction.json \
  --text onboarding-workspaces/docx-structure-pilot/extraction/plain-text.txt \
  --markdown onboarding-workspaces/docx-structure-pilot/extraction/extracted.md

dotnet run --project src/ThesisDocx.Cli -- structure draft \
  --extraction onboarding-workspaces/docx-structure-pilot/extraction/extraction.json \
  --out onboarding-workspaces/docx-structure-pilot/structured/thesis-document.draft.json \
  --report onboarding-workspaces/docx-structure-pilot/structured/structure-mapping-report.json \
  --unresolved onboarding-workspaces/docx-structure-pilot/structured/unresolved-items.json

dotnet run --project src/ThesisDocx.Cli -- intake docx \
  --input onboarding-workspaces/docx-structure-pilot/input/input.docx \
  --workspace onboarding-workspaces/docx-structure-pilot \
  --template examples/templates/example-university-engineering
```
