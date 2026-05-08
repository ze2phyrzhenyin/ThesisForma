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
  it('rejects missing schemaVersion and metadata', () => {
    const result = parseThesisDocumentJson(JSON.stringify({ sections: [] }));
    expect(result.ok).toBe(false);
    expect(result.issues.map((issue) => issue.code)).toContain('document.schemaVersion.required');
    expect(result.issues.map((issue) => issue.code)).toContain('document.metadata.required');
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
  });
});
