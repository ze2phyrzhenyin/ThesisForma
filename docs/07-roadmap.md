# Roadmap

## Stage 1 MVP

Completed in this repository:

- structured document and format models;
- formal JSON Schema files;
- semantic input validation;
- deterministic DOCX rendering;
- real footnote and endnote parts;
- real OMML equation rendering with numbering, bookmarks, and REF references;
- advanced table rendering for merged cells, repeat header rows, cantSplit rows, widths, layouts, margins, vertical alignment, and border overrides;
- template package system with inheritance, variables, assets, cover/declaration page templates, diff, and coverage matrix;
- DOCX structure diff, layout signatures, template regression suites, template gate reports, and format fixture coverage;
- template author quality workbench CLI for requirement capture, baseline management, diagnostics, fix hints, and authoring reports;
- OpenXML validation;
- XML-level regression tests;
- CLI render/validate/inspect/snapshot;
- examples and documentation.

## Stage 1 Next

Highest-priority engineering work:

1. Add generated schema documentation from JSON Schema.
2. Add deeper XML snapshots that normalize relationship ids throughout raw parts.
3. Add more fictional college format specs and template packages as declarative fixtures.
4. Continue expanding configurable footnote/endnote styles beyond the current `ThesisFormatSpec.notes` paragraph/font/reference-mark controls.
5. Expand equation rendering beyond the current small LaTeX subset without introducing platform-specific converters.
6. Add more approved table cell block surfaces beyond the current paragraph/heading/quote/list/note subset while preserving XML-level tests.
7. Add more page template blocks beyond the current stable `rule` separator block, still rendered as stable WordprocessingML.
8. Add larger real-college onboarding playbooks with legal/human review before storing any real institutional requirements.
9. Add richer remediation knowledge to `FixHintEngine` as new validators and fixtures are added.

## Stage 2

Stage 2 can introduce:

- upload API and web UI;
- AI parser that maps messy thesis documents into `ThesisDocument`;
- AI extractor that maps college requirements into `ThesisFormatSpec`;
- review workflow for user approval before DOCX generation;
- AI-assisted template suggestion, producing the same `TemplatePackage` and `ThesisFormatSpec` contracts.
- optional visual QA outside the deterministic core; the current engine intentionally avoids screenshot-level diff.
## Onboarding Status

Implemented:

- private onboarding workspace manifest and CLI;
- privacy scanning for examples/workspaces/packages;
- deterministic template pilot package generation and validation;
- onboarding summary markdown/JSON reports;
- CI integration for the fictional onboarding example.

Still planned:

- richer human review UI outside this repository;
- broader real-college pilot checklist automation;
- optional AI-assisted draft requirement capture in a later phase, behind human review.
