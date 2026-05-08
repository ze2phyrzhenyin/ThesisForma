/**
 * TypeScript types mirroring schemas/thesis-document.schema.json.
 * Keep in sync manually — schema versions accepted: 1.0.0, 1.1.0.
 */

export type SchemaVersion = '1.0.0' | '1.1.0';

export type TextAlignment = 'left' | 'center' | 'right' | 'both';

export type SectionKind =
  | 'cover'
  | 'originalityStatement'
  | 'abstract'
  | 'toc'
  | 'body'
  | 'acknowledgements'
  | 'bibliography'
  | 'appendix';

export type ImageContentType =
  | 'image/png'
  | 'image/jpeg'
  | 'image/jpg'
  | 'image/gif'
  | 'image/bmp'
  | 'image/tiff';

export interface Metadata {
  title: string;
  subtitle?: string;
  author: string;
  college: string;
  major: string;
  studentId: string;
  advisor: string;
  date: string;
  language: string;
}

// ───── Inlines ─────────────────────────────────────────────────────────────

export interface TextInline {
  type: 'text';
  text: string;
  bold?: boolean;
  italic?: boolean;
  underline?: boolean;
  verticalAlignment?: 'baseline' | 'subscript' | 'superscript';
}

export interface HyperlinkInline {
  type: 'hyperlink';
  text: string;
  uri: string;
}

export interface CitationInline {
  type: 'citation';
  targetId: string;
  displayText: string;
}

export interface BookmarkInline {
  type: 'bookmark';
  name: string;
  inlines: Inline[];
}

export interface ReferenceInline {
  type: 'reference';
  bookmarkName: string;
  fallbackText?: string;
}

export interface FootnoteInline {
  type: 'footnote';
  noteId: string;
  inlines: Inline[];
}

export interface EndnoteInline {
  type: 'endnote';
  noteId: string;
  inlines: Inline[];
}

export type Inline =
  | TextInline
  | HyperlinkInline
  | CitationInline
  | BookmarkInline
  | ReferenceInline
  | FootnoteInline
  | EndnoteInline;

// ───── Blocks ──────────────────────────────────────────────────────────────

export interface ParagraphBlock {
  type: 'paragraph';
  id?: string;
  inlines: Inline[];
  styleId?: string;
  alignment?: TextAlignment;
}

export interface HeadingBlock {
  type: 'heading';
  id?: string;
  level: 1 | 2 | 3 | 4 | 5 | 6;
  inlines: Inline[];
  bookmarkName?: string;
  numbered?: boolean;
}

export interface ListItem {
  blocks: Block[];
}

export interface ListBlock {
  type: 'list';
  id?: string;
  ordered?: boolean;
  items: ListItem[];
}

export interface FigureBlock {
  type: 'figure';
  id?: string;
  caption: string;
  imagePath?: string;
  imageDataBase64?: string;
  imageContentType: ImageContentType;
  widthCm?: number;
  heightCm?: number;
}

export interface TableWidth {
  type: 'auto' | 'percent' | 'dxa';
  value?: number;
}

export interface BorderSide {
  style?: 'nil' | 'none' | 'single' | 'double' | 'dotted' | 'dashed';
  size?: number;
  color?: string;
  space?: number;
}

export interface Borders {
  top?: BorderSide;
  bottom?: BorderSide;
  left?: BorderSide;
  right?: BorderSide;
  insideH?: BorderSide;
  insideV?: BorderSide;
}

export interface CellMargins {
  topCm?: number;
  bottomCm?: number;
  leftCm?: number;
  rightCm?: number;
}

export interface TableCellFont {
  eastAsia?: string;
  latin?: string;
  sizePt?: number;
  bold?: boolean;
  italic?: boolean;
}

export interface TableCellParagraph {
  lineSpacingMultiple?: number;
  spaceBeforePt?: number;
  spaceAfterPt?: number;
  firstLineIndentChars?: number;
  hangingIndentCm?: number;
  alignment?: TextAlignment;
  widowControl?: boolean;
}

export interface TableCell {
  id?: string;
  text?: string;
  blocks?: Block[];
  gridSpan?: number;
  verticalMerge?: 'none' | 'restart' | 'continue';
  width?: TableWidth;
  widthCm?: number;
  alignment?: TextAlignment;
  verticalAlignment?: 'top' | 'center' | 'bottom';
  shading?: string;
  borders?: Borders;
  cellMargins?: CellMargins;
  font?: TableCellFont;
  paragraph?: TableCellParagraph;
}

export interface TableRow {
  id?: string;
  isHeader?: boolean;
  cantSplit?: boolean;
  heightPt?: number;
  cells: TableCell[];
}

export interface TableBlock {
  type: 'table';
  id?: string;
  bookmarkId?: string;
  caption: string;
  captionPosition?: 'before' | 'after';
  style?: 'normal' | 'threeLine' | 'custom';
  width?: TableWidth;
  alignment?: TextAlignment;
  layout?: 'autofit' | 'fixed';
  allowRowBreakAcrossPages?: boolean;
  repeatHeaderRows?: number;
  borders?: Borders;
  cellMargins?: CellMargins;
  rows: TableRow[];
}

export interface QuoteBlock {
  type: 'quote';
  id?: string;
  inlines: Inline[];
}

export interface EquationNumbering {
  enabled: boolean;
  label?: string;
  format?: string;
  restartByHeadingLevel?: number;
}

export interface EquationBlock {
  type: 'equation';
  id?: string;
  bookmarkId?: string;
  bookmarkName?: string;
  placeholder?: string;
  sourceType?: 'omml' | 'latex' | 'plain';
  omml?: string;
  latex?: string;
  plainText?: string;
  display?: boolean;
  alignment?: TextAlignment;
  caption?: string;
  numbering?: EquationNumbering;
  allowWordUpdate?: boolean;
}

export interface PageBreakBlock {
  type: 'pageBreak';
  id?: string;
}

export interface SectionBreakBlock {
  type: 'sectionBreak';
  id?: string;
}

export interface BibliographyEntry {
  id: string;
  text: string;
}

export interface BibliographyBlock {
  type: 'bibliography';
  id?: string;
  entries: BibliographyEntry[];
}

export interface FootnoteBlock {
  type: 'footnote';
  id?: string;
  noteId: string;
  inlines: Inline[];
}

export interface EndnoteBlock {
  type: 'endnote';
  id?: string;
  noteId: string;
  inlines: Inline[];
}

export type Block =
  | ParagraphBlock
  | HeadingBlock
  | ListBlock
  | FigureBlock
  | TableBlock
  | QuoteBlock
  | EquationBlock
  | PageBreakBlock
  | SectionBreakBlock
  | BibliographyBlock
  | FootnoteBlock
  | EndnoteBlock;

export type BlockType = Block['type'];

// ───── Sections / Document ─────────────────────────────────────────────────

export interface Section {
  id?: string;
  kind: SectionKind;
  title?: string;
  startOnNewPage?: boolean;
  blocks: Block[];
}

export interface ThesisDocument {
  schemaVersion: SchemaVersion;
  metadata: Metadata;
  sections: Section[];
}
