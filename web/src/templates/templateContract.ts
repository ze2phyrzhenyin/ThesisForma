import type { ApiIssue, TemplatePackage, TemplateVariable, ThesisFormatSpecDraft } from '@/types';
import { downloadJson } from '@/editor/documentContract';

export interface ParseTemplateResult {
  ok: boolean;
  template?: TemplatePackage;
  issues: ApiIssue[];
}

export function createBlankTemplatePackage(): TemplatePackage {
  return {
    templateSchemaVersion: '1.0.0',
    id: 'local-template',
    name: '本地模板包',
    version: '0.1.0',
    locale: 'zh-CN',
    description: '本地结构化模板包草稿。',
    tags: ['local'],
    formatSpec: createDefaultFormatSpec(),
    variables: [],
    pageTemplates: []
  };
}

export function createDefaultFormatSpec(): ThesisFormatSpecDraft {
  return {
    schemaVersion: '1.2.0',
    name: 'local-template',
    pageSetup: {
      paperSize: 'a4',
      orientation: 'portrait',
      topMarginCm: 2.5,
      bottomMarginCm: 2.5,
      leftMarginCm: 3,
      rightMarginCm: 2.5,
      gutterCm: 0,
      headerDistanceCm: 1.5,
      footerDistanceCm: 1.75,
      columns: 1
    },
    defaultFont: {
      eastAsia: '宋体',
      latin: 'Times New Roman',
      sizePt: 12
    },
    bodyParagraph: {
      lineSpacingMultiple: 1.5,
      spaceBeforePt: 0,
      spaceAfterPt: 0,
      firstLineIndentChars: 2,
      hangingIndentCm: 0,
      alignment: 'both',
      widowControl: true
    },
    tables: {
      widthPercent: 100,
      cellMarginCm: 0.1,
      useThreeLineTables: true,
      captionPosition: 'above',
      repeatHeaderRowsDefault: 1,
      allowRowBreakAcrossPagesDefault: false
    }
  };
}

export function parseTemplatePackageJson(text: string): ParseTemplateResult {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch (error) {
    return {
      ok: false,
      issues: [
        issue(
          'template.json.parse',
          error instanceof Error ? `模板 JSON 解析失败：${error.message}` : '模板 JSON 解析失败。',
          'error',
          '$'
        )
      ]
    };
  }
  if (!isRecord(parsed)) {
    return {
      ok: false,
      issues: [issue('template.type', 'TemplatePackage 必须是 JSON object。', 'error', '$')]
    };
  }
  const template = normalizeTemplatePackage(parsed);
  const issues = validateTemplatePackage(template);
  return {
    ok: !issues.some((item) => item.severity === 'error'),
    template,
    issues
  };
}

export function cleanTemplatePackage(template: TemplatePackage): TemplatePackage {
  return stripNulls({
    templateSchemaVersion: template.templateSchemaVersion,
    id: template.id,
    name: template.name,
    version: template.version,
    locale: template.locale,
    description: template.description,
    school: template.school,
    college: template.college,
    degreeType: template.degreeType,
    tags: template.tags?.filter(Boolean),
    extends: template.extends,
    formatSpec: template.formatSpec,
    formatSpecRef: template.formatSpecRef,
    variables: template.variables?.map(cleanVariable),
    assets: template.assets,
    pageTemplates: template.pageTemplates,
    complianceRules: template.complianceRules,
    notes: template.notes
  });
}

export function validateTemplatePackage(template: TemplatePackage): ApiIssue[] {
  const issues: ApiIssue[] = [];
  if (template.templateSchemaVersion !== '1.0.0') {
    issues.push(issue('template.schemaVersion.unsupported', 'templateSchemaVersion 必须是 1.0.0。', 'error', '$.templateSchemaVersion'));
  }
  for (const key of ['id', 'name', 'version', 'locale'] as const) {
    if (!String(template[key] ?? '').trim()) {
      issues.push(issue('template.required', `${key} 是必填字段。`, 'error', `$.${key}`));
    }
  }
  if (template.id && !/^[A-Za-z][A-Za-z0-9_.-]*$/.test(template.id)) {
    issues.push(issue('template.id.invalid', 'id 必须以字母开头，只能包含字母、数字、下划线、点和横线。', 'error', '$.id'));
  }
  if (template.version && !/^[0-9]+\.[0-9]+\.[0-9]+$/.test(template.version)) {
    issues.push(issue('template.version.invalid', 'version 应形如 1.0.0。', 'error', '$.version'));
  }
  if (template.locale && !/^[a-z]{2}(-[A-Z]{2})?$/.test(template.locale)) {
    issues.push(issue('template.locale.invalid', 'locale 应形如 zh-CN 或 en。', 'error', '$.locale'));
  }
  if (!template.formatSpec && !template.formatSpecRef && !template.extends) {
    issues.push(issue('template.formatSpec.required', '必须提供 formatSpec、formatSpecRef 或 extends。', 'error', '$'));
  }
  validateVariables(template.variables ?? [], issues);
  validateFormatSpec(template.formatSpec, issues);
  validatePageTemplates(template, issues);
  return issues;
}

export function exportTemplatePackage(template: TemplatePackage): void {
  const cleaned = cleanTemplatePackage(template);
  downloadJson(templateFileName(cleaned), cleaned);
}

export function templateFileName(template: TemplatePackage): string {
  return `${safeFilePart(`${template.id || template.name || 'template'}-${template.version || 'draft'}`)}.json`;
}

function normalizeTemplatePackage(value: Record<string, unknown>): TemplatePackage {
  return {
    templateSchemaVersion: value.templateSchemaVersion === '1.0.0' ? '1.0.0' : '1.0.0',
    id: stringValue(value.id),
    name: stringValue(value.name),
    version: stringValue(value.version) || '0.1.0',
    locale: stringValue(value.locale) || 'zh-CN',
    ...(typeof value.description === 'string' ? { description: value.description } : {}),
    ...(typeof value.school === 'string' ? { school: value.school } : {}),
    ...(typeof value.college === 'string' ? { college: value.college } : {}),
    ...(typeof value.degreeType === 'string' ? { degreeType: value.degreeType } : {}),
    ...(Array.isArray(value.tags) ? { tags: value.tags.filter((tag): tag is string => typeof tag === 'string') } : {}),
    ...(isRecord(value.extends)
      ? {
          extends: {
            templateId: stringValue(value.extends.templateId),
            ...(typeof value.extends.versionRange === 'string' ? { versionRange: value.extends.versionRange } : {})
          }
        }
      : {}),
    ...(isRecord(value.formatSpec) ? { formatSpec: value.formatSpec as ThesisFormatSpecDraft } : {}),
    ...(typeof value.formatSpecRef === 'string' ? { formatSpecRef: value.formatSpecRef } : {}),
    variables: Array.isArray(value.variables) ? value.variables.filter(isRecord).map(normalizeVariable) : [],
    ...(Array.isArray(value.assets) ? { assets: value.assets } : {}),
    ...(Array.isArray(value.pageTemplates) ? { pageTemplates: value.pageTemplates as TemplatePackage['pageTemplates'] } : {}),
    ...(Array.isArray(value.complianceRules) ? { complianceRules: value.complianceRules } : {}),
    ...(Array.isArray(value.notes) ? { notes: value.notes.filter((note): note is string => typeof note === 'string') } : {})
  };
}

function normalizeVariable(value: Record<string, unknown>): TemplateVariable {
  return {
    name: stringValue(value.name),
    label: stringValue(value.label),
    type: isVariableType(value.type) ? value.type : 'string',
    ...(typeof value.required === 'boolean' ? { required: value.required } : {}),
    ...(value.defaultValue !== undefined ? { defaultValue: value.defaultValue } : {}),
    ...(typeof value.description === 'string' ? { description: value.description } : {}),
    ...(typeof value.sourcePath === 'string' ? { sourcePath: value.sourcePath } : {}),
    ...(typeof value.pattern === 'string' ? { pattern: value.pattern } : {}),
    ...(Array.isArray(value.enumValues) ? { enumValues: value.enumValues.filter((item): item is string => typeof item === 'string') } : {}),
    ...(typeof value.format === 'string' ? { format: value.format } : {}),
    ...(typeof value.displayOrder === 'number' ? { displayOrder: value.displayOrder } : {})
  };
}

function cleanVariable(variable: TemplateVariable): TemplateVariable {
  return stripNulls({
    name: variable.name,
    label: variable.label,
    type: variable.type,
    required: variable.required,
    defaultValue: variable.defaultValue,
    description: variable.description,
    sourcePath: variable.sourcePath,
    pattern: variable.pattern,
    enumValues: variable.enumValues,
    format: variable.format,
    displayOrder: variable.displayOrder
  });
}

function validateVariables(variables: TemplateVariable[], issues: ApiIssue[]): void {
  const seen = new Map<string, number>();
  variables.forEach((variable, index) => {
    if (!variable.name.trim()) issues.push(issue('template.variable.name.required', '变量 name 不能为空。', 'error', `$.variables[${index}].name`));
    if (!variable.label?.trim()) issues.push(issue('template.variable.label.required', '变量 label 不能为空。', 'error', `$.variables[${index}].label`));
    if (seen.has(variable.name)) {
      issues.push(issue('template.variable.duplicate', `变量 ${variable.name} 重复。`, 'error', `$.variables[${index}].name`));
    }
    seen.set(variable.name, index);
    if (variable.sourcePath && !/^(metadata|variables)\.[A-Za-z][A-Za-z0-9_.-]*$/.test(variable.sourcePath)) {
      issues.push(issue('template.variable.sourcePath.invalid', 'sourcePath 必须以 metadata. 或 variables. 开头。', 'error', `$.variables[${index}].sourcePath`));
    }
  });
}

function validateFormatSpec(formatSpec: ThesisFormatSpecDraft | undefined, issues: ApiIssue[]): void {
  if (!formatSpec) return;
  const page = formatSpec.pageSetup;
  if (page) {
    for (const key of ['topMarginCm', 'bottomMarginCm', 'leftMarginCm', 'rightMarginCm'] as const) {
      const value = page[key];
      if (typeof value === 'number' && (value <= 0 || value > 10)) {
        issues.push(issue('template.formatSpec.margin.invalid', `${key} 必须大于 0 且不超过 10。`, 'error', `$.formatSpec.pageSetup.${key}`));
      }
    }
  }
  const size = formatSpec.defaultFont?.sizePt;
  if (typeof size === 'number' && (size < 1 || size > 72)) {
    issues.push(issue('template.formatSpec.fontSize.invalid', '默认字号必须在 1 到 72 pt 之间。', 'error', '$.formatSpec.defaultFont.sizePt'));
  }
}

function validatePageTemplates(template: TemplatePackage, issues: ApiIssue[]): void {
  const variables = new Set((template.variables ?? []).map((variable) => variable.name));
  (template.pageTemplates ?? []).forEach((pageTemplate, templateIndex) => {
    pageTemplate.blocks?.forEach((block, blockIndex) => {
      if ('variableName' in block && block.variableName && !variables.has(block.variableName)) {
        issues.push(issue('template.pageTemplate.variable.missing', `页面模板引用了不存在的变量 ${block.variableName}。`, 'warning', `$.pageTemplates[${templateIndex}].blocks[${blockIndex}].variableName`));
      }
    });
  });
}

function issue(
  code: string,
  message: string,
  severity: ApiIssue['severity'],
  path: string | null
): ApiIssue {
  return { code, message, severity, path, suggestedAction: null };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function stringValue(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function isVariableType(value: unknown): value is TemplateVariable['type'] {
  return (
    value === 'string' ||
    value === 'multilineText' ||
    value === 'date' ||
    value === 'number' ||
    value === 'boolean' ||
    value === 'enum' ||
    value === 'image' ||
    value === 'richText'
  );
}

function stripNulls<T>(value: T): T {
  if (Array.isArray(value)) return value.map(stripNulls) as T;
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

function safeFilePart(value: string): string {
  return value
    .trim()
    .replace(/[\\/:*?"<>|]+/g, '-')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')
    .slice(0, 120)
    .replace(/^-|-$/g, '');
}
