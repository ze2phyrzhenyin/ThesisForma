# Structured Document API

`src/ThesisDocx.Api` exposes an API for structured thesis drafts. It is a thin application layer over Core models, validators, template resolution, renderer, inspector, and runtime file storage.

## Endpoints

- `GET /api/templates`
- `GET /api/templates/{id}`
- `POST /api/documents`
- `GET /api/documents/{id}`
- `PUT /api/documents/{id}`
- `POST /api/documents/{id}/validate`
- `POST /api/documents/{id}/render`
- `GET /api/runs/{runId}`
- `GET /api/runs/{runId}/download`
- `POST /api/assets/images`
- `GET /api/assets/{assetId}`
- `POST /api/documents/{id}/export-json`
- `POST /api/documents/import-json`

## Storage

The API uses file storage under `runtime/`:

- `runtime/documents`: saved `ThesisDocument` drafts and metadata
- `runtime/assets`: uploaded image assets and asset metadata
- `runtime/runs`: generated DOCX files, inspect summaries, validation output, and run metadata

This storage is intentionally simple for the MVP. It can be replaced by database/object storage later without changing the Core renderer contract.

## Safety

Document ids, run ids, and asset ids are constrained to safe characters. Image upload accepts common image MIME types and enforces a 5 MB size limit.

API errors are structured JSON with stable machine-readable fields:

```json
{
  "code": "document.validationFailed",
  "message": "ThesisDocument failed input validation.",
  "path": "$",
  "issues": [
    {
      "code": "thesis.schemaVersion.unsupported",
      "message": "Unsupported ThesisDocument schemaVersion '9.9.9'.",
      "path": "$.schemaVersion",
      "severity": "error",
      "suggestedAction": "Use a supported ThesisDocument schema version."
    }
  ]
}
```

`issues` is always present; simple errors use an empty array. Diagnostic issues always include `code`, `message`, `path`, `severity`, and `suggestedAction`. Successful JSON responses must not expose local absolute filesystem paths. Template paths are returned as repository-relative public paths, rendered run metadata uses relative artifact names such as `document.docx`, and downloads use API URLs.

The API must not write user content into `examples/` and must not bypass schema or semantic validation.
