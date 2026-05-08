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

const BASE = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

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
  const res = await request<{ templates: TemplateSummary[] }>('/api/templates');
  return res.templates;
}

export async function getTemplate(id: string): Promise<TemplateDetail> {
  return request<TemplateDetail>(`/api/templates/${encodeURIComponent(id)}`);
}

// ───── Documents ───────────────────────────────────────────────────────────

export async function createDocument(req: CreateDocumentRequest): Promise<DocumentEnvelope> {
  return request<DocumentEnvelope>('/api/documents', {
    method: 'POST',
    body: JSON.stringify(req)
  });
}

export async function getDocument(id: string): Promise<DocumentEnvelope> {
  return request<DocumentEnvelope>(`/api/documents/${encodeURIComponent(id)}`);
}

export async function saveDocument(
  id: string,
  document: ThesisDocument,
  templateId?: string | null
): Promise<DocumentEnvelope> {
  return request<DocumentEnvelope>(`/api/documents/${encodeURIComponent(id)}`, {
    method: 'PUT',
    body: JSON.stringify({ document: stripNulls(document), templateId: templateId ?? null })
  });
}

/**
 * Recursively drop properties whose value is `null` or `undefined`. The .NET
 * serializer emits nulls for optional fields, but our JSON schema validator
 * rejects them ("string" doesn't allow null). We sanitize before sending.
 */
function stripNulls<T>(value: T): T {
  if (Array.isArray(value)) {
    return value.map((v) => stripNulls(v)) as unknown as T;
  }
  if (value && typeof value === 'object') {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value as Record<string, unknown>)) {
      if (v === null || v === undefined) continue;
      out[k] = stripNulls(v);
    }
    return out as unknown as T;
  }
  return value;
}

export async function validateDocument(
  id: string,
  templateId?: string | null
): Promise<DocumentValidationResponse> {
  return request<DocumentValidationResponse>(`/api/documents/${encodeURIComponent(id)}/validate`, {
    method: 'POST',
    body: JSON.stringify({ templateId: templateId ?? null })
  });
}

export async function renderDocument(
  id: string,
  templateId?: string | null
): Promise<RenderRunResponse> {
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
  return request<DocumentEnvelope>('/api/documents/import-json', {
    method: 'POST',
    body: JSON.stringify({ document: stripNulls(document), templateId: templateId ?? null })
  });
}

export function exportDocumentJsonUrl(id: string): string {
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
