import type { Block, Inline, ThesisDocument } from '@/types';

export function walkBlocks(document: ThesisDocument, visit: (block: Block, path?: string) => void) {
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

export function walkInlines(document: ThesisDocument, visit: (inline: Inline, path: string) => void) {
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

export function inlinesPlainText(inlines: Inline[]): string {
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

