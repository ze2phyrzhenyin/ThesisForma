import type { ApiIssue, SectionKind, ThesisDocument } from '@/types';

export const SUPPORTED_SCHEMA_VERSIONS = new Set(['1.0.0', '1.1.0']);
export const SECTION_KINDS = new Set<SectionKind>([
  'cover',
  'originalityStatement',
  'abstract',
  'toc',
  'body',
  'acknowledgements',
  'bibliography',
  'appendix'
]);

export function stripNulls<T>(value: T): T {
  if (Array.isArray(value)) return value.map((v) => stripNulls(v)) as T;
  if (value && typeof value === 'object') {
    const out: Record<string, unknown> = {};
    for (const [key, item] of Object.entries(value as Record<string, unknown>)) {
      if (item === null || item === undefined) continue;
      out[key] = stripNulls(item);
    }
    return out as T;
  }
  return value;
}

export function issue(
  code: string,
  message: string,
  severity: ApiIssue['severity'] = 'error',
  path?: string | null,
  suggestedAction?: string | null
): ApiIssue {
  return { code, message, severity, path: path ?? null, suggestedAction: suggestedAction ?? null };
}

export function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

export function stringValue(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

export function normalizeHeadingLevel(value: unknown): 1 | 2 | 3 | 4 | 5 | 6 {
  return value === 1 || value === 2 || value === 3 || value === 4 || value === 5 || value === 6
    ? value
    : 1;
}

export function isAlignment(value: unknown): value is 'left' | 'center' | 'right' | 'both' {
  return value === 'left' || value === 'center' || value === 'right' || value === 'both';
}

export function isImageContentType(value: unknown): value is ThesisDocument['sections'][number]['blocks'][number] extends infer B
  ? B extends { type: 'figure'; imageContentType: infer T }
    ? T
    : never
  : never {
  return (
    value === 'image/png' ||
    value === 'image/jpeg' ||
    value === 'image/jpg' ||
    value === 'image/gif' ||
    value === 'image/bmp' ||
    value === 'image/tiff'
  );
}

export function safeFilePart(value: string): string {
  return value
    .trim()
    .replace(/[\\/:*?"<>|]+/g, '-')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')
    .slice(0, 120)
    .replace(/^-|-$/g, '');
}
