# Layout Signature

`DocxLayoutSignatureExtractor` reads a DOCX with the OpenXML SDK and extracts an approximate layout signature. It does not call Word and does not create screenshots.

## Extracted Fields

- page size, margins, header/footer distances;
- section count and page numbering;
- default/body/heading style font and paragraph data;
- table count, widths, borders, gridSpan, vMerge, repeated header rows, cantSplit;
- figure count, drawing sizes, captions;
- equation OMML count, numbering, bookmarks;
- TOC/PAGE/REF fields;
- footnotes/endnotes;
- bibliography hanging indent;
- custom document properties.

## Comparison

`LayoutSignatureComparer` flattens two signatures, compares structural fields, computes a `similarityScore` from `0` to `1`, and applies a caller-provided threshold.

CLI:

```bash
dotnet run --project src/ThesisDocx.Cli -- docx layout-signature \
  --docx out/template-full.docx \
  --out out/template-full.layout.json

dotnet run --project src/ThesisDocx.Cli -- docx layout-compare \
  --base out/template-full.layout.json \
  --target out/template-full.layout.json \
  --threshold 0.99 \
  --out out/template-full.layout-compare.json
```

The signature is intentionally approximate. It is suitable for regression gates, not pixel-level visual acceptance.

