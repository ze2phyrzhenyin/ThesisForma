import type {
  ApiIssue,
  PageTemplateDraft,
  TemplateAsset,
  TemplateLayoutBlock,
  TemplateMetadataFieldBlock,
  TemplatePackage,
  TemplateVariable,
  ThesisFormatSpecDraft
} from '@/types';
import { downloadJson } from '@/editor/documentContract';
import {
  METADATA_SOURCE_PATHS,
  cleanLayoutBlock,
  cleanTemplateAsset,
  isPageTemplateBlockType
} from './pageTemplateBlocks';

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
    assets: template.assets?.map(cleanTemplateAsset),
    pageTemplates: template.pageTemplates?.map(cleanPageTemplate),
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
  validateAssets(template.assets ?? [], issues);
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
    templateSchemaVersion: stringValue(value.templateSchemaVersion) || '1.0.0',
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
    ...(Array.isArray(value.assets) ? { assets: value.assets.filter(isRecord).map(normalizeAsset) } : {}),
    ...(Array.isArray(value.pageTemplates)
      ? { pageTemplates: value.pageTemplates.filter(isRecord).map(normalizePageTemplate) }
      : {}),
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

function normalizeAsset(value: Record<string, unknown>): TemplateAsset {
  return {
    id: stringValue(value.id),
    type: stringValue(value.type) || 'image',
    path: stringValue(value.path),
    contentType: stringValue(value.contentType),
    ...(typeof value.description === 'string' ? { description: value.description } : {}),
    ...(typeof value.required === 'boolean' ? { required: value.required } : {})
  };
}

function normalizePageTemplate(value: Record<string, unknown>): PageTemplateDraft {
  return {
    id: stringValue(value.id),
    targetSectionType: stringValue(value.targetSectionType) || 'cover',
    insertPosition: stringValue(value.insertPosition) || 'replaceSectionContent',
    ...(isRecord(value.pageSetupOverride) ? { pageSetupOverride: value.pageSetupOverride } : {}),
    blocks: Array.isArray(value.blocks)
      ? value.blocks.map(normalizeLayoutBlock).filter((block): block is TemplateLayoutBlock => block !== null)
      : []
  };
}

function normalizeLayoutBlock(value: unknown): TemplateLayoutBlock | null {
  if (!isRecord(value) || !isPageTemplateBlockType(value.type)) return null;
  switch (value.type) {
    case 'spacer':
      return { type: value.type, heightCm: numberValue(value.heightCm, 0) };
    case 'text':
      return {
        type: value.type,
        value: stringValue(value.value),
        ...(typeof value.style === 'string' ? { style: value.style } : {}),
        ...(typeof value.alignment === 'string' ? { alignment: value.alignment } : {}),
        ...(isRecord(value.fontOverride) ? { fontOverride: value.fontOverride } : {}),
        ...(typeof value.spacingBeforePt === 'number' ? { spacingBeforePt: value.spacingBeforePt } : {}),
        ...(typeof value.spacingAfterPt === 'number' ? { spacingAfterPt: value.spacingAfterPt } : {})
      };
    case 'metadataField':
      return normalizeMetadataFieldBlock(value);
    case 'image':
      return {
        type: value.type,
        assetId: stringValue(value.assetId),
        ...(typeof value.widthCm === 'number' ? { widthCm: value.widthCm } : {}),
        ...(typeof value.heightCm === 'number' ? { heightCm: value.heightCm } : {}),
        ...(typeof value.alignment === 'string' ? { alignment: value.alignment } : {})
      };
    case 'fieldTable':
      return {
        type: value.type,
        ...(typeof value.columns === 'number' ? { columns: value.columns } : {}),
        rows: Array.isArray(value.rows)
          ? value.rows.map((row) =>
              Array.isArray(row)
                ? row.filter(isRecord).map(normalizeMetadataFieldBlock)
                : []
            )
          : [],
        ...(typeof value.borderMode === 'string' ? { borderMode: value.borderMode } : {}),
        ...(typeof value.labelColumnWidthCm === 'number' ? { labelColumnWidthCm: value.labelColumnWidthCm } : {}),
        ...(typeof value.valueColumnWidthCm === 'number' ? { valueColumnWidthCm: value.valueColumnWidthCm } : {})
      };
    case 'declarationText':
      return {
        type: value.type,
        paragraphs: Array.isArray(value.paragraphs)
          ? value.paragraphs.filter((item): item is string => typeof item === 'string')
          : [],
        ...(Array.isArray(value.signatureFields)
          ? { signatureFields: value.signatureFields.filter(isRecord).map(normalizeMetadataFieldBlock) }
          : {})
      };
    case 'pageBreak':
      return { type: value.type };
  }
}

function normalizeMetadataFieldBlock(value: Record<string, unknown>): TemplateMetadataFieldBlock {
  return {
    type: 'metadataField',
    label: stringValue(value.label),
    ...(typeof value.sourcePath === 'string' ? { sourcePath: value.sourcePath } : {}),
    ...(typeof value.variableName === 'string' ? { variableName: value.variableName } : {}),
    ...(typeof value.valueTemplate === 'string' ? { valueTemplate: value.valueTemplate } : {}),
    ...(typeof value.layout === 'string' ? { layout: value.layout } : {}),
    ...(typeof value.underline === 'boolean' ? { underline: value.underline } : {}),
    ...(typeof value.alignment === 'string' ? { alignment: value.alignment } : {})
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

function cleanPageTemplate(pageTemplate: PageTemplateDraft): PageTemplateDraft {
  return stripNulls({
    id: pageTemplate.id,
    targetSectionType: pageTemplate.targetSectionType,
    insertPosition: pageTemplate.insertPosition,
    pageSetupOverride: pageTemplate.pageSetupOverride,
    blocks: pageTemplate.blocks.map(cleanLayoutBlock)
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

function validateAssets(assets: TemplateAsset[], issues: ApiIssue[]): void {
  const seen = new Set<string>();
  assets.forEach((asset, index) => {
    if (!asset.id.trim()) issues.push(issue('template.asset.id.required', 'asset id 不能为空。', 'error', `$.assets[${index}].id`));
    if (seen.has(asset.id)) issues.push(issue('template.asset.duplicate', `asset ${asset.id} 重复。`, 'error', `$.assets[${index}].id`));
    seen.add(asset.id);
    if (!asset.path.trim()) issues.push(issue('template.asset.path.required', 'asset path 不能为空。', 'error', `$.assets[${index}].path`));
    if (!asset.contentType.trim()) issues.push(issue('template.asset.contentType.required', 'asset contentType 不能为空。', 'error', `$.assets[${index}].contentType`));
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
  const assets = new Set((template.assets ?? []).map((asset) => asset.id));
  const metadataPaths = new Set<string>(METADATA_SOURCE_PATHS);
  (template.pageTemplates ?? []).forEach((pageTemplate, templateIndex) => {
    const path = `$.pageTemplates[${templateIndex}]`;
    if (!pageTemplate.id.trim()) {
      issues.push(issue('template.pageTemplate.id.required', 'page template id 不能为空。', 'error', `${path}.id`));
    }
    if (!['cover', 'declaration', 'abstract', 'toc', 'body', 'appendix'].includes(pageTemplate.targetSectionType)) {
      issues.push(issue('template.pageTemplate.target.invalid', 'targetSectionType 不在 schema 枚举中。', 'error', `${path}.targetSectionType`));
    }
    if (!['beforeSection', 'afterSection', 'replaceSectionContent'].includes(pageTemplate.insertPosition)) {
      issues.push(issue('template.pageTemplate.insertPosition.invalid', 'insertPosition 不在 schema 枚举中。', 'error', `${path}.insertPosition`));
    }
    validatePageSetupOverride(pageTemplate.pageSetupOverride, `${path}.pageSetupOverride`, issues);
    pageTemplate.blocks?.forEach((block, blockIndex) => {
      validateLayoutBlock(block, `${path}.blocks[${blockIndex}]`, variables, assets, metadataPaths, issues);
    });
  });
}

function validateLayoutBlock(
  block: TemplateLayoutBlock,
  path: string,
  variables: Set<string>,
  assets: Set<string>,
  metadataPaths: Set<string>,
  issues: ApiIssue[]
): void {
  switch (block.type) {
    case 'spacer':
      if (!Number.isFinite(block.heightCm) || block.heightCm < 0 || block.heightCm > 20) {
        issues.push(issue('template.pageTemplate.spacer.height.invalid', 'spacer.heightCm 必须在 0 到 20 之间。', 'error', `${path}.heightCm`));
      }
      return;
    case 'text':
      if (!block.value.trim()) {
        issues.push(issue('template.pageTemplate.text.empty', '固定文本内容为空。', 'warning', `${path}.value`));
      }
      validateVariableReferences(block.value, `${path}.value`, variables, issues);
      validateFontOverride(block.fontOverride, `${path}.fontOverride`, issues);
      validateNonNegative(block.spacingBeforePt, `${path}.spacingBeforePt`, 'template.pageTemplate.spacing.invalid', issues);
      validateNonNegative(block.spacingAfterPt, `${path}.spacingAfterPt`, 'template.pageTemplate.spacing.invalid', issues);
      return;
    case 'metadataField':
      validateMetadataField(block, path, variables, metadataPaths, issues);
      return;
    case 'image':
      if (!block.assetId.trim()) {
        issues.push(issue('template.pageTemplate.image.asset.required', '图片元素缺少 assetId。', 'error', `${path}.assetId`));
      } else if (!assets.has(block.assetId)) {
        issues.push(issue('template.pageTemplate.image.asset.missing', `图片元素引用了不存在的 asset ${block.assetId}。`, 'error', `${path}.assetId`));
      }
      validatePositive(block.widthCm, `${path}.widthCm`, 'template.pageTemplate.image.size.invalid', issues);
      validatePositive(block.heightCm, `${path}.heightCm`, 'template.pageTemplate.image.size.invalid', issues);
      return;
    case 'fieldTable':
      if (block.columns !== undefined && (!Number.isInteger(block.columns) || block.columns < 1 || block.columns > 6)) {
        issues.push(issue('template.pageTemplate.fieldTable.columns.invalid', 'fieldTable.columns 必须是 1 到 6 的整数。', 'error', `${path}.columns`));
      }
      validatePositive(block.labelColumnWidthCm, `${path}.labelColumnWidthCm`, 'template.pageTemplate.fieldTable.width.invalid', issues);
      validatePositive(block.valueColumnWidthCm, `${path}.valueColumnWidthCm`, 'template.pageTemplate.fieldTable.width.invalid', issues);
      block.rows.forEach((row, rowIndex) => {
        row.forEach((field, fieldIndex) =>
          validateMetadataField(field, `${path}.rows[${rowIndex}][${fieldIndex}]`, variables, metadataPaths, issues)
        );
      });
      return;
    case 'declarationText':
      if (block.paragraphs.length === 0 || block.paragraphs.every((paragraph) => !paragraph.trim())) {
        issues.push(issue('template.pageTemplate.declaration.empty', 'declarationText 至少需要一段声明文本。', 'warning', `${path}.paragraphs`));
      }
      block.paragraphs.forEach((paragraph, index) => validateVariableReferences(paragraph, `${path}.paragraphs[${index}]`, variables, issues));
      block.signatureFields?.forEach((field, index) =>
        validateMetadataField(field, `${path}.signatureFields[${index}]`, variables, metadataPaths, issues)
      );
      return;
    case 'pageBreak':
      return;
  }
}

function validateMetadataField(
  block: TemplateMetadataFieldBlock,
  path: string,
  variables: Set<string>,
  metadataPaths: Set<string>,
  issues: ApiIssue[]
): void {
  if (!block.label.trim()) {
    issues.push(issue('template.pageTemplate.metadataField.label.required', 'metadataField.label 不能为空。', 'error', `${path}.label`));
  }
  if (block.variableName && !variables.has(block.variableName)) {
    issues.push(issue('template.pageTemplate.variable.missing', `页面模板引用了不存在的变量 ${block.variableName}。`, 'error', `${path}.variableName`));
  }
  if (block.sourcePath) {
    if (!/^(metadata|variables)\.[A-Za-z][A-Za-z0-9_.-]*$/.test(block.sourcePath)) {
      issues.push(issue('template.pageTemplate.sourcePath.invalid', 'sourcePath 必须以 metadata. 或 variables. 开头。', 'error', `${path}.sourcePath`));
    } else if (block.sourcePath.startsWith('metadata.') && !metadataPaths.has(block.sourcePath)) {
      issues.push(issue('template.pageTemplate.metadataField.unknown', `未知 metadata 字段 ${block.sourcePath}。`, 'warning', `${path}.sourcePath`));
    }
  }
  if (!block.sourcePath && !block.variableName && !block.valueTemplate) {
    issues.push(issue('template.pageTemplate.metadataField.binding.empty', 'metadataField 缺少 sourcePath、variableName 或 valueTemplate。', 'warning', path));
  }
  if (block.valueTemplate) validateVariableReferences(block.valueTemplate, `${path}.valueTemplate`, variables, issues);
}

function validateVariableReferences(
  value: string,
  path: string,
  variables: Set<string>,
  issues: ApiIssue[]
): void {
  for (const match of value.matchAll(/\{\{\s*variables\.([A-Za-z][A-Za-z0-9_.-]*)\s*\}\}/g)) {
    const name = match[1];
    if (!variables.has(name)) {
      issues.push(issue('template.pageTemplate.variable.missing', `页面模板引用了不存在的变量 ${name}。`, 'error', path));
    }
  }
}

function validateFontOverride(value: unknown, path: string, issues: ApiIssue[]): void {
  if (!value || typeof value !== 'object') return;
  const size = (value as { sizePt?: unknown }).sizePt;
  if (typeof size === 'number' && (size < 1 || size > 72)) {
    issues.push(issue('template.pageTemplate.fontSize.invalid', '元素字号必须在 1 到 72 pt 之间。', 'error', `${path}.sizePt`));
  }
}

function validatePageSetupOverride(value: unknown, path: string, issues: ApiIssue[]): void {
  if (!value || typeof value !== 'object') return;
  const page = value as Record<string, unknown>;
  for (const key of ['topMarginCm', 'bottomMarginCm', 'leftMarginCm', 'rightMarginCm', 'headerDistanceCm', 'footerDistanceCm'] as const) {
    const v = page[key];
    if (typeof v === 'number' && v < 0) {
      issues.push(issue('template.pageTemplate.pageSetup.negative', `${key} 不能为负数。`, 'error', `${path}.${key}`));
    }
  }
}

function validateNonNegative(
  value: number | undefined,
  path: string,
  code: string,
  issues: ApiIssue[]
): void {
  if (value !== undefined && (!Number.isFinite(value) || value < 0)) {
    issues.push(issue(code, '数值不能为负数。', 'error', path));
  }
}

function validatePositive(
  value: number | undefined,
  path: string,
  code: string,
  issues: ApiIssue[]
): void {
  if (value !== undefined && (!Number.isFinite(value) || value <= 0)) {
    issues.push(issue(code, '数值必须大于 0。', 'error', path));
  }
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

function numberValue(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
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
