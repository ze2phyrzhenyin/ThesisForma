/**
 * Subset of TemplatePackage exposed by the API.
 * Only fields needed by the frontend are typed; the rest pass through as unknown.
 */

export interface TemplateSummary {
  id: string;
  name: string;
  school: string;
  college: string;
  version: string;
  status: 'ready' | 'draft' | string;
  coverage: number;
  readiness: 'ready' | 'review' | string;
  tags: string[];
  path: string;
}

export type TemplateVariableType =
  | 'string'
  | 'multilineText'
  | 'date'
  | 'number'
  | 'boolean'
  | 'enum'
  | 'image'
  | 'richText';

export interface TemplateVariable {
  name: string;
  label?: string;
  type: TemplateVariableType;
  required?: boolean;
  defaultValue?: unknown;
  description?: string;
  sourcePath?: string;
  pattern?: string;
  enumValues?: string[];
  format?: string;
  displayOrder?: number;
}

export interface TemplateDetail {
  summary: TemplateSummary;
  variables: TemplateVariable[];
  pageTemplates: unknown[];
  knownGaps: string[];
  formatSpecRef?: string | null;
}
