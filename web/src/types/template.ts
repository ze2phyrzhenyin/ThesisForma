import type {
  TemplatePackage as GeneratedTemplatePackage,
  ThesisFormatSpec as GeneratedThesisFormatSpec
} from './generated';

/**
 * Editor adapter types for TemplatePackage / ThesisFormatSpec.
 * Generated schema types remain available for drift checks and API contracts.
 */

export type TemplatePackageSchema = GeneratedTemplatePackage;
export type ThesisFormatSpecSchema = GeneratedThesisFormatSpec;

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

export interface TemplatePackage {
  templateSchemaVersion: string;
  id: string;
  name: string;
  version: string;
  locale: string;
  description?: string;
  school?: string;
  college?: string;
  degreeType?: string;
  tags?: string[];
  extends?: {
    templateId: string;
    versionRange?: string;
  };
  formatSpec?: ThesisFormatSpecDraft;
  formatSpecRef?: string;
  variables?: TemplateVariable[];
  assets?: TemplateAsset[];
  pageTemplates?: PageTemplateDraft[];
  complianceRules?: unknown[];
  notes?: string[];
}

export interface TemplateAsset {
  id: string;
  type: 'image' | 'font' | 'staticDocxFragment' | 'text' | string;
  path: string;
  contentType: string;
  description?: string;
  required?: boolean;
}

export interface ThesisFormatSpecDraft {
  schemaVersion?: '1.0.0' | '1.1.0' | '1.2.0' | string;
  name?: string;
  pageSetup?: {
    paperSize?: 'a4' | 'letter' | string;
    orientation?: 'portrait' | 'landscape' | string;
    topMarginCm?: number;
    bottomMarginCm?: number;
    leftMarginCm?: number;
    rightMarginCm?: number;
    gutterCm?: number;
    headerDistanceCm?: number;
    footerDistanceCm?: number;
    columns?: number;
  };
  defaultFont?: {
    eastAsia?: string;
    latin?: string;
    sizePt?: number;
    bold?: boolean;
    italic?: boolean;
  };
  bodyParagraph?: {
    lineSpacingMultiple?: number;
    lineSpacingExactPt?: number | null;
    spaceBeforePt?: number;
    spaceAfterPt?: number;
    firstLineIndentChars?: number;
    hangingIndentCm?: number;
    alignment?: 'left' | 'center' | 'right' | 'both' | string;
    widowControl?: boolean;
  };
  headings?: Record<string, unknown>;
  headerFooter?: unknown;
  toc?: unknown;
  tables?: {
    widthPercent?: number;
    cellMarginCm?: number;
    useThreeLineTables?: boolean;
    captionPosition?: 'above' | 'below' | string;
    defaultAlignment?: 'left' | 'center' | 'right' | 'both' | string;
    repeatHeaderRowsDefault?: number;
    allowRowBreakAcrossPagesDefault?: boolean;
  };
  figures?: unknown;
  captions?: unknown;
  bibliography?: unknown;
  numbering?: unknown;
  compatibility?: unknown;
  sections?: unknown;
  coverPage?: unknown;
  [key: string]: unknown;
}

export interface PageTemplateDraft {
  id: string;
  targetSectionType: 'cover' | 'declaration' | 'abstract' | 'toc' | 'body' | 'appendix' | string;
  insertPosition: 'beforeSection' | 'afterSection' | 'replaceSectionContent' | string;
  pageSetupOverride?: unknown;
  blocks: TemplateLayoutBlock[];
}

export interface TemplateFontOverride {
  eastAsia?: string;
  latin?: string;
  sizePt?: number;
  bold?: boolean;
  italic?: boolean;
}

export interface TemplateSpacerBlock {
  type: 'spacer';
  heightCm: number;
}

export interface TemplateTextBlock {
  type: 'text';
  value: string;
  style?: string;
  alignment?: 'left' | 'center' | 'right' | 'both' | string;
  fontOverride?: TemplateFontOverride;
  spacingBeforePt?: number;
  spacingAfterPt?: number;
}

export interface TemplateMetadataFieldBlock {
  type: 'metadataField';
  label: string;
  sourcePath?: string;
  variableName?: string;
  valueTemplate?: string;
  layout?: 'inline' | 'labelValueLine' | 'tableRow' | string;
  underline?: boolean;
  alignment?: 'left' | 'center' | 'right' | 'both' | string;
}

export interface TemplateImageBlock {
  type: 'image';
  assetId: string;
  widthCm?: number;
  heightCm?: number;
  alignment?: 'left' | 'center' | 'right' | 'both' | string;
}

export interface TemplateFieldTableBlock {
  type: 'fieldTable';
  columns?: number;
  rows: TemplateMetadataFieldBlock[][];
  borderMode?: 'none' | 'full' | 'bottomLine' | 'custom' | string;
  labelColumnWidthCm?: number;
  valueColumnWidthCm?: number;
}

export interface TemplateDeclarationTextBlock {
  type: 'declarationText';
  paragraphs: string[];
  signatureFields?: TemplateMetadataFieldBlock[];
}

export interface TemplatePageBreakBlock {
  type: 'pageBreak';
}

export interface TemplateRuleBlock {
  type: 'rule';
  thicknessPt?: number;
  color?: string;
  alignment?: 'left' | 'center' | 'right' | 'both' | string;
  spacingBeforePt?: number;
  spacingAfterPt?: number;
}

export type TemplateLayoutBlock =
  | TemplateSpacerBlock
  | TemplateTextBlock
  | TemplateMetadataFieldBlock
  | TemplateImageBlock
  | TemplateFieldTableBlock
  | TemplateDeclarationTextBlock
  | TemplatePageBreakBlock
  | TemplateRuleBlock;
