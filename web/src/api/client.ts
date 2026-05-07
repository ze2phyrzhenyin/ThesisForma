import type { AssetRef, RenderRun, TemplateSummary } from '../components/thesis-editor/types';
import { createLocalDraftId, localDocumentKey, saveLocalDraft } from '../components/thesis-editor/localDraftStorage';

const apiBase = normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL ?? import.meta.env.VITE_API_BASE ?? '');
const docxRenderEnabled = parseBoolean(import.meta.env.VITE_ENABLE_DOCX_RENDER, false);

export const appConfig = {
  appMode: import.meta.env.VITE_APP_MODE ?? 'frontend-only',
  apiBase,
  docxRenderEnabled: docxRenderEnabled && apiBase.length > 0,
  localExportEnabled: parseBoolean(import.meta.env.VITE_ENABLE_LOCAL_EXPORT, true)
};

const demoTemplates: TemplateSummary[] = [
  {
    id: 'example-university-engineering',
    name: 'Example University Engineering Thesis',
    school: 'Example University',
    college: 'Example Engineering College',
    version: '1.0.0',
    status: 'ready',
    coverage: 0.875,
    readiness: 'ready',
    tags: ['example']
  }
];

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  if (!apiBase) {
    throw new Error('API is not configured for this frontend deployment.');
  }
  const response = await fetch(apiBase + url, {
    headers: init?.body instanceof FormData ? undefined : { 'content-type': 'application/json', ...init?.headers },
    ...init
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => ({ message: response.statusText }));
    throw new Error(payload.message ?? `Request failed: ${response.status}`);
  }
  return response.json() as Promise<T>;
}

export const templateApi = {
  async list(): Promise<TemplateSummary[]> {
    if (!apiBase) {
      return demoTemplates;
    }
    const response = await request<{ templates: TemplateSummary[] }>('/api/templates');
    return response.templates;
  }
};

export const documentApi = {
  create(body: unknown) {
    if (!apiBase) {
      const id = createLocalDraftId();
      const envelope = saveLocalDraft(id, body, readTemplateId(body));
      return Promise.resolve(envelope);
    }
    return request<{ id: string; document: unknown; templateId?: string }>('/api/documents', {
      method: 'POST',
      body: JSON.stringify(body)
    });
  },
  save(id: string, document: unknown, templateId?: string) {
    if (!apiBase) {
      const envelope = saveLocalDraft(id, document, templateId);
      return Promise.resolve(envelope);
    }
    return request<{ id: string; document: unknown; templateId?: string }>(`/api/documents/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ document, templateId })
    });
  },
  validate(id: string, templateId?: string) {
    if (!apiBase) {
      void id;
      void templateId;
      return Promise.resolve({ isValid: true, issues: [] });
    }
    return request<{ isValid: boolean; issues: any[] }>(`/api/documents/${id}/validate`, {
      method: 'POST',
      body: JSON.stringify({ templateId })
    });
  },
  exportJson(id: string) {
    if (!apiBase) {
      const item = localStorage.getItem(localDocumentKey(id));
      return Promise.resolve(new Response(item ?? '{}', { headers: { 'content-type': 'application/json' } }));
    }
    return fetch(apiBase + `/api/documents/${id}/export-json`, { method: 'POST' });
  },
  importJson(document: unknown, templateId?: string) {
    if (!apiBase) {
      const id = createLocalDraftId();
      const envelope = saveLocalDraft(id, document, templateId);
      return Promise.resolve(envelope);
    }
    return request<{ id: string; document: unknown; templateId?: string }>('/api/documents/import-json', {
      method: 'POST',
      body: JSON.stringify({ document, templateId })
    });
  }
};

export const assetApi = {
  async uploadImage(file: File): Promise<AssetRef> {
    if (!apiBase) {
      const assetId = localId('asset');
      return {
        assetId,
        fileName: file.name,
        imagePath: `local-assets/${assetId}/${safeFileName(file.name)}`,
        previewUrl: typeof URL.createObjectURL === 'function' ? URL.createObjectURL(file) : '',
        contentType: file.type || 'application/octet-stream'
      };
    }
    const form = new FormData();
    form.append('file', file);
    const response = await request<{ assetId: string; fileName: string; imagePath: string; previewUrl: string; contentType: string }>('/api/assets/images', {
      method: 'POST',
      body: form
    });
    return response;
  }
};

export const renderApi = {
  render(documentId: string, templateId?: string) {
    if (!appConfig.docxRenderEnabled) {
      void templateId;
      return Promise.resolve({
        runId: 'render-disabled',
        status: 'disabled',
        openXmlValid: false,
        formatValid: false,
        downloadUrl: '',
        issues: [{
          code: 'render.backendRequired',
          severity: 'info',
          message: '当前部署仅支持结构化编辑与 JSON 导出；DOCX 生成需要连接后端渲染服务。',
          suggestedAction: '部署 ThesisDocx.Api 或设置 VITE_API_BASE_URL 并启用 VITE_ENABLE_DOCX_RENDER。'
        }]
      } satisfies RenderRun);
    }
    return request<RenderRun>(`/api/documents/${documentId}/render`, {
      method: 'POST',
      body: JSON.stringify({ templateId })
    });
  },
  getRun(runId: string) {
    return request<RenderRun>(`/api/runs/${runId}`);
  }
};

function parseBoolean(value: unknown, fallback: boolean) {
  if (typeof value !== 'string' || value.length === 0) return fallback;
  return value.toLowerCase() === 'true';
}

function normalizeBaseUrl(value: string) {
  return value.trim().replace(/\/$/, '');
}

function localId(prefix: string) {
  return `${prefix}-${globalThis.crypto?.randomUUID?.() ?? Math.random().toString(36).slice(2, 10)}`;
}

function readTemplateId(body: unknown) {
  return typeof body === 'object' && body !== null && 'templateId' in body
    ? String((body as { templateId?: unknown }).templateId ?? '')
    : undefined;
}

function safeFileName(value: string) {
  return value.replace(/[^a-zA-Z0-9._-]/g, '_');
}
