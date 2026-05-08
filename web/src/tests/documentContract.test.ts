import { describe, expect, it } from 'vitest';
import {
  cleanThesisDocument,
  exportFileNameForDocument,
  parseThesisDocumentJson,
  validateThesisDocument
} from '@/editor/documentContract';
import type { ThesisDocument } from '@/types';

const validDoc: ThesisDocument = {
  schemaVersion: '1.1.0',
  metadata: {
    title: '测试论文',
    author: '张三',
    college: '信息学院',
    major: '软件工程',
    studentId: '20260001',
    advisor: '李四',
    date: '2026-05-08',
    language: 'zh-CN'
  },
  sections: [
    {
      kind: 'body',
      blocks: [{ type: 'paragraph', inlines: [{ type: 'text', text: '正文' }] }]
    }
  ]
};

describe('ThesisDocument contract helpers', () => {
  it('imports a legal minimal ThesisDocument', () => {
    const result = parseThesisDocumentJson(JSON.stringify(validDoc));
    expect(result.ok).toBe(true);
    expect(result.document?.metadata.title).toBe('测试论文');
  });

  it('rejects missing schemaVersion and metadata', () => {
    const result = parseThesisDocumentJson(JSON.stringify({ sections: [] }));
    expect(result.ok).toBe(false);
    expect(result.issues.map((issue) => issue.code)).toContain('document.schemaVersion.required');
    expect(result.issues.map((issue) => issue.code)).toContain('document.metadata.required');
  });

  it('rejects missing sections', () => {
    const result = parseThesisDocumentJson(JSON.stringify({ schemaVersion: '1.1.0', metadata: validDoc.metadata }));
    expect(result.ok).toBe(false);
    expect(result.issues.map((issue) => issue.code)).toContain('document.sections.required');
  });

  it('reports malformed and non-object JSON clearly', () => {
    expect(parseThesisDocumentJson('{bad').issues[0].code).toBe('json.parse');
    for (const value of ['[]', 'null', '"text"']) {
      const result = parseThesisDocumentJson(value);
      expect(result.ok).toBe(false);
      expect(result.issues[0].code).toBe('document.type');
    }
  });

  it('imports unknown local properties as warnings without crashing', () => {
    const result = parseThesisDocumentJson(
      JSON.stringify({
        ...validDoc,
        metadata: { ...validDoc.metadata, uiOnly: true }
      })
    );
    expect(result.ok).toBe(true);
    expect(result.issues.some((issue) => issue.code === 'document.unknownProperty')).toBe(true);
    expect(result.document?.metadata).not.toHaveProperty('uiOnly');
  });

  it('cleans null values from exported document', () => {
    const dirty = {
      ...validDoc,
      metadata: { ...validDoc.metadata, subtitle: undefined },
      sections: [{ ...validDoc.sections[0], startOnNewPage: undefined }]
    };
    expect(JSON.stringify(cleanThesisDocument(dirty))).not.toContain('undefined');
  });

  it('cleans UI-only fields while preserving schema-backed table fields', () => {
    const dirty = {
      ...validDoc,
      sections: [
        {
          kind: 'body',
          blocks: [
            {
              type: 'table',
              caption: '表 1',
              uiSelection: { row: 0, col: 0 },
              width: { type: 'percent', value: 100 },
              borders: { bottom: { style: 'single', size: 4 } },
              rows: [
                {
                  cells: [
                    {
                      text: 'A',
                      gridSpan: 2,
                      verticalAlignment: 'center',
                      shading: 'EEEEEE',
                      cellMargins: { leftCm: 0.1 },
                      font: { eastAsia: '黑体', latin: 'Times New Roman', sizePt: 10.5, bold: true },
                      paragraph: { alignment: 'center', lineSpacingMultiple: 1.2, widowControl: true }
                    }
                  ]
                }
              ]
            }
          ]
        }
      ]
    } as unknown as ThesisDocument;

    const cleaned = cleanThesisDocument(dirty);
    const table = cleaned.sections[0].blocks[0];

    expect('uiSelection' in table).toBe(false);
    expect(table).toMatchObject({
      type: 'table',
      width: { type: 'percent', value: 100 },
      borders: { bottom: { style: 'single', size: 4 } },
      rows: [
        {
          cells: [
            {
              gridSpan: 2,
              verticalAlignment: 'center',
              shading: 'EEEEEE',
              cellMargins: { leftCm: 0.1 },
              font: { eastAsia: '黑体', latin: 'Times New Roman', sizePt: 10.5, bold: true },
              paragraph: { alignment: 'center', lineSpacingMultiple: 1.2, widowControl: true }
            }
          ]
        }
      ]
    });
  });

  it('preserves legal table fields during import normalization', () => {
    const result = parseThesisDocumentJson(
      JSON.stringify({
        ...validDoc,
        sections: [
          {
            kind: 'body',
            blocks: [
              {
                type: 'table',
                caption: '表 1',
                width: { type: 'percent', value: 100 },
                rows: [
                  {
                    cells: [
                      {
                        text: 'A',
                        widthCm: 3,
                        alignment: 'center',
                        verticalAlignment: 'bottom',
                        borders: { top: { style: 'single' } },
                        font: { sizePt: 11, italic: true },
                        paragraph: { spaceBeforePt: 2, alignment: 'both' }
                      }
                    ]
                  }
                ]
              }
            ]
          }
        ]
      })
    );

    const table = result.document?.sections[0].blocks[0];
    expect(result.ok).toBe(true);
    expect(table).toMatchObject({
      type: 'table',
      width: { type: 'percent', value: 100 },
      rows: [
        {
          cells: [
            {
              widthCm: 3,
              alignment: 'center',
              verticalAlignment: 'bottom',
              borders: { top: { style: 'single' } },
              font: { sizePt: 11, italic: true },
              paragraph: { spaceBeforePt: 2, alignment: 'both' }
            }
          ]
        }
      ]
    });
  });

  it('validates duplicate note ids as errors', () => {
    const doc: ThesisDocument = {
      ...validDoc,
      sections: [
        {
          kind: 'body',
          blocks: [
            {
              type: 'paragraph',
              inlines: [
                { type: 'footnote', noteId: 'fn-1', inlines: [{ type: 'text', text: 'A' }] },
                { type: 'footnote', noteId: 'fn-1', inlines: [{ type: 'text', text: 'B' }] }
              ]
            }
          ]
        }
      ]
    };
    expect(validateThesisDocument(doc).some((issue) => issue.code === 'duplicate.footnoteId')).toBe(true);
  });

  it('builds safe export file names', () => {
    expect(exportFileNameForDocument(validDoc)).toMatch(/^测试论文-张三-2026-05-08\.json$/);
    expect(
      exportFileNameForDocument({
        ...validDoc,
        metadata: { ...validDoc.metadata, title: '标题 / A B:*?', author: '王 五' }
      })
    ).toBe('标题-A-B-王-五-2026-05-08.json');
  });
});
