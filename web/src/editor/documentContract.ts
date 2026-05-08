import type {
  ApiIssue,
  Block,
  DocumentEnvelope,
  EndnoteInline,
  FootnoteInline,
  Inline,
  Section,
  SectionKind,
  TableCell,
  TableRow,
  ThesisDocument
} from '@/types';

const SUPPORTED_SCHEMA_VERSIONS = new Set(['1.0.0', '1.1.0']);
const SECTION_KINDS = new Set<SectionKind>([
  'cover',
  'originalityStatement',
  'abstract',
  'toc',
  'body',
  'acknowledgements',
  'bibliography',
  'appendix'
]);

export interface ParseDocumentResult {
  ok: boolean;
  document?: ThesisDocument;
  issues: ApiIssue[];
}

export function parseThesisDocumentJson(text: string): ParseDocumentResult {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch (error) {
    return {
      ok: false,
      issues: [
        issue(
          'json.parse',
          error instanceof Error ? `JSON 解析失败：${error.message}` : 'JSON 解析失败。',
          'error',
          '$',
          '请确认文件是合法 JSON。'
        )
      ]
    };
  }

  return normalizeThesisDocument(parsed);
}

export function normalizeThesisDocument(value: unknown): ParseDocumentResult {
  const issues: ApiIssue[] = [];
  if (!isRecord(value)) {
    return {
      ok: false,
      issues: [issue('document.type', 'ThesisDocument 必须是 JSON object。', 'error', '$')]
    };
  }

  const schemaVersion = value.schemaVersion;
  if (typeof schemaVersion !== 'string') {
    issues.push(
      issue('document.schemaVersion.required', '缺少 schemaVersion。', 'error', '$.schemaVersion')
    );
  } else if (!SUPPORTED_SCHEMA_VERSIONS.has(schemaVersion)) {
    issues.push(
      issue(
        'document.schemaVersion.unsupported',
        `暂不支持 schemaVersion=${schemaVersion}。`,
        'error',
        '$.schemaVersion'
      )
    );
  }

  if (!isRecord(value.metadata)) {
    issues.push(issue('document.metadata.required', '缺少 metadata。', 'error', '$.metadata'));
  }
  if (!Array.isArray(value.sections)) {
    issues.push(issue('document.sections.required', '缺少 sections 数组。', 'error', '$.sections'));
  }

  if (issues.some((i) => i.severity === 'error')) {
    return { ok: false, issues };
  }

  const document: ThesisDocument = {
    schemaVersion: schemaVersion as ThesisDocument['schemaVersion'],
    metadata: normalizeMetadata(value.metadata as Record<string, unknown>, issues),
    sections: normalizeSections(value.sections as unknown[], issues)
  };

  if (document.sections.length === 0) {
    issues.push(
      issue('document.sections.empty', '至少需要一个 section。', 'error', '$.sections')
    );
  }

  issues.push(...validateThesisDocument(document));
  return {
    ok: !issues.some((i) => i.severity === 'error'),
    document,
    issues
  };
}

export function cleanThesisDocument(document: ThesisDocument): ThesisDocument {
  return stripNulls({
    schemaVersion: document.schemaVersion,
    metadata: {
      title: document.metadata.title,
      ...(document.metadata.subtitle ? { subtitle: document.metadata.subtitle } : {}),
      author: document.metadata.author,
      college: document.metadata.college,
      major: document.metadata.major,
      studentId: document.metadata.studentId,
      advisor: document.metadata.advisor,
      date: document.metadata.date,
      language: document.metadata.language
    },
    sections: document.sections.map(cleanSection)
  });
}

export function validateThesisDocument(document: ThesisDocument): ApiIssue[] {
  const issues: ApiIssue[] = [];
  if (!SUPPORTED_SCHEMA_VERSIONS.has(document.schemaVersion)) {
    issues.push(
      issue(
        'document.schemaVersion.unsupported',
        `暂不支持 schemaVersion=${document.schemaVersion}。`,
        'error',
        '$.schemaVersion'
      )
    );
  }
  validateMetadata(document, issues);
  validateSections(document, issues);
  validateBibliographyCitations(document, issues);
  validateReferences(document, issues);
  validateNotes(document, issues);
  return issues;
}

export function exportFileNameForDocument(document: ThesisDocument): string {
  const parts = [
    document.metadata.title || 'thesis-document',
    document.metadata.author,
    document.metadata.date
  ].filter(Boolean);
  return `${safeFilePart(parts.join('-') || 'thesis-document')}.json`;
}

export function downloadJson(filename: string, value: unknown): void {
  const blob = new Blob([`${JSON.stringify(value, null, 2)}\n`], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export function makeDocumentEnvelope(
  id: string,
  document: ThesisDocument,
  templateId?: string | null,
  updatedAt = new Date().toISOString()
): DocumentEnvelope {
  return {
    id,
    templateId: templateId ?? null,
    document: cleanThesisDocument(document),
    updatedAt
  };
}

function normalizeMetadata(metadata: Record<string, unknown>, issues: ApiIssue[]) {
  const normalized = {
    title: stringValue(metadata.title),
    ...(typeof metadata.subtitle === 'string' ? { subtitle: metadata.subtitle } : {}),
    author: stringValue(metadata.author),
    college: stringValue(metadata.college),
    major: stringValue(metadata.major),
    studentId: stringValue(metadata.studentId),
    advisor: stringValue(metadata.advisor),
    date: stringValue(metadata.date),
    language: stringValue(metadata.language) || 'zh-CN'
  };

  for (const key of Object.keys(metadata)) {
    if (!(key in normalized)) {
      issues.push(
        issue(
          'document.unknownProperty',
          `metadata.${key} 不是当前 schema 字段，导入时会忽略。`,
          'warning',
          `$.metadata.${key}`
        )
      );
    }
  }
  return normalized;
}

function normalizeSections(values: unknown[], issues: ApiIssue[]): Section[] {
  const sections: Section[] = [];
  values.forEach((value, index) => {
    const path = `$.sections[${index}]`;
    if (!isRecord(value)) {
      issues.push(issue('section.type', 'section 必须是 object。', 'error', path));
      return;
    }
    if (!SECTION_KINDS.has(value.kind as SectionKind)) {
      issues.push(issue('section.kind.invalid', 'section.kind 不受支持。', 'error', `${path}.kind`));
      return;
    }
    if (!Array.isArray(value.blocks)) {
      issues.push(issue('section.blocks.required', 'section.blocks 必须是数组。', 'error', `${path}.blocks`));
      return;
    }
    const section: Section = {
      ...(typeof value.id === 'string' ? { id: value.id } : {}),
      kind: value.kind as SectionKind,
      ...(typeof value.title === 'string' ? { title: value.title } : {}),
      ...(typeof value.startOnNewPage === 'boolean' ? { startOnNewPage: value.startOnNewPage } : {}),
      blocks: normalizeBlocks(value.blocks, `${path}.blocks`, issues)
    };
    sections.push(section);
  });
  return sections;
}

function normalizeBlocks(values: unknown[], path: string, issues: ApiIssue[]): Block[] {
  const blocks: Block[] = [];
  values.forEach((value, index) => {
    const block = normalizeBlock(value, `${path}[${index}]`, issues);
    if (block) blocks.push(block);
  });
  return blocks;
}

function normalizeBlock(value: unknown, path: string, issues: ApiIssue[]): Block | null {
  if (!isRecord(value) || typeof value.type !== 'string') {
    issues.push(issue('block.type.required', 'block.type 缺失或非法。', 'error', `${path}.type`));
    return null;
  }

  const id = typeof value.id === 'string' ? { id: value.id } : {};
  switch (value.type) {
    case 'paragraph':
      return {
        type: 'paragraph',
        ...id,
        inlines: normalizeInlines(value.inlines, `${path}.inlines`, issues),
        ...(typeof value.styleId === 'string' ? { styleId: value.styleId } : {}),
        ...(isAlignment(value.alignment) ? { alignment: value.alignment } : {})
      };
    case 'heading':
      return {
        type: 'heading',
        ...id,
        level: normalizeHeadingLevel(value.level),
        inlines: normalizeInlines(value.inlines, `${path}.inlines`, issues),
        ...(typeof value.bookmarkName === 'string' ? { bookmarkName: value.bookmarkName } : {}),
        ...(typeof value.numbered === 'boolean' ? { numbered: value.numbered } : {})
      };
    case 'list':
      return {
        type: 'list',
        ...id,
        ...(typeof value.ordered === 'boolean' ? { ordered: value.ordered } : {}),
        items: Array.isArray(value.items)
          ? value.items.map((item, itemIndex) => ({
              blocks: isRecord(item) && Array.isArray(item.blocks)
                ? normalizeBlocks(item.blocks, `${path}.items[${itemIndex}].blocks`, issues)
                : []
            }))
          : []
      };
    case 'figure':
      return {
        type: 'figure',
        ...id,
        caption: stringValue(value.caption),
        ...(typeof value.imagePath === 'string' ? { imagePath: value.imagePath } : {}),
        ...(typeof value.imageDataBase64 === 'string' ? { imageDataBase64: value.imageDataBase64 } : {}),
        imageContentType: isImageContentType(value.imageContentType) ? value.imageContentType : 'image/png',
        ...(typeof value.widthCm === 'number' ? { widthCm: value.widthCm } : {}),
        ...(typeof value.heightCm === 'number' ? { heightCm: value.heightCm } : {})
      };
    case 'table':
      return {
        type: 'table',
        ...id,
        ...(typeof value.bookmarkId === 'string' ? { bookmarkId: value.bookmarkId } : {}),
        caption: stringValue(value.caption),
        ...(value.captionPosition === 'before' || value.captionPosition === 'after'
          ? { captionPosition: value.captionPosition }
          : {}),
        ...(value.style === 'threeLine' || value.style === 'custom' || value.style === 'normal'
          ? { style: value.style }
          : {}),
        ...(isRecord(value.width) ? { width: value.width as unknown as Extract<Block, { type: 'table' }>['width'] } : {}),
        ...(isAlignment(value.alignment) ? { alignment: value.alignment } : {}),
        ...(value.layout === 'autofit' || value.layout === 'fixed' ? { layout: value.layout } : {}),
        ...(typeof value.allowRowBreakAcrossPages === 'boolean'
          ? { allowRowBreakAcrossPages: value.allowRowBreakAcrossPages }
          : {}),
        ...(typeof value.repeatHeaderRows === 'number' ? { repeatHeaderRows: value.repeatHeaderRows } : {}),
        ...(isRecord(value.borders) ? { borders: value.borders as Extract<Block, { type: 'table' }>['borders'] } : {}),
        ...(isRecord(value.cellMargins)
          ? { cellMargins: value.cellMargins as Extract<Block, { type: 'table' }>['cellMargins'] }
          : {}),
        rows: Array.isArray(value.rows)
          ? value.rows.map((row, rowIndex) => normalizeTableRow(row, `${path}.rows[${rowIndex}]`, issues)).filter(isTableRow)
          : []
      };
    case 'quote':
      return { type: 'quote', ...id, inlines: normalizeInlines(value.inlines, `${path}.inlines`, issues) };
    case 'equation':
      return {
        type: 'equation',
        ...id,
        ...(typeof value.bookmarkId === 'string' ? { bookmarkId: value.bookmarkId } : {}),
        ...(typeof value.bookmarkName === 'string' ? { bookmarkName: value.bookmarkName } : {}),
        ...(typeof value.placeholder === 'string' ? { placeholder: value.placeholder } : {}),
        ...(value.sourceType === 'omml' || value.sourceType === 'latex' || value.sourceType === 'plain'
          ? { sourceType: value.sourceType }
          : {}),
        ...(typeof value.omml === 'string' ? { omml: value.omml } : {}),
        ...(typeof value.latex === 'string' ? { latex: value.latex } : {}),
        ...(typeof value.plainText === 'string' ? { plainText: value.plainText } : {}),
        ...(typeof value.display === 'boolean' ? { display: value.display } : {}),
        ...(isAlignment(value.alignment) ? { alignment: value.alignment } : {}),
        ...(typeof value.caption === 'string' ? { caption: value.caption } : {}),
        ...(isRecord(value.numbering)
          ? {
              numbering: {
                enabled: value.numbering.enabled === true,
                ...(typeof value.numbering.label === 'string' ? { label: value.numbering.label } : {}),
                ...(typeof value.numbering.format === 'string' ? { format: value.numbering.format } : {}),
                ...(typeof value.numbering.restartByHeadingLevel === 'number'
                  ? { restartByHeadingLevel: value.numbering.restartByHeadingLevel }
                  : {})
              }
            }
          : {}),
        ...(typeof value.allowWordUpdate === 'boolean' ? { allowWordUpdate: value.allowWordUpdate } : {})
      };
    case 'pageBreak':
      return { type: 'pageBreak', ...id };
    case 'sectionBreak':
      return { type: 'sectionBreak', ...id };
    case 'bibliography':
      return {
        type: 'bibliography',
        ...id,
        entries: Array.isArray(value.entries)
          ? value.entries
              .filter(isRecord)
              .map((entry) => ({ id: stringValue(entry.id), text: stringValue(entry.text) }))
          : []
      };
    case 'footnote':
      return {
        type: 'footnote',
        ...id,
        noteId: stringValue(value.noteId),
        inlines: normalizeInlines(value.inlines, `${path}.inlines`, issues)
      };
    case 'endnote':
      return {
        type: 'endnote',
        ...id,
        noteId: stringValue(value.noteId),
        inlines: normalizeInlines(value.inlines, `${path}.inlines`, issues)
      };
    default:
      issues.push(
        issue('block.type.unsupported', `不支持的 block.type=${value.type}，导入时会忽略。`, 'warning', `${path}.type`)
      );
      return null;
  }
}

function normalizeTableRow(value: unknown, path: string, issues: ApiIssue[]): TableRow | null {
  if (!isRecord(value)) return null;
  return {
    ...(typeof value.id === 'string' ? { id: value.id } : {}),
    ...(typeof value.isHeader === 'boolean' ? { isHeader: value.isHeader } : {}),
    ...(typeof value.cantSplit === 'boolean' ? { cantSplit: value.cantSplit } : {}),
    ...(typeof value.heightPt === 'number' ? { heightPt: value.heightPt } : {}),
    cells: Array.isArray(value.cells)
      ? value.cells.map((cell, cellIndex) => normalizeTableCell(cell, `${path}.cells[${cellIndex}]`, issues)).filter(isTableCell)
      : []
  };
}

function isTableRow(value: TableRow | null): value is TableRow {
  return value !== null;
}

function normalizeTableCell(value: unknown, path: string, issues: ApiIssue[]): TableCell | null {
  if (!isRecord(value)) return null;
  return {
    ...(typeof value.id === 'string' ? { id: value.id } : {}),
    ...(typeof value.text === 'string' ? { text: value.text } : { text: '' }),
    ...(typeof value.gridSpan === 'number' && value.gridSpan > 1 ? { gridSpan: value.gridSpan } : {}),
    ...(value.verticalMerge === 'none' || value.verticalMerge === 'restart' || value.verticalMerge === 'continue'
      ? { verticalMerge: value.verticalMerge }
      : {}),
    ...(isRecord(value.width) ? { width: value.width as unknown as TableCell['width'] } : {}),
    ...(typeof value.widthCm === 'number' ? { widthCm: value.widthCm } : {}),
    ...(isAlignment(value.alignment) ? { alignment: value.alignment } : {}),
    ...(value.verticalAlignment === 'top' || value.verticalAlignment === 'center' || value.verticalAlignment === 'bottom'
      ? { verticalAlignment: value.verticalAlignment }
      : {}),
    ...(typeof value.shading === 'string' ? { shading: value.shading } : {}),
    ...(isRecord(value.borders) ? { borders: value.borders as TableCell['borders'] } : {}),
    ...(isRecord(value.cellMargins) ? { cellMargins: value.cellMargins as TableCell['cellMargins'] } : {}),
    ...(isRecord(value.font) ? { font: value.font as TableCell['font'] } : {}),
    ...(isRecord(value.paragraph) ? { paragraph: value.paragraph as TableCell['paragraph'] } : {}),
    ...(Array.isArray(value.blocks) ? { blocks: normalizeBlocks(value.blocks, `${path}.blocks`, issues) } : {})
  };
}

function isTableCell(value: TableCell | null): value is TableCell {
  return value !== null;
}

function normalizeInlines(value: unknown, path: string, issues: ApiIssue[]): Inline[] {
  if (!Array.isArray(value)) return [];
  const inlines: Inline[] = [];
  value.forEach((inline, index) => {
    const normalized = normalizeInline(inline, `${path}[${index}]`, issues);
    if (normalized) inlines.push(normalized);
  });
  return inlines;
}

function normalizeInline(value: unknown, path: string, issues: ApiIssue[]): Inline | null {
  if (!isRecord(value) || typeof value.type !== 'string') {
    issues.push(issue('inline.type.required', 'inline.type 缺失或非法。', 'warning', `${path}.type`));
    return null;
  }
  switch (value.type) {
    case 'text':
      return {
        type: 'text',
        text: stringValue(value.text),
        ...(value.bold === true ? { bold: true } : {}),
        ...(value.italic === true ? { italic: true } : {}),
        ...(value.underline === true ? { underline: true } : {}),
        ...(value.verticalAlignment === 'baseline' ||
        value.verticalAlignment === 'subscript' ||
        value.verticalAlignment === 'superscript'
          ? { verticalAlignment: value.verticalAlignment }
          : {})
      };
    case 'hyperlink':
      return { type: 'hyperlink', text: stringValue(value.text), uri: stringValue(value.uri) };
    case 'citation':
      return {
        type: 'citation',
        targetId: stringValue(value.targetId),
        displayText: stringValue(value.displayText)
      };
    case 'bookmark':
      return {
        type: 'bookmark',
        name: stringValue(value.name),
        inlines: normalizeInlines(value.inlines, `${path}.inlines`, issues)
      };
    case 'reference':
      return {
        type: 'reference',
        bookmarkName: stringValue(value.bookmarkName),
        ...(typeof value.fallbackText === 'string' ? { fallbackText: value.fallbackText } : {})
      };
    case 'footnote':
      return noteInline('footnote', value, path, issues);
    case 'endnote':
      return noteInline('endnote', value, path, issues);
    default:
      issues.push(
        issue('inline.type.unsupported', `不支持的 inline.type=${value.type}，导入时会忽略。`, 'warning', `${path}.type`)
      );
      return null;
  }
}

function noteInline(
  type: FootnoteInline['type'] | EndnoteInline['type'],
  value: Record<string, unknown>,
  path: string,
  issues: ApiIssue[]
): FootnoteInline | EndnoteInline {
  const inlines = normalizeInlines(value.inlines, `${path}.inlines`, issues);
  if (type === 'footnote') {
    return { type, noteId: stringValue(value.noteId), inlines };
  }
  return { type, noteId: stringValue(value.noteId), inlines };
}

function cleanSection(section: Section): Section {
  return stripNulls({
    ...(section.id ? { id: section.id } : {}),
    kind: section.kind,
    ...(section.title ? { title: section.title } : {}),
    ...(typeof section.startOnNewPage === 'boolean'
      ? { startOnNewPage: section.startOnNewPage }
      : {}),
    blocks: section.blocks.map(cleanBlock)
  });
}

function cleanBlock(block: Block): Block {
  switch (block.type) {
    case 'paragraph':
      return stripNulls({
        type: block.type,
        ...(block.id ? { id: block.id } : {}),
        inlines: block.inlines.map(cleanInline),
        ...(block.styleId ? { styleId: block.styleId } : {}),
        ...(block.alignment ? { alignment: block.alignment } : {})
      });
    case 'heading':
      return stripNulls({
        type: block.type,
        ...(block.id ? { id: block.id } : {}),
        level: block.level,
        inlines: block.inlines.map(cleanInline),
        ...(block.bookmarkName ? { bookmarkName: block.bookmarkName } : {}),
        ...(typeof block.numbered === 'boolean' ? { numbered: block.numbered } : {})
      });
    case 'list':
      return stripNulls({
        type: block.type,
        ...(block.id ? { id: block.id } : {}),
        ...(typeof block.ordered === 'boolean' ? { ordered: block.ordered } : {}),
        items: block.items.map((item) => ({ blocks: item.blocks.map(cleanBlock) }))
      });
    case 'figure':
      return stripNulls({
        type: block.type,
        ...(block.id ? { id: block.id } : {}),
        caption: block.caption,
        ...(block.imagePath ? { imagePath: block.imagePath } : {}),
        ...(block.imageDataBase64 ? { imageDataBase64: block.imageDataBase64 } : {}),
        imageContentType: block.imageContentType,
        ...(typeof block.widthCm === 'number' ? { widthCm: block.widthCm } : {}),
        ...(typeof block.heightCm === 'number' ? { heightCm: block.heightCm } : {})
      });
    case 'table':
      return stripNulls({
        type: block.type,
        ...(block.id ? { id: block.id } : {}),
        ...(block.bookmarkId ? { bookmarkId: block.bookmarkId } : {}),
        caption: block.caption,
        ...(block.captionPosition ? { captionPosition: block.captionPosition } : {}),
        ...(block.style ? { style: block.style } : {}),
        ...(block.width ? { width: block.width } : {}),
        ...(block.alignment ? { alignment: block.alignment } : {}),
        ...(block.layout ? { layout: block.layout } : {}),
        ...(typeof block.allowRowBreakAcrossPages === 'boolean'
          ? { allowRowBreakAcrossPages: block.allowRowBreakAcrossPages }
          : {}),
        ...(typeof block.repeatHeaderRows === 'number' ? { repeatHeaderRows: block.repeatHeaderRows } : {}),
        ...(block.borders ? { borders: block.borders } : {}),
        ...(block.cellMargins ? { cellMargins: block.cellMargins } : {}),
        rows: block.rows.map(cleanTableRow)
      });
    case 'quote':
      return stripNulls({
        type: block.type,
        ...(block.id ? { id: block.id } : {}),
        inlines: block.inlines.map(cleanInline)
      });
    case 'equation':
      return stripNulls({
        type: block.type,
        ...(block.id ? { id: block.id } : {}),
        ...(block.bookmarkId ? { bookmarkId: block.bookmarkId } : {}),
        ...(block.bookmarkName ? { bookmarkName: block.bookmarkName } : {}),
        ...(block.placeholder ? { placeholder: block.placeholder } : {}),
        ...(block.sourceType ? { sourceType: block.sourceType } : {}),
        ...(block.omml ? { omml: block.omml } : {}),
        ...(block.latex ? { latex: block.latex } : {}),
        ...(block.plainText ? { plainText: block.plainText } : {}),
        ...(typeof block.display === 'boolean' ? { display: block.display } : {}),
        ...(block.alignment ? { alignment: block.alignment } : {}),
        ...(block.caption ? { caption: block.caption } : {}),
        ...(block.numbering ? { numbering: block.numbering } : {}),
        ...(typeof block.allowWordUpdate === 'boolean' ? { allowWordUpdate: block.allowWordUpdate } : {})
      });
    case 'pageBreak':
    case 'sectionBreak':
      return stripNulls({ type: block.type, ...(block.id ? { id: block.id } : {}) });
    case 'bibliography':
      return stripNulls({
        type: block.type,
        ...(block.id ? { id: block.id } : {}),
        entries: block.entries.map((entry) => ({ id: entry.id, text: entry.text }))
      });
    case 'footnote':
    case 'endnote':
      return stripNulls({
        type: block.type,
        ...(block.id ? { id: block.id } : {}),
        noteId: block.noteId,
        inlines: block.inlines.map(cleanInline)
      });
  }
}

function cleanTableRow(row: TableRow): TableRow {
  return stripNulls({
    ...(row.id ? { id: row.id } : {}),
    ...(typeof row.isHeader === 'boolean' ? { isHeader: row.isHeader } : {}),
    ...(typeof row.cantSplit === 'boolean' ? { cantSplit: row.cantSplit } : {}),
    ...(typeof row.heightPt === 'number' ? { heightPt: row.heightPt } : {}),
    cells: row.cells.map(cleanTableCell)
  });
}

function cleanTableCell(cell: TableCell): TableCell {
  return stripNulls({
    ...(cell.id ? { id: cell.id } : {}),
    ...(typeof cell.text === 'string' ? { text: cell.text } : {}),
    ...(cell.blocks ? { blocks: cell.blocks.map(cleanBlock) } : {}),
    ...(typeof cell.gridSpan === 'number' ? { gridSpan: cell.gridSpan } : {}),
    ...(cell.verticalMerge ? { verticalMerge: cell.verticalMerge } : {}),
    ...(cell.width ? { width: cell.width } : {}),
    ...(typeof cell.widthCm === 'number' ? { widthCm: cell.widthCm } : {}),
    ...(cell.alignment ? { alignment: cell.alignment } : {}),
    ...(cell.verticalAlignment ? { verticalAlignment: cell.verticalAlignment } : {}),
    ...(cell.shading ? { shading: cell.shading } : {}),
    ...(cell.borders ? { borders: cell.borders } : {}),
    ...(cell.cellMargins ? { cellMargins: cell.cellMargins } : {}),
    ...(cell.font ? { font: cell.font } : {}),
    ...(cell.paragraph ? { paragraph: cell.paragraph } : {})
  });
}

function cleanInline(inline: Inline): Inline {
  switch (inline.type) {
    case 'text':
      return stripNulls({
        type: inline.type,
        text: inline.text,
        ...(inline.bold ? { bold: inline.bold } : {}),
        ...(inline.italic ? { italic: inline.italic } : {}),
        ...(inline.underline ? { underline: inline.underline } : {}),
        ...(inline.verticalAlignment ? { verticalAlignment: inline.verticalAlignment } : {})
      });
    case 'hyperlink':
      return stripNulls({ type: inline.type, text: inline.text, uri: inline.uri });
    case 'citation':
      return stripNulls({ type: inline.type, targetId: inline.targetId, displayText: inline.displayText });
    case 'bookmark':
      return stripNulls({ type: inline.type, name: inline.name, inlines: inline.inlines.map(cleanInline) });
    case 'reference':
      return stripNulls({
        type: inline.type,
        bookmarkName: inline.bookmarkName,
        ...(inline.fallbackText ? { fallbackText: inline.fallbackText } : {})
      });
    case 'footnote':
    case 'endnote':
      return stripNulls({ type: inline.type, noteId: inline.noteId, inlines: inline.inlines.map(cleanInline) });
  }
}

function validateMetadata(document: ThesisDocument, issues: ApiIssue[]) {
  const required = ['title', 'author', 'college', 'major', 'studentId', 'advisor', 'date', 'language'] as const;
  for (const key of required) {
    if (!document.metadata[key]?.trim()) {
      issues.push(issue('metadata.required', `${key} 是必填字段。`, 'error', `$.metadata.${key}`));
    }
  }
  if (document.metadata.language && !/^[a-z]{2}(-[A-Z]{2})?$/.test(document.metadata.language)) {
    issues.push(
      issue('metadata.language.invalid', 'language 应形如 zh-CN 或 en。', 'error', '$.metadata.language')
    );
  }
}

function validateSections(document: ThesisDocument, issues: ApiIssue[]) {
  if (!document.sections.length) {
    issues.push(issue('sections.empty', '至少需要一个 section。', 'error', '$.sections'));
  }
  document.sections.forEach((section, sectionIndex) => {
    if (!SECTION_KINDS.has(section.kind)) {
      issues.push(issue('section.kind.invalid', 'section.kind 不受支持。', 'error', `$.sections[${sectionIndex}].kind`));
    }
    section.blocks.forEach((block, blockIndex) => {
      validateBlock(block, `$.sections[${sectionIndex}].blocks[${blockIndex}]`, issues);
    });
  });
}

function validateBlock(block: Block, path: string, issues: ApiIssue[]) {
  if ((block.type === 'paragraph' || block.type === 'heading' || block.type === 'quote') && block.inlines.length === 0) {
    issues.push(issue('block.emptyText', '文本块为空。', 'warning', `${path}.inlines`));
  }
  if (block.type === 'figure') {
    if (!block.caption.trim()) issues.push(issue('figure.caption.empty', '图片题注为空。', 'warning', `${path}.caption`));
    if (!block.imagePath && !block.imageDataBase64) {
      issues.push(issue('figure.image.missing', '图片缺少 imagePath 或 imageDataBase64。', 'warning', path));
    }
  }
  if (block.type === 'equation') {
    const hasSource = Boolean(block.omml || block.latex || block.plainText || block.placeholder);
    if (!hasSource) issues.push(issue('equation.source.empty', '公式内容为空。', 'warning', path));
  }
  if (block.type === 'bibliography' && block.entries.length === 0) {
    issues.push(issue('bibliography.empty', '参考文献块没有条目。', 'warning', `${path}.entries`));
  }
  if (block.type === 'table') validateTableBlock(block, path, issues);
  if ((block.type === 'footnote' || block.type === 'endnote') && block.inlines.length === 0) {
    issues.push(issue('note.empty', '注释内容为空。', 'error', `${path}.inlines`));
  }
}

function validateTableBlock(block: Extract<Block, { type: 'table' }>, path: string, issues: ApiIssue[]) {
  if (!block.caption.trim()) issues.push(issue('table.caption.empty', '表格题注为空。', 'warning', `${path}.caption`));
  let expected = -1;
  const active = new Set<number>();
  block.rows.forEach((row, rowIndex) => {
    let col = 0;
    const next = new Set<number>();
    row.cells.forEach((cell, cellIndex) => {
      const span = cell.gridSpan ?? 1;
      if (span < 1) {
        issues.push(issue('table.gridSpan.invalid', 'gridSpan 必须大于等于 1。', 'error', `${path}.rows[${rowIndex}].cells[${cellIndex}].gridSpan`));
      }
      for (let i = col; i < col + Math.max(1, span); i++) {
        if (cell.verticalMerge === 'continue' && !active.has(i)) {
          issues.push(issue('table.verticalMerge.invalidChain', 'vMerge continue 上方必须有 restart 或 continue。', 'error', `${path}.rows[${rowIndex}].cells[${cellIndex}].verticalMerge`));
        }
        if (cell.verticalMerge === 'restart' || cell.verticalMerge === 'continue') next.add(i);
      }
      col += Math.max(1, span);
    });
    if (expected < 0) expected = col;
    else if (col !== expected) {
      issues.push(issue('table.grid.inconsistent', `第 ${rowIndex + 1} 行逻辑列数 ${col} 与首行 ${expected} 不一致。`, 'error', `${path}.rows[${rowIndex}]`));
    }
    active.clear();
    next.forEach((v) => active.add(v));
  });
}

function validateBibliographyCitations(document: ThesisDocument, issues: ApiIssue[]) {
  const refs = new Set<string>();
  walkBlocks(document, (block) => {
    if (block.type === 'bibliography') {
      for (const entry of block.entries) {
        if (!entry.id.trim()) issues.push(issue('bibliography.id.empty', '参考文献 id 为空。', 'error'));
        if (!entry.text.trim()) issues.push(issue('bibliography.entry.empty', `参考文献 ${entry.id || '(空 id)'} 内容为空。`, 'warning'));
        refs.add(entry.id);
      }
    }
  });
  walkInlines(document, (inline, path) => {
    if (inline.type === 'citation' && !refs.has(inline.targetId)) {
      issues.push(issue('citation.target.missing', `引用目标 ${inline.targetId} 不存在于参考文献库。`, 'warning', path));
    }
  });
}

function validateReferences(document: ThesisDocument, issues: ApiIssue[]) {
  const bookmarks = new Set<string>();
  walkBlocks(document, (block) => {
    if ((block.type === 'heading' || block.type === 'equation') && block.bookmarkName) bookmarks.add(block.bookmarkName);
    if ((block.type === 'figure' || block.type === 'table' || block.type === 'equation') && block.id) bookmarks.add(block.id);
    if ((block.type === 'table' || block.type === 'equation') && block.bookmarkId) bookmarks.add(block.bookmarkId);
  });
  walkInlines(document, (inline, path) => {
    if (inline.type === 'bookmark') bookmarks.add(inline.name);
    if (inline.type === 'reference' && !bookmarks.has(inline.bookmarkName)) {
      issues.push(issue('reference.target.missing', `交叉引用目标 ${inline.bookmarkName} 不存在。`, 'warning', path));
    }
  });
}

function validateNotes(document: ThesisDocument, issues: ApiIssue[]) {
  const seenFootnotes = new Map<string, string>();
  const seenEndnotes = new Map<string, string>();
  walkBlocks(document, (block, path) => {
    if (block.type === 'footnote') addNote(block.noteId, path, seenFootnotes, 'duplicate.footnoteId', issues);
    if (block.type === 'endnote') addNote(block.noteId, path, seenEndnotes, 'duplicate.endnoteId', issues);
  });
  walkInlines(document, (inline, path) => {
    if (inline.type === 'footnote') {
      addNote(inline.noteId, path, seenFootnotes, 'duplicate.footnoteId', issues);
      if (!inlinesPlainText(inline.inlines).trim()) issues.push(issue('note.empty', '脚注内容为空。', 'error', path));
    }
    if (inline.type === 'endnote') {
      addNote(inline.noteId, path, seenEndnotes, 'duplicate.endnoteId', issues);
      if (!inlinesPlainText(inline.inlines).trim()) issues.push(issue('note.empty', '尾注内容为空。', 'error', path));
    }
  });
}

function addNote(noteId: string, path: string | undefined, seen: Map<string, string>, code: string, issues: ApiIssue[]) {
  if (!noteId.trim()) {
    issues.push(issue('note.id.empty', 'noteId 不能为空。', 'error', path));
    return;
  }
  const previous = seen.get(noteId);
  if (previous) {
    issues.push(issue(code, `noteId=${noteId} 重复；首次出现于 ${previous}。`, 'error', path));
  } else {
    seen.set(noteId, path ?? '$');
  }
}

function walkBlocks(document: ThesisDocument, visit: (block: Block, path?: string) => void) {
  document.sections.forEach((section, sectionIndex) => {
    section.blocks.forEach((block, blockIndex) => {
      const path = `$.sections[${sectionIndex}].blocks[${blockIndex}]`;
      visit(block, path);
      if (block.type === 'list') {
        block.items.forEach((item, itemIndex) => {
          item.blocks.forEach((child, childIndex) =>
            visit(child, `${path}.items[${itemIndex}].blocks[${childIndex}]`)
          );
        });
      }
      if (block.type === 'table') {
        block.rows.forEach((row, rowIndex) => {
          row.cells.forEach((cell, cellIndex) => {
            cell.blocks?.forEach((child, childIndex) =>
              visit(child, `${path}.rows[${rowIndex}].cells[${cellIndex}].blocks[${childIndex}]`)
            );
          });
        });
      }
    });
  });
}

function walkInlines(document: ThesisDocument, visit: (inline: Inline, path: string) => void) {
  walkBlocks(document, (block, blockPath) => {
    if (!blockPath) return;
    if (block.type === 'paragraph' || block.type === 'heading' || block.type === 'quote' || block.type === 'footnote' || block.type === 'endnote') {
      block.inlines.forEach((inline, index) => {
        visit(inline, `${blockPath}.inlines[${index}]`);
        walkNestedInline(inline, `${blockPath}.inlines[${index}]`, visit);
      });
    }
  });
}

function walkNestedInline(inline: Inline, path: string, visit: (inline: Inline, path: string) => void) {
  if (inline.type === 'bookmark' || inline.type === 'footnote' || inline.type === 'endnote') {
    inline.inlines.forEach((child, index) => {
      const childPath = `${path}.inlines[${index}]`;
      visit(child, childPath);
      walkNestedInline(child, childPath, visit);
    });
  }
}

function inlinesPlainText(inlines: Inline[]): string {
  return inlines
    .map((inline) => {
      if (inline.type === 'text' || inline.type === 'hyperlink') return inline.text;
      if (inline.type === 'citation') return inline.displayText;
      if (inline.type === 'reference') return inline.fallbackText ?? inline.bookmarkName;
      if (inline.type === 'bookmark' || inline.type === 'footnote' || inline.type === 'endnote') {
        return inlinesPlainText(inline.inlines);
      }
      return '';
    })
    .join('');
}

function stripNulls<T>(value: T): T {
  if (Array.isArray(value)) return value.map((v) => stripNulls(v)) as T;
  if (value && typeof value === 'object') {
    const out: Record<string, unknown> = {};
    for (const [key, item] of Object.entries(value as Record<string, unknown>)) {
      if (item === null || item === undefined) continue;
      out[key] = stripNulls(item);
    }
    return out as T;
  }
  return value;
}

function issue(
  code: string,
  message: string,
  severity: ApiIssue['severity'] = 'error',
  path?: string | null,
  suggestedAction?: string | null
): ApiIssue {
  return { code, message, severity, path: path ?? null, suggestedAction: suggestedAction ?? null };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function stringValue(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function normalizeHeadingLevel(value: unknown): 1 | 2 | 3 | 4 | 5 | 6 {
  return value === 1 || value === 2 || value === 3 || value === 4 || value === 5 || value === 6
    ? value
    : 1;
}

function isAlignment(value: unknown): value is 'left' | 'center' | 'right' | 'both' {
  return value === 'left' || value === 'center' || value === 'right' || value === 'both';
}

function isImageContentType(value: unknown): value is ThesisDocument['sections'][number]['blocks'][number] extends infer B
  ? B extends { type: 'figure'; imageContentType: infer T }
    ? T
    : never
  : never {
  return (
    value === 'image/png' ||
    value === 'image/jpeg' ||
    value === 'image/jpg' ||
    value === 'image/gif' ||
    value === 'image/bmp' ||
    value === 'image/tiff'
  );
}

function safeFilePart(value: string): string {
  return value
    .trim()
    .replace(/[\\/:*?"<>|]+/g, '-')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')
    .slice(0, 120)
    .replace(/^-|-$/g, '');
}
