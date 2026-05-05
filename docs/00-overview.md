# Overview

ThesisDocx is a Stage 1 rendering engine for Chinese graduation thesis DOCX formatting.

The engine receives structured content and declarative formatting:

```text
ThesisDocument + ThesisFormatSpec -> DOCX
```

It deliberately does not accept messy Word files in this stage. Stage 2 can use AI to convert user uploads and college documents into the JSON model used here.

## Current Deliverables

- Core C# library in `src/ThesisDocx.Core`.
- CLI in `src/ThesisDocx.Cli`.
- API placeholder in `src/ThesisDocx.Api`.
- xUnit tests in `tests/ThesisDocx.Tests`.
- example thesis and format spec JSON under `examples`.
- validation and normalized snapshot tooling.
- formal JSON Schema files and semantic input validation.
- real footnote/endnote OpenXML parts.
- real OMML equation rendering and advanced table rendering.
- reusable `TemplatePackage` examples with inheritance, variables, assets, cover/declaration page templates, diff, and coverage reporting.

## Quality Bar

Generated DOCX files must pass `OpenXmlValidator`. Tests must inspect WordprocessingML and OMML nodes and attributes, including styles, numbering, section properties, table borders and merges, drawings, equations, field codes, bookmarks, and references.

## Template Packages

`TemplatePackage` wraps a declarative `ThesisFormatSpec` with reusable metadata: fictional school/college labels, variables, assets, page layout templates, inheritance, and compliance notes. Templates are data. Renderer code must not branch on a school name.

The implemented template examples use fictional names such as Example University and Example Engineering College.
