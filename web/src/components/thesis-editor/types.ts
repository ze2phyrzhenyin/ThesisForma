export type SectionKind =
  | 'cover'
  | 'originalityStatement'
  | 'abstract'
  | 'toc'
  | 'body'
  | 'acknowledgements'
  | 'bibliography'
  | 'appendix';

export type InlineNode =
  | { type: 'text'; text: string; bold?: boolean; italic?: boolean; underline?: boolean; verticalAlignment?: 'superscript' | 'subscript' }
  | { type: 'citation'; targetId: string; displayText: string }
  | { type: 'reference'; bookmarkName: string; fallbackText?: string }
  | { type: 'footnote'; noteId: string; inlines: InlineNode[] };

export type TableCellDraft = { id: string; text: string; gridSpan?: number };
export type TableRowDraft = { id: string; isHeader?: boolean; cells: TableCellDraft[] };

export type BlockNode =
  | { type: 'heading'; id: string; level: number; text: string; bookmarkName?: string; numbered?: boolean }
  | { type: 'paragraph'; id: string; inlines: InlineNode[] }
  | { type: 'abstract'; id: string; language: 'zh' | 'en'; text: string; keywords: string[] }
  | { type: 'table'; id: string; caption: string; style: 'normal' | 'threeLine'; rows: TableRowDraft[]; bookmarkId?: string; repeatHeaderRows?: number }
  | { type: 'figure'; id: string; caption: string; altText: string; imagePath?: string; previewUrl?: string; imageContentType?: string; widthCm?: number }
  | { type: 'equation'; id: string; plainText: string; caption?: string; bookmarkName?: string }
  | { type: 'pageBreak'; id: string };

export type SectionNode = {
  id: string;
  kind: SectionKind;
  title: string;
  blocks: BlockNode[];
};

export type BibliographyEntry = {
  id: string;
  key: string;
  text: string;
  entryType: 'book' | 'journal' | 'web' | 'other';
};

export type AssetRef = {
  assetId: string;
  fileName: string;
  imagePath: string;
  previewUrl: string;
  contentType: string;
};

export type ValidationIssue = {
  code: string;
  severity: 'error' | 'warning' | 'info';
  message: string;
  blockId?: string;
  path?: string;
  suggestedAction?: string;
};

export type TemplateSummary = {
  id: string;
  name: string;
  school: string;
  college: string;
  version: string;
  status: string;
  coverage: number;
  readiness: string;
  tags: string[];
};

export type RenderRun = {
  runId: string;
  status: string;
  openXmlValid: boolean;
  formatValid: boolean;
  downloadUrl: string;
  issues: ValidationIssue[];
};

export type ThesisEditorState = {
  documentId?: string;
  templateId: string;
  template?: TemplateSummary;
  metadata: {
    title: string;
    subtitle: string;
    author: string;
    college: string;
    major: string;
    studentId: string;
    advisor: string;
    date: string;
  };
  sections: SectionNode[];
  bibliography: BibliographyEntry[];
  assets: AssetRef[];
  selectedBlockId?: string;
  validationIssues: ValidationIssue[];
  autosaveStatus: 'unsaved' | 'saving' | 'saved' | 'failed';
  renderRun?: RenderRun;
};
