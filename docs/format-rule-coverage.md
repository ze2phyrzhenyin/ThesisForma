# Format Rule Coverage

The coverage reporter currently emits core categories for:

1. page setup
2. section/page numbering
3. fonts
4. paragraph
5. headings
6. header/footer
7. TOC
8. figures
9. tables
10. equations
11. citations/cross references
12. footnotes/endnotes
13. bibliography
14. cover/declaration page templates
15. assets
16. template inheritance

The matrix is intentionally conservative. Image assets are marked partial because font assets are metadata-only. Page templates are marked partial because the implemented DSL covers common cover/declaration layouts but not arbitrary positioning.

Generate a matrix:

```bash
dotnet run --project src/ThesisDocx.Cli -- template coverage \
  --template examples/templates/example-university-engineering \
  --out out/template.coverage.json
```

