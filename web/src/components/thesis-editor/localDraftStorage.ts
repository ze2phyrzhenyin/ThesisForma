export type LocalDraftEntry = {
  id: string;
  title: string;
  templateId: string;
  updatedAt: string;
};

const LOCAL_DOCUMENT_PREFIX = 'thesisforma.document.';

export function createLocalDraftId() {
  return `doc-${globalThis.crypto?.randomUUID?.() ?? Math.random().toString(36).slice(2, 10)}`;
}

export function localDocumentKey(id: string) {
  return `${LOCAL_DOCUMENT_PREFIX}${id}`;
}

export function loadLocalDrafts(): LocalDraftEntry[] {
  const entries: LocalDraftEntry[] = [];
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    if (!key?.startsWith(LOCAL_DOCUMENT_PREFIX)) continue;
    try {
      const raw = localStorage.getItem(key);
      if (!raw) continue;
      const envelope = JSON.parse(raw) as {
        id?: string;
        document?: { metadata?: { title?: string } };
        templateId?: string;
        updatedAt?: string;
      };
      entries.push({
        id: envelope.id ?? key.replace(LOCAL_DOCUMENT_PREFIX, ''),
        title: envelope.document?.metadata?.title?.trim() || '未命名论文',
        templateId: envelope.templateId ?? '',
        updatedAt: envelope.updatedAt ?? ''
      });
    } catch {
      // Ignore malformed local drafts.
    }
  }
  return entries.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt)).slice(0, 12);
}

export function saveLocalDraft(id: string, document: unknown, templateId?: string) {
  const envelope = { id, document, templateId, updatedAt: new Date().toISOString() };
  localStorage.setItem(localDocumentKey(id), JSON.stringify(envelope));
  return envelope;
}

export function readLocalDraft(id: string) {
  return localStorage.getItem(localDocumentKey(id));
}

export function deleteLocalDraft(id: string) {
  localStorage.removeItem(localDocumentKey(id));
}
