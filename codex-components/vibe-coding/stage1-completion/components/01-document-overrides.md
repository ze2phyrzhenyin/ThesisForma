# Component 01: DocumentOverrides Backend And Renderer

## Goal

Make `DocumentOverrides` a first-class render input:

`ThesisDocument + TemplatePackage + DocumentOverrides -> effective ThesisFormatSpec + valid DOCX`

The web editor may expose local controls, but final behavior must be persisted through the API and reflected in rendered OpenXML.

## Owners

- Schema: models, JSON schema, examples, docs.
- Web API: envelope DTOs, runtime storage, validation, render request flow.
- Renderer: effective spec merge, section-aware rendering behavior, XML-level tests.
- Validation: conformance checks against effective spec where applicable.
- Web editor: API round trip and localStorage migration.

## Write Scope

- `schemas/document-overrides.schema.json`
- `src/ThesisDocx.Core/Models`
- `src/ThesisDocx.Core/Rendering`
- `src/ThesisDocx.Core/Services`
- `src/ThesisDocx.Api`
- `web/src/editor/overrides.ts`
- `web/src/api`
- `tests/ThesisDocx.Tests`
- `web/src/tests`
- `docs/web-overrides-contract.md`
- `docs/33-web-editor-api.md`

## Required Behavior

- `DocumentEnvelope` includes optional `overrides`.
- `SaveDocumentRequest`, `ImportDocumentRequest`, and render requests accept optional `overrides`.
- API persists overrides beside document metadata without modifying `ThesisDocument`.
- Invalid overrides return structured API errors.
- Renderer merges template format spec plus overrides into an effective spec before package creation.
- Section bucket overrides apply to `cover`, `frontMatter`, and `body`.
- Section instance overrides win over bucket overrides.
- TOC min/max/title overrides affect generated field code.
- Header/footer/page-number overrides affect section properties and header/footer parts.

## Implementation Order

1. Add schema and C# model types for overrides.
2. Add API DTO fields and runtime persistence round trip.
3. Add pure effective-spec merger tests before renderer integration.
4. Integrate effective spec into render path.
5. Add section instance rendering support only after bucket overrides are covered.
6. Connect web API payloads and migrate old localStorage overrides when opening a document.
7. Update docs and examples.

## Acceptance Gates

```bash
dotnet test ThesisDocx.slnx --nologo --filter "FullyQualifiedName~Overrides"
dotnet test ThesisDocx.slnx --nologo
npm --prefix web run typecheck
npm --prefix web test -- overrides
scripts/ci-quality-gate
```

Required test evidence:

- schema accepts valid overrides and rejects out-of-range values;
- API create/save/import/get round-trips overrides;
- rendered `document.xml` has expected TOC field instruction changes;
- rendered `sectPr` has expected page numbering and header/footer references;
- `OpenXmlValidator` passes for override-rendered DOCX.

## Boundaries

- Do not add formatting fields to `ThesisDocument`.
- Do not expose free-form Word-like paragraph controls outside the documented override layer.
- Do not hardcode a school or template id.

