import { describe, expect, it } from 'vitest';
import { cleanThesisDocument, parseThesisDocumentJson, validateThesisDocument } from '@/editor/documentContract';
import { collectNotes, deleteNote, insertNoteAtBlockEnd, updateNoteText } from '@/editor/notes';
import type { ThesisDocument } from '@/types';

function documentFixture(): ThesisDocument {
  return {
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
}

describe('notes helpers', () => {
  it('round-trips an inserted footnote reference through export and import', () => {
    const doc = documentFixture();
    const inserted = insertNoteAtBlockEnd(doc, 0, 0, 'footnote', '脚注内容');

    const parsed = parseThesisDocumentJson(JSON.stringify(cleanThesisDocument(doc)));
    const notes = collectNotes(parsed.document!);

    expect(parsed.ok).toBe(true);
    expect(notes).toHaveLength(1);
    expect(notes[0]).toMatchObject({
      kind: 'footnote',
      noteId: inserted?.noteId,
      text: '脚注内容'
    });
  });

  it('round-trips an inserted endnote reference through export and import', () => {
    const doc = documentFixture();
    const inserted = insertNoteAtBlockEnd(doc, 0, 0, 'endnote', '尾注内容');

    const parsed = parseThesisDocumentJson(JSON.stringify(cleanThesisDocument(doc)));
    const notes = collectNotes(parsed.document!);

    expect(parsed.ok).toBe(true);
    expect(notes[0]).toMatchObject({
      kind: 'endnote',
      noteId: inserted?.noteId,
      text: '尾注内容'
    });
  });

  it('updates note panel text into exported JSON', () => {
    const doc = documentFixture();
    const inserted = insertNoteAtBlockEnd(doc, 0, 0, 'footnote', '旧内容')!;
    updateNoteText(doc, inserted, '新内容');

    const exported = cleanThesisDocument(doc);
    const notes = collectNotes(exported);

    expect(notes[0]).toMatchObject({ noteId: inserted.noteId, text: '新内容' });
  });

  it('deletes inline note references explicitly', () => {
    const doc = documentFixture();
    const inserted = insertNoteAtBlockEnd(doc, 0, 0, 'footnote', '脚注内容')!;
    deleteNote(doc, inserted);

    expect(collectNotes(doc)).toHaveLength(0);
    expect(JSON.stringify(cleanThesisDocument(doc))).not.toContain(inserted.noteId);
  });

  it('reports duplicate noteId errors and empty note warnings', () => {
    const doc = documentFixture();
    doc.sections[0].blocks[0] = {
      type: 'paragraph',
      inlines: [
        { type: 'footnote', noteId: 'fn-1', inlines: [{ type: 'text', text: 'A' }] },
        { type: 'footnote', noteId: 'fn-1', inlines: [] }
      ]
    };

    const issues = validateThesisDocument(doc);

    expect(issues.some((issue) => issue.code === 'duplicate.footnoteId')).toBe(true);
    expect(issues.some((issue) => issue.code === 'note.empty')).toBe(true);
    expect(collectNotes(doc).every((note) => note.referenceCount === 2)).toBe(true);
  });
});
