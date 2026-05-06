# Web Editor Limitations

Current MVP limitations:

- no AI parsing in the web editor
- no Word-like free-layout editing
- no manual font, font-size, line-spacing, margin, header/footer, page-number, or table-border controls
- no visual DOCX preview
- no screenshot-level visual diff
- no collaboration, comments, version history, or database storage
- table cell merging UI is reserved for a later round
- equation editing is plain text / simple source entry only
- image assets are local preview metadata in Vercel frontend-only mode
- DOCX generation is disabled unless a separate backend API is configured
- mobile layout is usable but desktop is the primary target

Validation in the frontend is an authoring aid. Server-side schema and semantic validation remain authoritative.

## Frontend-Only Mode

On Vercel, the frontend can:

- create and edit structured thesis content
- save drafts in browser storage
- import ThesisDocument JSON
- export ThesisDocument JSON
- show local validation issues

It cannot:

- run OpenXML rendering
- persist user files on a server
- embed uploaded images into DOCX without backend asset handling
- guarantee final school-specific compliance without backend validation/gate reports
