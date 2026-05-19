# DOCX Intake And Structuring

DOCX intake starts the second phase without changing the renderer contract.

The standalone flow is:

1. `extract docx` reads `input.docx` with OpenXML and writes extraction evidence.
2. `extract format-candidates` turns effective format clusters into a draft `ThesisFormatSpec` candidate plus an evidence report.
3. `structure draft` maps evidence into a draft `ThesisDocument`.
4. `structure prompt` creates a Codex review prompt for human-guided structure review.
5. `structure codex-review` can run an explicit Codex CLI review step that returns a structured repair plan for private artifacts.
6. `validate-input` checks the draft against the existing schema and semantic validator.
7. `render` can produce a formatted DOCX from the draft and a template.

Extraction is factual. It records paragraphs, runs, styles, tables, images, fields, notes, bookmarks, numbering, section properties, effective paragraph formatting, format signatures, and candidate roles with evidence paths such as `paragraphs[12]` or `tables[0].rows[1].cells[2]`.

Each extracted paragraph includes `effectiveFormat`, a normalized view of style inheritance, numbering level properties, paragraph direct formatting, and single-run direct formatting where it can be represented without hiding mixed-run content. The root `formatSignatures` collection groups paragraphs with the same effective format so template authors can identify candidate body, heading, bibliography, and caption styles from evidence before drafting a `ThesisFormatSpec`.

Messy-source handling is explicit. The root `formatChaos` report scores signature fragmentation, direct paragraph formatting, direct run formatting, missing styles, body-format fragmentation, weak heading styles, and empty paragraph frequency. The root `formatClusters` collection groups similar effective formats by role hints such as `body`, `heading`, `bibliography`, `caption`, and `frontMatter`; clusters are review evidence, not accepted template rules.

`extract format-candidates` is conservative. It emits `candidate-format-spec.json` from high-confidence body, heading, bibliography, and page-setup evidence where available, and writes unresolved items for high chaos, missing clusters, notes, tables, figures, or unsupported surfaces. The candidate is not a template package and must be reviewed before accepted fields are copied into a committed `TemplatePackage`.

`intake docx` runs candidate format generation automatically after extraction. The workspace receives `structured/candidate-format-spec.json`, `structured/format-candidate-report.json`, and `structured/format-candidate-report.md`; `reports/intake-report.json` summarizes candidate status, chaos level, generated field count, and unresolved review count. These candidate files are review evidence, not accepted template fixtures.

Reviewed template proposal is a separate human gate:

1. `template scaffold-candidate-decisions` creates `structured/format-candidate-decisions.json` with one decision entry per generated field.
2. A reviewer changes each decision to `accept`, `reject`, or `modify`, records a reason, and keeps evidence paths for accepted or modified fields.
3. `template propose-from-candidate` copies the source template to a fresh output directory and applies only accepted or modified fields to the copied `format-spec.json`.
4. High-chaos candidates cannot apply accepted or modified fields unless `riskAccepted: true` and `riskAcceptanceReason` are recorded in the decision file.
5. The proposed template still must pass `template validate`, render validation, coverage, and regression gates before it is treated as accepted.

Structuring is interpretive. It classifies sections and blocks, but it must not rewrite thesis text, polish language, summarize paragraphs, or delete content. Uncertain metadata, heading levels, bibliography boundaries, captions, and section mappings belong in `unresolved-items.json`.

`structure draft` now writes a draft-level content preservation audit into `structure-mapping-report.json` under `contentPreservation`. The audit compares source extraction text, notes, and table text against the generated `ThesisDocument` draft text view, records normalized SHA-256 hashes, reports missing segments, and turns long missing source text into blocking issues. `intake docx` summarizes this as `draftContentPreservationStatus`, `draftContentMissingSegments`, and `draftContentBlockingIssues` in `reports/intake-report.json`.

Extracted image evidence is mapped into `FigureBlock` nodes when an image artifact is available. The extractor copies embedded image parts to `artifacts/images/`, records the relationship id, content type, inline/anchor placement kind, drawing dimensions, and source-rectangle crop values when WordprocessingML exposes them. Nearby `图...` / `Figure...` paragraphs are linked as captions, and image evidence inside table cells is retained so rendering can rebuild a table cell with a nested figure. In an intake workspace, artifact paths are rewritten relative to `structured/thesis-document.draft.json` so the normal render path can resolve copied images from `artifacts/images/`. Missing image artifacts are recorded as unresolved review items instead of silently dropping the figure.

Extracted table evidence preserves source order, nearby `表...` / `Table...` captions, caption position, table width where representable, table borders, header-row markers, row height, cell width, cell vertical alignment, shading, cell borders, cell text, horizontal grid spans, vertical merge chains, nested tables, and image ids found inside cells when building draft `TableBlock` nodes. A `<w:vMerge/>` cell with no explicit value is treated as `continue`, matching WordprocessingML semantics instead of dropping the continuation.

Non-picture drawing objects such as text boxes, charts, SmartArt, and shapes are recorded under `drawingObjects` with object type, relationship ids, graphic data URI, dimensions, placement kind, text where available, raw XML evidence, and evidence path. These objects are not silently treated as normal body text. Structuring maps them to `preservedObject` blocks with an explicit preservation mode and emits review-required unresolved items because the core `ThesisDocument` model intentionally supports a bounded thesis block surface.

`preservedObject` rendering is conservative. Relationship-free `w:pict` / `w:drawing` objects can use safe passthrough after raw XML validation. Objects with relationships, including charts and SmartArt packages, remain evidence-backed review items or text extraction blocks until their related parts can be copied through a reviewed allowlist.

Footnote and endnote references are preserved as explicit paragraph reference ids in `extraction.json` and mapped into `FootnoteInline` / `EndnoteInline` nodes in the draft when matching note content is available. Missing note content is kept as the original reference marker and reported as an unresolved review item rather than guessed.

External hyperlink runs retain their relationship id and URI in `extraction.json`, remain part of paragraph text for content preservation, and map into `HyperlinkInline` nodes in the structured draft.

Codex or another LLM may review `extraction.json` and `extracted.md`, but it should not read or modify `input.docx` directly. The LLM output must include evidence links and must preserve original body text.

`structure codex-review` is the automated form of that review. It first regenerates the rule-based draft, writes `reports/structure-analysis.json`, writes a repair prompt, then invokes `codex exec` in the private workspace. The prompt tells Codex to repair section and chapter boundaries only when evidence supports the move, including cases where content after `第三章` was grouped under `第二章`.

Codex does not directly edit the draft. It must return JSON matching `schemas/structure-repair-plan.schema.json`; Core writes this to `reports/structure-repair-plan.json` and applies operations deterministically. Supported operations are `moveBlock`, `ensureSection`, `addUnresolvedItem`, `removeUnresolvedItem`, and `updateHeadingLevel`. Core writes `reports/structure-repair-apply-report.json` with applied/rejected operation counts, moved block counts, diagnostics, and evidence paths. Direct edits to structured artifacts are detected and rejected.

`intake docx --structure-mode <mode>` controls when Codex runs:

- `rule`: default; use rule-based draft only.
- `auto`: run Codex only when `structure-analysis.json` flags medium/high structure risk.
- `codex`: run Codex and fall back to the rule-based draft if Codex fails.
- `codex-required`: run Codex and block rendering if Codex fails.

`--codex-review` is a compatibility alias for `--structure-mode codex-required`.

`intake docx --codex-review` runs the same Codex step after rule-based structuring and before template validation/rendering. The intake report records `structureMode`, `structureAnalysisStatus`, `structureAnalysisRiskLevel`, `structureQualityScore`, `codexReviewStatus`, `codexReviewExitCode`, and `codexReviewReportPath`; `reports/structure-codex-review.json` records the Codex command, exit code, prompt path, repair plan path, application report path, content-preservation audit result, warnings, blocking issues, and short stdout/stderr excerpts. If Codex exits non-zero, times out, corrupts JSON, directly edits artifacts, returns rejected operations, or fails content preservation in `codex-required` mode, intake treats the draft as untrusted and does not render it.

`intake gate` is the unified intake quality entry point for pilot workspaces. It accepts the same `--input`, `--workspace`, `--template`, and Codex options as `intake docx`, defaults to `--structure-mode auto`, and runs the full deterministic gate: extraction, privacy scan, format candidate generation, rule-based structuring, structure risk scoring, optional Codex repair, schema validation, content-preservation audit, template resolve, draft render, and OpenXML validation.

The default command is `codex exec --sandbox workspace-write --ask-for-approval never --skip-git-repo-check --output-schema schemas/structure-repair-plan.schema.json`. Use `--codex-command`, `--codex-model`, `--codex-profile`, `--timeout-seconds`, `--repair-plan-schema`, or repeated `--codex-arg` only in private developer workspaces.

Why not ask an LLM to output DOCX directly:

- deterministic rendering already exists;
- OpenXML packages contain many volatile implementation details;
- schema validation and evidence links are easier to review than a binary DOCX;
- renderer output can be regression-tested.

Privacy:

- `onboarding-workspaces/docx-structure-pilot/input/input.docx` may contain user thesis content.
- `extraction/plain-text.txt` and `structured/thesis-document.draft.json` may contain the full thesis.
- `structured/candidate-format-spec.json` and `structured/format-candidate-report.json` may contain source-derived format evidence and should be reviewed before any fields are copied into a public template package.
- Keep these files in ignored onboarding workspaces.
- Do not copy user content into `examples` or docs.

Safety boundaries:

- `extract docx` rejects missing inputs, non-`.docx` inputs, empty files, invalid ZIP/OpenXML packages, packages without `word/document.xml`, unsafe ZIP entry paths, unsafe relationship targets, excessive entry counts, excessive expanded size, and high compression ratios.
- Internal relationship targets are normalized inside the package root. External relationship targets are limited to `http`, `https`, and `mailto`; local file and UNC-style targets fail intake.
- Intake workspace outputs stay inside the configured private workspace.
- Extraction, structure-draft, and intake failure reports include `reportVersion: "1.0.0"` and use the normalized `diagnostics[]` contract with `category: "intake"` and severity `error`, `warning`, or `info`.
- The draft document is an intake prototype artifact. Rule-based drafting and explicit Codex review are not a formal DOCX import feature and must not be presented as guaranteed free-document parsing.

Current limits:

- no OCR by default;
- no Microsoft Word dependency;
- no screenshot-level visual diff;
- rule-based structuring is a draft, not a guarantee;
- effective run formatting is summarized conservatively; mixed run-level formatting remains available in each paragraph's `runs`;
- `formatClusters` reduce noise from messy direct formatting, but they do not replace human review for accepted templates;
- text wrapping, exact floating position, chart parts, SmartArt parts, and relationship-backed shapes are recorded as evidence but are not fully reconstructed as structured thesis blocks;
- relationship-free text boxes and shapes can be preserved through safe raw XML passthrough;
- image crop, table borders, and nested tables are reconstructed where they fit the supported `ThesisDocument` model;
- uncertain items require human review.

Commands:

```bash
dotnet run --project src/ThesisDocx.Cli -- extract docx \
  --input onboarding-workspaces/docx-structure-pilot/input/input.docx \
  --out onboarding-workspaces/docx-structure-pilot/extraction/extraction.json \
  --text onboarding-workspaces/docx-structure-pilot/extraction/plain-text.txt \
  --markdown onboarding-workspaces/docx-structure-pilot/extraction/extracted.md

dotnet run --project src/ThesisDocx.Cli -- extract format-candidates \
  --extraction onboarding-workspaces/docx-structure-pilot/extraction/extraction.json \
  --out onboarding-workspaces/docx-structure-pilot/structured/candidate-format-spec.json \
  --report onboarding-workspaces/docx-structure-pilot/structured/format-candidate-report.json \
  --markdown onboarding-workspaces/docx-structure-pilot/structured/format-candidate-report.md

dotnet run --project src/ThesisDocx.Cli -- template scaffold-candidate-decisions \
  --candidate-format onboarding-workspaces/docx-structure-pilot/structured/candidate-format-spec.json \
  --candidate-report onboarding-workspaces/docx-structure-pilot/structured/format-candidate-report.json \
  --reviewer reviewer-id \
  --out onboarding-workspaces/docx-structure-pilot/structured/format-candidate-decisions.json

dotnet run --project src/ThesisDocx.Cli -- template propose-from-candidate \
  --template examples/templates/example-university-engineering \
  --candidate-format onboarding-workspaces/docx-structure-pilot/structured/candidate-format-spec.json \
  --candidate-report onboarding-workspaces/docx-structure-pilot/structured/format-candidate-report.json \
  --decisions onboarding-workspaces/docx-structure-pilot/structured/format-candidate-decisions.json \
  --out onboarding-workspaces/docx-structure-pilot/proposed-template \
  --report onboarding-workspaces/docx-structure-pilot/reports/template-candidate-proposal-report.json \
  --markdown onboarding-workspaces/docx-structure-pilot/reports/template-candidate-proposal-report.md

dotnet run --project src/ThesisDocx.Cli -- structure draft \
  --extraction onboarding-workspaces/docx-structure-pilot/extraction/extraction.json \
  --out onboarding-workspaces/docx-structure-pilot/structured/thesis-document.draft.json \
  --report onboarding-workspaces/docx-structure-pilot/structured/structure-mapping-report.json \
  --unresolved onboarding-workspaces/docx-structure-pilot/structured/unresolved-items.json

dotnet run --project src/ThesisDocx.Cli -- structure codex-review \
  --workspace onboarding-workspaces/docx-structure-pilot \
  --extraction onboarding-workspaces/docx-structure-pilot/extraction/extraction.json \
  --template examples/templates/example-university-engineering

dotnet run --project src/ThesisDocx.Cli -- intake docx \
  --input onboarding-workspaces/docx-structure-pilot/input/input.docx \
  --workspace onboarding-workspaces/docx-structure-pilot \
  --template examples/templates/example-university-engineering \
  --structure-mode codex-required

dotnet run --project src/ThesisDocx.Cli -- intake gate \
  --input onboarding-workspaces/docx-structure-pilot/input/input.docx \
  --workspace onboarding-workspaces/docx-structure-pilot \
  --template examples/templates/example-university-engineering
```
