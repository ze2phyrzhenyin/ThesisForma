# Frontend UI Audit

## Current Pages

- `/`: Frontend-only entry page for creating or importing a structured thesis.
- `/templates`: Template selection page backed by API or demo templates.
- `/editor/:documentId`: Three-column structured thesis editor.
- `/runs/:runId`: Render result placeholder page.

## Main UX Problems Found

- The editor worked as a demo but did not clearly communicate the product model: structure first, template controls formatting.
- The right column stacked unrelated panels, making validation, references, render status, and template information compete for attention.
- The insert menu was a flat list without academic grouping, so users could not quickly distinguish common content, academic markers, and document structure.
- Table editing was usable but cramped; caption, header row, row/column controls, and template constraints were not visually separated.
- Figure insertion did not clearly distinguish local preview from backend asset persistence.
- Validation issues were listed without severity grouping, summary counts, or strong suggested action hierarchy.

## Visual Problems Found

- Token coverage was too small and old variable names were mixed through the app.
- Cards, panels, block headers, and empty states used similar treatments, reducing information hierarchy.
- The editor canvas needed stronger paper-like structure without pretending to be Word.
- Several inline styles made spacing and alignment harder to audit.

## Component Duplication And Inconsistency

- Buttons, panels, badges, fields, modal, and empty states existed but lacked richer states such as inline alerts, status pills, tabs, segmented controls, and checkbox wrappers.
- Modal behavior did not handle Escape close.
- Tabs were missing, forcing right-side content into one long column.

## User Task Blockers

- It was possible to add blocks, but the insertion flow was not self-explanatory.
- Users could add a table without understanding that border styling is template-owned.
- Users could see “generate DOCX” disabled but needed a clearer backend requirement explanation.
- Bibliography management was hidden by visual noise rather than framed as the source of valid citation keys.

## Functions To Preserve

- Frontend-only mode must remain fully usable.
- Create/edit structured document state.
- Metadata, heading, paragraph, table, figure, equation, citations, cross references, bibliography, TOC preview.
- Import and export ThesisDocument JSON.
- Optional backend render through environment variables.
- Vercel build without dotnet.

## Refactor Scope

- Unified design tokens with CSS variables.
- Professional three-column editor shell.
- Right panel tabs: Properties, Validation, References, Template.
- Grouped InsertBlockMenu with table and figure modals.
- Clearer table, figure, bibliography, render, validation, template status components.
- Expanded component tests for UI copy, state changes, serialization, modal behavior, and frontend-only render behavior.

## Not In Scope

- Word-like free layout editing.
- Manual font, size, line-height, margin, or border styling in the frontend.
- Online DOCX rendering on Vercel without a separate backend API.
- AI parsing, OCR, multi-user editing, comments, version history, or visual DOCX preview.
