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

  it('addSection inserts at the requested position and shifts the active view', () => {
    const store = createEditorStore(baseEnvelope);
    // initial view points at body (index 1)
    expect(store.getState().view).toEqual({ kind: 'section', sectionIndex: 1 });
    const idx = store.getState().addSection('toc', 1);
    expect(idx).toBe(1);
    const sections = store.getState().envelope.document.sections;
    expect(sections[1].kind).toBe('toc');
    // body shifted to index 2; view must follow
    expect(sections[2].kind).toBe('body');
    expect(store.getState().view).toEqual({ kind: 'section', sectionIndex: 2 });
  });

  it('addSection allows duplicates and assigns a unique id', () => {
    const store = createEditorStore(baseEnvelope);
    const idxA = store.getState().addSection('appendix');
    const idxB = store.getState().addSection('appendix');
    const sections = store.getState().envelope.document.sections;
    expect(sections[idxA].id).not.toBe(sections[idxB].id);
  });

  it('moveSection updates the active view', () => {
    const store = createEditorStore(baseEnvelope);
    // sections: [cover, body], view at body (1). Move body to 0.
    store.getState().moveSection(1, 0);
    const sections = store.getState().envelope.document.sections;
    expect(sections[0].kind).toBe('body');
    expect(sections[1].kind).toBe('cover');
    expect(store.getState().view).toEqual({ kind: 'section', sectionIndex: 0 });
  });

  it('removeSection by index switches view when the active section is removed', () => {
    const store = createEditorStore(baseEnvelope);
    // view at body (1). Remove body.
    store.getState().removeSection(1);
    const sections = store.getState().envelope.document.sections;
    expect(sections).toHaveLength(1);
    expect(sections[0].kind).toBe('cover');
    expect(store.getState().view).toEqual({ kind: 'section', sectionIndex: 0 });
  });

  it('renameSection writes section.title and clears it when blank', () => {
    const store = createEditorStore(baseEnvelope);
    store.getState().renameSection(0, '我的封面');
    expect(store.getState().envelope.document.sections[0].title).toBe('我的封面');
    store.getState().renameSection(0, '   ');
    expect(store.getState().envelope.document.sections[0].title).toBeUndefined();
  });
});
