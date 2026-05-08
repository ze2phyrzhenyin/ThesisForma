/// <reference types="node" />

import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import { cleanThesisDocument, parseThesisDocumentJson } from '@/editor/documentContract';
import { cleanTemplatePackage, parseTemplatePackageJson } from '@/templates/templateContract';

const repoRoot = path.resolve(process.cwd(), '..');

function readExample(...segments: string[]): string {
  return fs.readFileSync(path.join(repoRoot, ...segments), 'utf8');
}

describe('repository JSON fixtures through web contracts', () => {
  it('round-trips simple and full ThesisDocument examples', () => {
    for (const segments of [
      ['examples', 'simple-thesis', 'document.json'],
      ['examples', 'full-thesis', 'document.json']
    ]) {
      const parsed = parseThesisDocumentJson(readExample(...segments));
      expect(parsed.ok, segments.join('/')).toBe(true);

      const cleaned = cleanThesisDocument(parsed.document!);
      expect(() => JSON.parse(JSON.stringify(cleaned))).not.toThrow();
      expect(cleaned.metadata.title).toBeTruthy();
      expect(cleaned.sections.length).toBeGreaterThan(0);
    }
  });

  it('preserves advanced table, figure, equation, reference, and note fields from full thesis', () => {
    const parsed = parseThesisDocumentJson(readExample('examples', 'full-thesis', 'document.json'));
    const cleaned = cleanThesisDocument(parsed.document!);
    const blocks = cleaned.sections.flatMap((section) => section.blocks);
    const table = blocks.find((block) => block.type === 'table');
    const paragraphWithNotes = blocks.find(
      (block) =>
        block.type === 'paragraph' &&
        block.inlines.some((inline) => inline.type === 'footnote' || inline.type === 'endnote')
    );

    expect(parsed.ok).toBe(true);
    expect(blocks.some((block) => block.type === 'figure' && block.caption && block.imageDataBase64)).toBe(true);
    expect(blocks.some((block) => block.type === 'equation' && block.numbering?.format === '({chapter}.{index})')).toBe(true);
    expect(blocks.some((block) => block.type === 'paragraph' && block.inlines.some((inline) => inline.type === 'reference'))).toBe(true);
    expect(table).toMatchObject({ type: 'table', bookmarkId: 'bm-table-format-rules' });
    if (table?.type !== 'table') throw new Error('Expected table fixture.');
    expect(table.rows[0].cells[0]).toMatchObject({ width: { type: 'dxa', value: 2400 } });
    expect(table.rows[0].cells[1]).toMatchObject({ gridSpan: 2 });
    expect(table.rows[1].cells[2]).toMatchObject({ borders: { bottom: { style: 'double' } } });
    expect(paragraphWithNotes).toBeTruthy();
    expect(JSON.stringify(cleaned)).not.toContain('ui');
  });

  it('round-trips template package examples with variables, assets, and page template blocks', () => {
    for (const segments of [
      ['examples', 'templates', 'basic-cn-thesis', 'template.json'],
      ['examples', 'templates', 'example-university-engineering', 'template.json']
    ]) {
      const parsed = parseTemplatePackageJson(readExample(...segments));
      expect(parsed.ok, segments.join('/')).toBe(true);

      const cleaned = cleanTemplatePackage(parsed.template!);
      expect(() => JSON.parse(JSON.stringify(cleaned))).not.toThrow();
      expect(cleaned.templateSchemaVersion).toBe('1.0.0');
      expect(cleaned.id).toBeTruthy();
    }

    const engineering = cleanTemplatePackage(
      parseTemplatePackageJson(readExample('examples', 'templates', 'example-university-engineering', 'template.json')).template!
    );
    const blockTypes = engineering.pageTemplates?.flatMap((template) => template.blocks.map((block) => block.type)) ?? [];
    expect(engineering.variables?.some((variable) => variable.name === 'defenseDate')).toBe(true);
    expect(engineering.assets?.some((asset) => asset.id === 'collegeLogo')).toBe(true);
    expect(blockTypes).toEqual(
      expect.arrayContaining(['spacer', 'text', 'metadataField', 'image', 'fieldTable', 'declarationText', 'pageBreak'])
    );
    expect(JSON.stringify(engineering)).not.toContain('uiExpanded');
  });
});
