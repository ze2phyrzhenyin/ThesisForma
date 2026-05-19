# Fictional CN Theatre Journal Article Template

This template is a fictional Chinese theatre-studies journal article scaffold. It is not a verified template for any real journal or institution.

Use `scaffold-document.json` as the content starting point. Keep the opening body section id as `article-body` if you want the template's block-level front-matter overrides for abstract, keywords, classification, article number, author bio, and project note.

## Rules

- Page: A4 portrait; margins top 2.3 cm, bottom 2.1 cm, left 2.6 cm, right 2.4 cm.
- Title block: rendered before `article-body` only; title centered in HeiTi 18 pt bold; optional subtitle centered in KaiTi 12 pt; author centered in SongTi 12 pt; journal and issue centered in SongTi 9.5 pt; separator rule below.
- Front matter paragraphs: `内容摘要：`, `关键词：`, `中图分类号/文献标识码`, `文章编号`, `作者简介`, and `基金项目` are not indented. Abstract and keyword labels are bold inline text.
- Body text: SongTi 10.5 pt, Times New Roman for Latin text, 1.15 line spacing, 4 pt after spacing, justified alignment, two-character first-line indentation.
- Primary section headings: use unnumbered heading blocks and write the Chinese section label directly, for example `一、一级标题`; centered HeiTi 12 pt bold; 18 pt before and 10 pt after.
- Secondary headings: left aligned HeiTi 11 pt bold; 8 pt before and 4 pt after.
- Third-level headings: left aligned SongTi 10.5 pt bold; 6 pt before and 2 pt after.
- Tables: three-line table style by default, caption above, full width.
- Footnotes/endnotes: SongTi 9 pt; enclosed Chinese circle numbering; footnotes restart each page.
- References: text uses bracketed numeric citation display only, such as `[1]`, and each `citation.targetId` must match a bibliography entry id. Body text should not add citation author names or years around the marker.
- Bibliography entries: rendered in document order with `[n]` numbering, SongTi 9.5 pt, hanging indent 0.74 cm. Each entry must include author, title, publication or journal source, year, and page or page range. Do not type `[1]` into the entry text; the renderer adds it from `bibliographyText`.

Render example:

```bash
dotnet run --project src/ThesisDocx.Cli -- render \
  --document examples/templates/fictional-cn-theatre-journal-article/scaffold-document.json \
  --template examples/templates/fictional-cn-theatre-journal-article \
  --var variables.journalName=中文戏剧研究辑刊 \
  --var variables.issueLabel=2026年第1期 \
  --out out/fictional-cn-theatre-journal-article.docx
```

Current heading numbering is decimal-only in the renderer's numbering definition. For Chinese section labels such as `一、`, write the label directly in an unnumbered heading block.
