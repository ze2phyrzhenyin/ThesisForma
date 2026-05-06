---
name: web-editor-api
description: Use when working on ASP.NET Core API endpoints for templates, documents, validation, rendering, runs, downloads, image assets, import/export JSON, or MVP runtime file storage.
---

# Web Editor API

The API is an application layer over Core.

Rules:

- Do not bypass Core models, validators, template resolver, renderer, or inspect tools.
- Do not write user uploads, documents, or runs into `examples/`.
- Keep runtime artifacts under `runtime/` or a configured workspace path.
- Validate ids and paths; reject traversal and unsafe asset types.
- Return structured errors with actionable codes and issue lists.
- Keep render output tied to a run id with inspect and validation artifacts.
- Tests should cover create/save/validate/render/download/upload/error behavior.

Primary paths:

- `src/ThesisDocx.Api`
- `tests/ThesisDocx.Tests`
