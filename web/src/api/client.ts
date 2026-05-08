import type {
  ApiError,
  AssetUploadResponse,
  CreateDocumentRequest,
  DocumentEnvelope,
  DocumentValidationResponse,
  RenderRunResponse,
  TemplateDetail,
  TemplateSummary,
  ThesisDocument
} from '@/types';
import {
  cleanThesisDocument,
  makeDocumentEnvelope,
  validateThesisDocument
} from '@/editor/documentContract';
import {
  createLocalDocument,
  getLocalDocument,
  importLocalDocument,
  isLocalDocumentId,
  LOCAL_TEMPLATE_SUMMARIES,
  saveLocalDocument,
  validateLocalDocument
} from '@/editor/localDrafts';

const BASE = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
export const isApiBacked = BASE.length > 0;

export class ThesisApiError extends Error {
  readonly status: number;
  readonly payload: ApiError | null;

  constructor(status: number, payload: ApiError | null, fallback: string) {
    super(payload?.message ?? fallback);
    this.status = status;
    this.payload = payload;
    this.name = 'ThesisApiError';
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      ...(init?.headers ?? {})
    },
    ...init
  });

  if (!res.ok) {
    let payload: ApiError | null = null;
    try {
      payload = (await res.json()) as ApiError;
    } catch {
      // ignore
    }
    throw new ThesisApiError(res.status, payload, `${init?.method ?? 'GET'} ${path} failed: ${res.status}`);
  }

  if (res.status === 204) {
    return undefined as T;
  }

  const contentType = res.headers.get('content-type') ?? '';
  if (contentType.includes('application/json')) {
    return (await res.json()) as T;
  }
  return (await res.text()) as unknown as T;
}

// ───── Templates ───────────────────────────────────────────────────────────

export async function listTemplates(): Promise<TemplateSummary[]> {
  if (!isApiBacked) return LOCAL_TEMPLATE_SUMMARIES;
  const res = await request<{ templates: TemplateSummary[] }>('/api/templates');
  return res.templates;
}

export async function getTemplate(id: string): Promise<TemplateDetail> {
  if (!isApiBacked) {
    const summary = LOCAL_TEMPLATE_SUMMARIES.find((item) => item.id === id);
    if (!summary) throw new ThesisApiError(404, null, `Template not found: ${id}`);
    return {
      summary,
      variables: [],
      pageTemplates: [],
      knownGaps: ['静态前端仅提供本地草稿模板占位；真实 TemplatePackage 可在模板编辑器导入。'],
      formatSpecRef: null
    };
  }
  return request<TemplateDetail>(`/api/templates/${encodeURIComponent(id)}`);
}

// ───── Documents ───────────────────────────────────────────────────────────

export async function createDocument(req: CreateDocumentRequest): Promise<DocumentEnvelope> {
  if (!isApiBacked) return createLocalDocument(req);
  return request<DocumentEnvelope>('/api/documents', {
    method: 'POST',
    body: JSON.stringify(req)
  });
}

export async function getDocument(id: string): Promise<DocumentEnvelope> {
  if (!isApiBacked || isLocalDocumentId(id)) return getLocalDocument(id);
  return request<DocumentEnvelope>(`/api/documents/${encodeURIComponent(id)}`);
}

export async function saveDocument(
  id: string,
  document: ThesisDocument,
  templateId?: string | null
): Promise<DocumentEnvelope> {
  if (!isApiBacked || isLocalDocumentId(id)) {
    return saveLocalDocument(id, cleanThesisDocument(document), templateId ?? null);
  }
  const cleaned = cleanThesisDocument(document);
  const issues = validateThesisDocument(cleaned).filter((item) => item.severity === 'error');
  if (issues.length) {
    throw new ThesisApiError(
      400,
      { code: 'document.invalid', message: '文档结构校验未通过，未保存到后端。', issues },
      'Document validation failed'
    );
  }
  return request<DocumentEnvelope>(`/api/documents/${encodeURIComponent(id)}`, {
    method: 'PUT',
    body: JSON.stringify({ document: cleaned, templateId: templateId ?? null })
  });
}

export async function validateDocument(
  id: string,
  templateId?: string | null
): Promise<DocumentValidationResponse> {
  if (!isApiBacked || isLocalDocumentId(id)) return validateLocalDocument(id);
  return request<DocumentValidationResponse>(`/api/documents/${encodeURIComponent(id)}/validate`, {
    method: 'POST',
    body: JSON.stringify({ templateId: templateId ?? null })
  });
}

export async function renderDocument(
  id: string,
  templateId?: string | null
): Promise<RenderRunResponse> {
  if (!isApiBacked || isLocalDocumentId(id)) {
    throw new ThesisApiError(
      400,
      {
        code: 'render.unavailable.static',
        message: '静态前端不连接生产后端；当前请导出 ThesisDocument JSON。',
        path: null,
        issues: []
      },
      'Render unavailable in static frontend'
    );
  }
  return request<RenderRunResponse>(`/api/documents/${encodeURIComponent(id)}/render`, {
    method: 'POST',
    body: JSON.stringify({ templateId: templateId ?? null })
  });
}

export async function getRun(runId: string): Promise<RenderRunResponse> {
  return request<RenderRunResponse>(`/api/runs/${encodeURIComponent(runId)}`);
}

export function runDownloadUrl(runId: string): string {
  return `${BASE}/api/runs/${encodeURIComponent(runId)}/download`;
}

// ───── Import / Export ─────────────────────────────────────────────────────

export async function importDocumentJson(
  document: ThesisDocument,
  templateId?: string | null
): Promise<DocumentEnvelope> {
  if (!isApiBacked) return importLocalDocument(cleanThesisDocument(document), templateId ?? null);
  return request<DocumentEnvelope>('/api/documents/import-json', {
    method: 'POST',
    body: JSON.stringify({ document: cleanThesisDocument(document), templateId: templateId ?? null })
  });
}

export function exportDocumentJsonUrl(id: string): string {
  if (!isApiBacked || isLocalDocumentId(id)) {
    const draft = getLocalDocument(id);
    return URL.createObjectURL(
      new Blob([`${JSON.stringify(makeDocumentEnvelope(id, draft.document, draft.templateId).document, null, 2)}\n`], {
        type: 'application/json'
      })
    );
  }
  return `${BASE}/api/documents/${encodeURIComponent(id)}/export-json`;
}

// ───── Assets ──────────────────────────────────────────────────────────────

export async function uploadImage(file: File): Promise<AssetUploadResponse> {
  const form = new FormData();
  form.append('file', file);
  const res = await fetch(`${BASE}/api/assets/images`, { method: 'POST', body: form });
  if (!res.ok) {
    let payload: ApiError | null = null;
    try {
      payload = (await res.json()) as ApiError;
    } catch {
      // ignore
    }
    throw new ThesisApiError(res.status, payload, `Upload failed: ${res.status}`);
  }
  return (await res.json()) as AssetUploadResponse;
}

export function assetUrl(assetId: string): string {
  return `${BASE}/api/assets/${encodeURIComponent(assetId)}`;
}
