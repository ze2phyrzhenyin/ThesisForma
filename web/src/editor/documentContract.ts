import type { ApiIssue, DocumentEnvelope, DocumentOverrides, ThesisDocument } from '@/types';
import { cleanThesisDocument } from './documentCleaner';
import { normalizeMetadata, normalizeSections } from './documentNormalizer';
import {
  issue,
  isRecord,
  safeFilePart,
  SUPPORTED_SCHEMA_VERSIONS
} from './documentContractUtils';
import { validateThesisDocument } from './documentValidator';

export { cleanThesisDocument } from './documentCleaner';
export { validateThesisDocument } from './documentValidator';

export interface ParseDocumentResult {
  ok: boolean;
  document?: ThesisDocument;
  issues: ApiIssue[];
}

export function parseThesisDocumentJson(text: string): ParseDocumentResult {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch (error) {
    return {
      ok: false,
      issues: [
        issue(
          'json.parse',
          error instanceof Error ? `JSON 解析失败：${error.message}` : 'JSON 解析失败。',
          'error',
          '$',
          '请确认文件是合法 JSON。'
        )
      ]
    };
  }

  return normalizeThesisDocument(parsed);
}

export function normalizeThesisDocument(value: unknown): ParseDocumentResult {
  const issues: ApiIssue[] = [];
  if (!isRecord(value)) {
    return {
      ok: false,
      issues: [issue('document.type', 'ThesisDocument 必须是 JSON object。', 'error', '$')]
    };
  }

  const schemaVersion = value.schemaVersion;
  if (typeof schemaVersion !== 'string') {
    issues.push(
      issue('document.schemaVersion.required', '缺少 schemaVersion。', 'error', '$.schemaVersion')
    );
  } else if (!SUPPORTED_SCHEMA_VERSIONS.has(schemaVersion)) {
    issues.push(
      issue(
        'document.schemaVersion.unsupported',
        `暂不支持 schemaVersion=${schemaVersion}。`,
        'error',
        '$.schemaVersion'
      )
    );
  }

  if (!isRecord(value.metadata)) {
    issues.push(issue('document.metadata.required', '缺少 metadata。', 'error', '$.metadata'));
  }
  if (!Array.isArray(value.sections)) {
    issues.push(issue('document.sections.required', '缺少 sections 数组。', 'error', '$.sections'));
  }

  if (issues.some((i) => i.severity === 'error')) {
    return { ok: false, issues };
  }

  const document: ThesisDocument = {
    schemaVersion: schemaVersion as ThesisDocument['schemaVersion'],
    metadata: normalizeMetadata(value.metadata as Record<string, unknown>, issues),
    sections: normalizeSections(value.sections as unknown[], issues)
  };

  if (document.sections.length === 0) {
    issues.push(
      issue('document.sections.empty', '至少需要一个 section。', 'error', '$.sections')
    );
  }

  issues.push(...validateThesisDocument(document));
  return {
    ok: !issues.some((i) => i.severity === 'error'),
    document,
    issues
  };
}

export function exportFileNameForDocument(document: ThesisDocument): string {
  const parts = [
    document.metadata.title || 'thesis-document',
    document.metadata.author,
    document.metadata.date
  ].filter(Boolean);
  return `${safeFilePart(parts.join('-') || 'thesis-document')}.json`;
}

export function downloadJson(filename: string, value: unknown): void {
  const blob = new Blob([`${JSON.stringify(value, null, 2)}\n`], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export function makeDocumentEnvelope(
  id: string,
  document: ThesisDocument,
  templateId?: string | null,
  updatedAt = new Date().toISOString(),
  overrides?: DocumentOverrides | null
): DocumentEnvelope {
  return {
    id,
    templateId: templateId ?? null,
    document: cleanThesisDocument(document),
    overrides: overrides ?? null,
    updatedAt
  };
}
