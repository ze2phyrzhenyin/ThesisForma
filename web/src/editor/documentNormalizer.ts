import type {
  ApiIssue,
  Block,
  EndnoteInline,
  FootnoteInline,
  Inline,
  Section,
  SectionKind,
  TableCell,
  TableRow
} from '@/types';
import {
  isAlignment,
  isImageContentType,
  isRecord,
  normalizeHeadingLevel,
  SECTION_KINDS,
  stringValue,
  issue
} from './documentContractUtils';

export function normalizeMetadata(metadata: Record<string, unknown>, issues: ApiIssue[]) {
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

export function normalizeSections(values: unknown[], issues: ApiIssue[]): Section[] {
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

