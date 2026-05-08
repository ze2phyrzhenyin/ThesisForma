import { describe, it, expect } from 'vitest';
import { createEditorStore, blockFactory } from '@/editor/store';
import type { DocumentEnvelope, ThesisDocument } from '@/types';

const baseDoc: ThesisDocument = {
  schemaVersion: '1.1.0',
  metadata: {
    title: '测试',
    author: 'a',
    college: 'c',
    major: 'm',
    studentId: 's',
    advisor: 'd',
    date: '2026-05',
    language: 'zh-CN'
  },
  sections: [
    { id: 'cover', kind: 'cover', blocks: [] },
    {
      id: 'body',
      kind: 'body',
      blocks: [
        { type: 'heading', id: 'h1', level: 1, inlines: [{ type: 'text', text: '绪论' }] },
        { type: 'paragraph', id: 'p1', inlines: [{ type: 'text', text: 'hi' }] }
      ]
    }
  ]
};

const baseEnvelope: DocumentEnvelope = {
  id: 'doc-test',
  templateId: null,
  document: baseDoc,
  updatedAt: 'now'
};

describe('editor store', () => {
  it('opens body section by default', () => {
    const store = createEditorStore(baseEnvelope);
    expect(store.getState().view).toEqual({ kind: 'section', sectionIndex: 1 });
  });

  it('inserts a paragraph and selects it', () => {
    const store = createEditorStore(baseEnvelope);
    store.getState().insertBlock(1, 1, blockFactory.paragraph('插入'));
    const state = store.getState();
    expect(state.envelope.document.sections[1].blocks).toHaveLength(3);
    expect(state.envelope.document.sections[1].blocks[1].type).toBe('paragraph');
    expect(state.dirty).toBe(true);
    expect(state.selectedBlock).toEqual({ sectionIndex: 1, blockIndex: 1 });
  });

  it('moves a block', () => {
    const store = createEditorStore(baseEnvelope);
    store.getState().moveBlock(1, 0, 1);
    expect(store.getState().envelope.document.sections[1].blocks[0].type).toBe('paragraph');
    expect(store.getState().envelope.document.sections[1].blocks[1].type).toBe('heading');
  });

  it('deletes the last block and clears selection if it was deleted', () => {
    const store = createEditorStore(baseEnvelope);
    store.getState().selectBlock(1, 1);
    store.getState().deleteBlock(1, 1);
    expect(store.getState().envelope.document.sections[1].blocks).toHaveLength(1);
    expect(store.getState().selectedBlock).toBeNull();
  });

  it('ensureSection inserts a missing section in canonical order', () => {
    const store = createEditorStore(baseEnvelope);
    const idx = store.getState().ensureSection('abstract');
    const sections = store.getState().envelope.document.sections;
    expect(sections[idx].kind).toBe('abstract');
    // body must still be after abstract
    const abstractIdx = sections.findIndex((s) => s.kind === 'abstract');
    const bodyIdx = sections.findIndex((s) => s.kind === 'body');
    expect(abstractIdx).toBeLessThan(bodyIdx);
  });
});
