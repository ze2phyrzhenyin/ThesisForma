import type { Block, Inline, Section, TableCell, TableRow, ThesisDocument } from '@/types';
import { stripNulls } from './documentContractUtils';

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

