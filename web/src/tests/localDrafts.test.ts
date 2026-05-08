import { beforeEach, describe, expect, it } from 'vitest';
import {
  createLocalDocument,
  deleteLocalDraft,
  duplicateLocalDraft,
  getActiveDraftId,
  getLocalDocument,
  listLocalDrafts,
  saveLocalDocument
} from '@/editor/localDrafts';
import type { ThesisDocument } from '@/types';

const STORAGE_KEY = 'thesisforma.localDrafts.v1';

function doc(title: string): ThesisDocument {
  return {
    schemaVersion: '1.1.0',
    metadata: {
      title,
      author: '张三',
      college: '信息学院',
      major: '软件工程',
      studentId: '20260001',
      advisor: '李四',
      date: '2026-05-08',
      language: 'zh-CN'
    },
    sections: [{ kind: 'body', blocks: [{ type: 'paragraph', inlines: [{ type: 'text', text: '正文' }] }] }]
  };
}

describe('local drafts', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('creates, saves, opens, duplicates, and deletes local drafts', () => {
    const created = createLocalDocument({ title: '草稿一' });
    saveLocalDocument(created.id, doc('草稿一修订'));

    expect(getActiveDraftId()).toBe(created.id);
    expect(getLocalDocument(created.id).document.metadata.title).toBe('草稿一修订');

    const copy = duplicateLocalDraft(created.id);
    expect(copy.document.metadata.title).toBe('草稿一修订 副本');
    expect(listLocalDrafts().map((draft) => draft.id)).toContain(copy.id);

    deleteLocalDraft(created.id);
    expect(() => getLocalDocument(created.id)).toThrow('本地草稿不存在');
  });

  it('sorts recent drafts by updatedAt descending', () => {
    const older = {
      id: 'local-doc-old',
      templateId: null,
      document: doc('旧草稿'),
      createdAt: '2026-05-01T00:00:00.000Z',
      updatedAt: '2026-05-01T00:00:00.000Z'
    };
    const newer = {
      ...older,
      id: 'local-doc-new',
      document: doc('新草稿'),
      updatedAt: '2026-05-02T00:00:00.000Z'
    };
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify([older, newer]));

    expect(listLocalDrafts().map((draft) => draft.id)).toEqual(['local-doc-new', 'local-doc-old']);
  });

  it('recovers from damaged localStorage data without crashing', () => {
    window.localStorage.setItem(STORAGE_KEY, '{bad');
    expect(listLocalDrafts()).toEqual([]);
  });

  it('keeps old draft records without optional templateId compatible', () => {
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify([
        {
          id: 'local-doc-legacy',
          document: doc('旧版本草稿'),
          createdAt: '2026-05-01T00:00:00.000Z',
          updatedAt: '2026-05-01T00:00:00.000Z'
        }
      ])
    );

    expect(getLocalDocument('local-doc-legacy').document.metadata.title).toBe('旧版本草稿');
  });
});
