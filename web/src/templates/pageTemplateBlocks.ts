import type {
  TemplateAsset,
  TemplateDeclarationTextBlock,
  TemplateFieldTableBlock,
  TemplateFontOverride,
  TemplateImageBlock,
  TemplateLayoutBlock,
  TemplateMetadataFieldBlock,
  TemplatePageBreakBlock,
  TemplateSpacerBlock,
  TemplateTextBlock
} from '@/types';

export const PAGE_TEMPLATE_BLOCK_TYPES = [
  'spacer',
  'text',
  'metadataField',
  'image',
  'fieldTable',
  'declarationText',
  'pageBreak'
] as const;

export type PageTemplateBlockType = (typeof PAGE_TEMPLATE_BLOCK_TYPES)[number];

export const METADATA_SOURCE_PATHS = [
  'metadata.title',
  'metadata.subtitle',
  'metadata.author',
  'metadata.college',
  'metadata.major',
  'metadata.studentId',
  'metadata.advisor',
  'metadata.date',
  'metadata.language'
] as const;

export function createLayoutBlock(type: PageTemplateBlockType): TemplateLayoutBlock {
  switch (type) {
    case 'spacer':
      return { type, heightCm: 0.8 };
    case 'text':
      return { type, value: '{{metadata.title}}', alignment: 'center' };
    case 'metadataField':
      return { type, label: '字段', sourcePath: 'metadata.title', layout: 'labelValueLine' };
    case 'image':
      return { type, assetId: 'collegeLogo', widthCm: 2.2, heightCm: 2.2, alignment: 'center' };
    case 'fieldTable':
      return {
        type,
        columns: 2,
        borderMode: 'bottomLine',
        rows: [[{ type: 'metadataField', label: '论文题目', sourcePath: 'metadata.title', layout: 'tableRow' }]]
      };
    case 'declarationText':
      return { type, paragraphs: ['声明正文'], signatureFields: [] };
    case 'pageBreak':
      return { type };
  }
}

export function cloneLayoutBlock(block: TemplateLayoutBlock): TemplateLayoutBlock {
  return structuredClone(block);
}

export function cleanLayoutBlock(block: TemplateLayoutBlock): TemplateLayoutBlock {
  switch (block.type) {
    case 'spacer':
      return stripNulls({ type: block.type, heightCm: block.heightCm }) as TemplateSpacerBlock;
    case 'text':
      return stripNulls({
        type: block.type,
        value: block.value,
        style: block.style,
        alignment: block.alignment,
        fontOverride: cleanFontOverride(block.fontOverride),
        spacingBeforePt: block.spacingBeforePt,
        spacingAfterPt: block.spacingAfterPt
      }) as TemplateTextBlock;
    case 'metadataField':
      return cleanMetadataFieldBlock(block);
    case 'image':
      return stripNulls({
        type: block.type,
        assetId: block.assetId,
        widthCm: block.widthCm,
        heightCm: block.heightCm,
        alignment: block.alignment
      }) as TemplateImageBlock;
    case 'fieldTable':
      return stripNulls({
        type: block.type,
        columns: block.columns,
        rows: block.rows.map((row) => row.map(cleanMetadataFieldBlock)),
        borderMode: block.borderMode,
        labelColumnWidthCm: block.labelColumnWidthCm,
        valueColumnWidthCm: block.valueColumnWidthCm
      }) as TemplateFieldTableBlock;
    case 'declarationText':
      return stripNulls({
        type: block.type,
        paragraphs: block.paragraphs,
        signatureFields: block.signatureFields?.map(cleanMetadataFieldBlock)
      }) as TemplateDeclarationTextBlock;
    case 'pageBreak':
      return { type: block.type } as TemplatePageBreakBlock;
  }
}

export function cleanMetadataFieldBlock(block: TemplateMetadataFieldBlock): TemplateMetadataFieldBlock {
  return stripNulls({
    type: block.type,
    label: block.label,
    sourcePath: block.sourcePath,
    variableName: block.variableName,
    valueTemplate: block.valueTemplate,
    layout: block.layout,
    underline: block.underline,
    alignment: block.alignment
  }) as TemplateMetadataFieldBlock;
}

export function cleanTemplateAsset(asset: TemplateAsset): TemplateAsset {
  return stripNulls({
    id: asset.id,
    type: asset.type,
    path: asset.path,
    contentType: asset.contentType,
    description: asset.description,
    required: asset.required
  }) as TemplateAsset;
}

export function layoutBlockSummary(block: TemplateLayoutBlock): string {
  switch (block.type) {
    case 'spacer':
      return `spacer · ${block.heightCm ?? 0} cm`;
    case 'text':
      return block.value?.trim() ? `text · ${block.value.trim().slice(0, 48)}` : 'text · 空文本';
    case 'metadataField':
      return `metadataField · ${metadataFieldTarget(block) || block.label || '未绑定'}`;
    case 'image':
      return `image · ${block.assetId || '未绑定 asset'}`;
    case 'fieldTable':
      return `fieldTable · ${block.rows.length} 行 / ${block.columns ?? 1} 列`;
    case 'declarationText':
      return `declarationText · ${block.paragraphs.length} 段`;
    case 'pageBreak':
      return 'pageBreak';
  }
}

export function layoutBlockPreview(block: TemplateLayoutBlock, variableDefaults: Record<string, string>): string {
  switch (block.type) {
    case 'spacer':
      return `空白 ${block.heightCm ?? 0} cm`;
    case 'text':
      return interpolatePreview(block.value, variableDefaults);
    case 'metadataField':
      return `${block.label || '字段'}：${metadataFieldTarget(block) || '未绑定'}`;
    case 'image':
      return `[图片：${block.assetId || '未绑定 asset'}]`;
    case 'fieldTable':
      return block.rows
        .map((row) => row.map((field) => `${field.label || '字段'}=${metadataFieldTarget(field) || '未绑定'}`).join(' / '))
        .join('\n');
    case 'declarationText':
      return block.paragraphs.join('\n');
    case 'pageBreak':
      return '[分页]';
  }
}

export function metadataFieldTarget(block: TemplateMetadataFieldBlock): string {
  if (block.sourcePath) return block.sourcePath;
  if (block.variableName) return `variables.${block.variableName}`;
  if (block.valueTemplate) return block.valueTemplate;
  return '';
}

export function isPageTemplateBlockType(value: unknown): value is PageTemplateBlockType {
  return PAGE_TEMPLATE_BLOCK_TYPES.includes(value as PageTemplateBlockType);
}

function cleanFontOverride(value: TemplateFontOverride | undefined): TemplateFontOverride | undefined {
  if (!value) return undefined;
  return stripNulls({
    eastAsia: value.eastAsia,
    latin: value.latin,
    sizePt: value.sizePt,
    bold: value.bold,
    italic: value.italic
  }) as TemplateFontOverride;
}

function interpolatePreview(value: string, variableDefaults: Record<string, string>): string {
  return value.replace(/\{\{\s*variables\.([A-Za-z][A-Za-z0-9_.-]*)\s*\}\}/g, (_, key: string) => {
    return variableDefaults[key] || `{{variables.${key}}}`;
  });
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
