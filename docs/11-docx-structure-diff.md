# DOCX Structure Diff

`DocxStructureDiffEngine` compares generated DOCX files as OpenXML packages. It does not automate Microsoft Word, render pages, or compare screenshots.

## What It Compares

- package part presence and absence;
- canonicalized WordprocessingML XML markers;
- section page setup and margins;
- heading style definitions;
- TOC/PAGE/REF field counts;
- table border and width markers;
- drawing extents;
- footnote/endnote part presence and ids;
- custom document properties.

## What It Ignores

- `rsid` attributes;
- relationship id value churn such as `rId7` vs `rId9`;
- DrawingML `docPr` ids;
- core property created/modified timestamps;
- ZIP entry order;
- OpenXML package metadata service parts.

## Severity

Changes are classified as `info`, `warning`, or `breaking`. Page setup, heading style, TOC/PAGE fields, tables, figures, and notes are treated as breaking because they affect layout conformance.

## CLI

```bash
dotnet run --project src/ThesisDocx.Cli -- docx diff \
  --base out/template-full.docx \
  --target out/template-full.docx \
  --json \
  --out out/template-full.self-diff.json
```

This is a structural quality gate. It is not a guarantee that thesis content is semantically correct.

