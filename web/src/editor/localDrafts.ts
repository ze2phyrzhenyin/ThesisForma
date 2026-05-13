import type {
  CreateDocumentRequest,
  DocumentEnvelope,
  DocumentOverrides,
  DocumentValidationResponse,
  TemplateSummary,
  ThesisDocument
} from '@/types';
import {
  cleanThesisDocument,
  makeDocumentEnvelope,
  validateThesisDocument
} from './documentContract';

const STORAGE_KEY = 'thesisforma.localDrafts.v1';
const ACTIVE_KEY = 'thesisforma.activeDraftId.v1';

export interface LocalDraftRecord extends DocumentEnvelope {
  createdAt: string;
}

export interface LocalDraftSummary {
  id: string;
  title: string;
  author: string;
  templateId?: string | null;
  updatedAt: string;
  createdAt: string;
}

export const LOCAL_TEMPLATE_SUMMARIES: TemplateSummary[] = [
  {
    id: 'local-structured-thesis',
    name: '本地结构化论文草稿',
    school: 'TemplatePackage',
    college: 'Local Draft',
    version: '0.1.0',
    status: 'draft',
    coverage: 0,
    readiness: 'review',
    tags: ['local', 'json', 'structured'],
    path: 'local'
  }
];

export function isLocalDocumentId(id: string | undefined): boolean {
  return Boolean(id?.startsWith('local-'));
}

export function listLocalDrafts(): LocalDraftSummary[] {
  return readDrafts()
    .map((draft) => ({
      id: draft.id,
      title: draft.document.metadata.title || '未命名论文',
      author: draft.document.metadata.author || '',
      templateId: draft.templateId ?? null,
      createdAt: draft.createdAt,
      updatedAt: draft.updatedAt
    }))
    .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
}

export function getActiveDraftId(): string | null {
  return safeStorage()?.getItem(ACTIVE_KEY) ?? null;
}

export function setActiveDraftId(id: string): void {
  safeStorage()?.setItem(ACTIVE_KEY, id);
}

export function createLocalDocument(req: CreateDocumentRequest = {}): DocumentEnvelope {
  const now = new Date().toISOString();
  const document = createBlankThesisDocument(req);
  const record: LocalDraftRecord = {
    ...makeDocumentEnvelope(newDraftId('local-doc'), document, req.templateId ?? null, now, req.overrides ?? null),
    createdAt: now
  };
  upsertDraft(record);
  setActiveDraftId(record.id);
  return record;
}

export function importLocalDocument(
  document: ThesisDocument,
  templateId?: string | null,
  overrides?: DocumentOverrides | null
): DocumentEnvelope {
  const now = new Date().toISOString();
  const record: LocalDraftRecord = {
    ...makeDocumentEnvelope(newDraftId('local-doc'), document, templateId ?? null, now, overrides ?? null),
    createdAt: now
  };
  upsertDraft(record);
  setActiveDraftId(record.id);
  return record;
}

export function getLocalDocument(id: string): DocumentEnvelope {
  const record = readDrafts().find((draft) => draft.id === id);
  if (!record) {
    throw new Error(`本地草稿不存在：${id}`);
  }
  setActiveDraftId(record.id);
  return record;
}

export function saveLocalDocument(
  id: string,
  document: ThesisDocument,
  templateId?: string | null,
  overrides?: DocumentOverrides | null
): DocumentEnvelope {
  const drafts = readDrafts();
  const existing = drafts.find((draft) => draft.id === id);
  const now = new Date().toISOString();
  const record: LocalDraftRecord = {
    ...makeDocumentEnvelope(id, document, templateId ?? existing?.templateId ?? null, now, overrides ?? existing?.overrides ?? null),
    createdAt: existing?.createdAt ?? now
  };
  writeDrafts([record, ...drafts.filter((draft) => draft.id !== id)]);
  setActiveDraftId(id);
  return record;
}

export function duplicateLocalDraft(id: string): DocumentEnvelope {
  const source = getLocalDocument(id);
  const now = new Date().toISOString();
  const document: ThesisDocument = {
    ...source.document,
    metadata: {
      ...source.document.metadata,
      title: `${source.document.metadata.title || '未命名论文'} 副本`
    }
  };
  const copy: LocalDraftRecord = {
    ...makeDocumentEnvelope(newDraftId('local-doc'), document, source.templateId ?? null, now, source.overrides ?? null),
    createdAt: now
  };
  upsertDraft(copy);
  setActiveDraftId(copy.id);
  return copy;
}

export function deleteLocalDraft(id: string): void {
  writeDrafts(readDrafts().filter((draft) => draft.id !== id));
  if (getActiveDraftId() === id) {
    const next = listLocalDrafts()[0]?.id;
    if (next) safeStorage()?.setItem(ACTIVE_KEY, next);
    else safeStorage()?.removeItem(ACTIVE_KEY);
  }
}

export function validateLocalDocument(id: string): DocumentValidationResponse {
  const draft = getLocalDocument(id);
  const issues = validateThesisDocument(draft.document);
  return {
    isValid: !issues.some((item) => item.severity === 'error'),
    issues
  };
}

export function createBlankThesisDocument(req: CreateDocumentRequest = {}): ThesisDocument {
  const title = req.title?.trim() || '未命名论文';
  return {
    schemaVersion: '1.1.0',
    metadata: {
      title,
      author: req.author?.trim() || '作者',
      college: req.college?.trim() || '学院',
      major: req.major?.trim() || '专业',
      studentId: req.studentId?.trim() || '学号',
      advisor: req.advisor?.trim() || '指导教师',
      date: req.date?.trim() || new Date().toISOString().slice(0, 10),
      language: 'zh-CN'
    },
    sections: [
      { id: 'cover', kind: 'cover', title: '封面', blocks: [] },
      {
        id: 'originalityStatement',
        kind: 'originalityStatement',
        title: '原创声明',
        blocks: []
      },
      { id: 'abstract', kind: 'abstract', title: '摘要', blocks: [] },
      { id: 'toc', kind: 'toc', title: '目录', blocks: [] },
      {
        id: 'body',
        kind: 'body',
        title: '正文',
        blocks: [
          {
            type: 'heading',
            id: newDraftId('h'),
            level: 1,
            bookmarkName: newDraftId('h'),
            inlines: [{ type: 'text', text: '绪论' }]
          },
          {
            type: 'paragraph',
            id: newDraftId('p'),
            inlines: [{ type: 'text', text: '从这里开始写作。' }]
          }
        ]
      },
      { id: 'acknowledgements', kind: 'acknowledgements', title: '致谢', blocks: [] },
      {
        id: 'bibliography',
        kind: 'bibliography',
        title: '参考文献',
        blocks: [
          {
            type: 'bibliography',
            id: newDraftId('bib'),
            entries: [{ id: 'ref-1', text: '作者. 文献题名. 出版地: 出版社, 年.' }]
          }
        ]
      },
      { id: 'appendix', kind: 'appendix', title: '附录', blocks: [] }
    ]
  };
}

function upsertDraft(record: LocalDraftRecord): void {
  const drafts = readDrafts();
  writeDrafts([record, ...drafts.filter((draft) => draft.id !== record.id)]);
}

function readDrafts(): LocalDraftRecord[] {
  const storage = safeStorage();
  if (!storage) return [];
  const raw = storage.getItem(STORAGE_KEY);
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(isDraftRecord).map((draft) => ({
      ...draft,
      document: cleanThesisDocument(draft.document)
    }));
  } catch {
    return [];
  }
}

function writeDrafts(drafts: LocalDraftRecord[]): void {
  safeStorage()?.setItem(STORAGE_KEY, JSON.stringify(drafts.slice(0, 100)));
}

function isDraftRecord(value: unknown): value is LocalDraftRecord {
  if (!value || typeof value !== 'object') return false;
  const record = value as LocalDraftRecord;
  return (
    typeof record.id === 'string' &&
    typeof record.updatedAt === 'string' &&
    typeof record.createdAt === 'string' &&
    Boolean(record.document) &&
    typeof record.document === 'object'
  );
}

function safeStorage(): Storage | null {
  if (typeof window === 'undefined') return null;
  return window.localStorage;
}

function newDraftId(prefix: string): string {
  const random =
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID().replace(/-/g, '').slice(0, 10)
      : Math.random().toString(36).slice(2, 12);
  return `${prefix}-${Date.now().toString(36)}-${random}`;
}
