import type { ThesisDocument } from './document';

export interface ApiIssue {
  code: string;
  message: string;
  path?: string | null;
  severity: 'error' | 'warning' | 'info' | string;
  suggestedAction?: string | null;
}

export interface ApiError {
  code: string;
  message: string;
  path?: string | null;
  issues?: ApiIssue[];
}

export interface DocumentEnvelope {
  id: string;
  templateId?: string | null;
  document: ThesisDocument;
  updatedAt: string;
}

export interface DocumentValidationResponse {
  isValid: boolean;
  issues: ApiIssue[];
}

export interface RenderRunResponse {
  runId: string;
  documentId: string;
  templateId: string;
  status: string;
  openXmlValid: boolean;
  formatValid: boolean;
  openXmlErrorCount: number;
  formatErrorCount: number;
  docxPath: string;
  downloadUrl: string;
  inspectSummary: unknown;
  issues: ApiIssue[];
  createdAt: string;
}

export interface AssetUploadResponse {
  assetId: string;
  fileName: string;
  contentType: string;
  size: number;
  imagePath: string;
  previewUrl: string;
}

export interface CreateDocumentRequest {
  templateId?: string | null;
  title?: string;
  author?: string;
  college?: string;
  major?: string;
  studentId?: string;
  advisor?: string;
  date?: string;
}
