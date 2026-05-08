import type { SectionKind, TextAlignment } from '@/types';

export type PageNumberStyle = 'none' | 'decimal' | 'lowerRoman' | 'upperRoman';

export type HeadingLevel = '1' | '2' | '3' | '4' | '5' | '6';

export interface FontOverride {
  eastAsia?: string;
  latin?: string;
  sizePt?: number;
  bold?: boolean;
  italic?: boolean;
}

export interface ParagraphOverride {
  lineSpacingMultiple?: number;
  spaceBeforePt?: number;
  spaceAfterPt?: number;
  firstLineIndentChars?: number;
  hangingIndentCm?: number;
  alignment?: TextAlignment;
  widowControl?: boolean;
}

export interface HeadingOverride {
  font?: FontOverride;
  spaceBeforePt?: number;
  spaceAfterPt?: number;
  numbered?: boolean;
  pageBreakBefore?: boolean;
  outlineLevel?: number;
  alignment?: TextAlignment;
}

/**
 * Architecture-aligned bucket of sections. The renderer maps the 8 section
 * kinds into these three buckets and applies a single page-setup / page-numbering
 * profile to each. See ThesisFormatSpec.sections in the format spec schema.
 */
export type SectionBucket = 'cover' | 'frontMatter' | 'body';

export const SECTION_BUCKET_FOR_KIND: Record<SectionKind, SectionBucket> = {
  cover: 'cover',
  originalityStatement: 'frontMatter',
  abstract: 'frontMatter',
  toc: 'frontMatter',
  body: 'body',
  acknowledgements: 'body',
  bibliography: 'body',
  appendix: 'body'
};

export const SECTION_BUCKET_LABEL: Record<SectionBucket, string> = {
  cover: '封面',
  frontMatter: '前置页（原创声明 / 摘要 / 目录）',
  body: '正文（正文 / 致谢 / 参考文献 / 附录）'
};

export interface SectionFormatOverride {
  pageNumberStyle?: PageNumberStyle;
  startPageNumber?: number;
  restartPageNumbering?: boolean;
  includeHeader?: boolean;
  includeFooter?: boolean;
}

export interface SectionInstanceOverride extends SectionFormatOverride {
  /** 仅这一节使用的页眉文本（渲染器需为该 section 单独发 header part） */
  headerText?: string;
  /** 仅这一节使用的页脚文本 */
  footerText?: string;
  /** 仅这一节内段落的覆盖 */
  paragraph?: ParagraphOverride;
  /** 仅这一节内默认字体的覆盖 */
  defaultFont?: FontOverride;
}

export interface DocumentOverrides {
  toc?: {
    minLevel?: number;
    maxLevel?: number;
    title?: string;
    /**
     * 显示在目录里的章节范围。
     *  - undefined / 不存在 = 全部章节
     *  - [] = 不引用任何章节（用户显式清空时；UI 层一般不应允许这种状态）
     *  - 否则按 section.id 过滤
     */
    includeSectionIds?: string[];
  };
  headerFooter?: {
    headerText?: string;
    drawHeaderLine?: boolean;
    hidePageNumberOnCover?: boolean;
    differentFirstPage?: boolean;
  };
  defaultFont?: FontOverride;
  bodyParagraph?: ParagraphOverride;
  headings?: Partial<Record<HeadingLevel, HeadingOverride>>;
  sectionFormats?: Partial<Record<SectionBucket, SectionFormatOverride>>;
  /** 按 section.id 索引；优先级高于同 bucket 的 sectionFormats */
  sectionInstances?: Record<string, SectionInstanceOverride>;
}

const STORAGE_PREFIX = 'thesisforma.overrides.v1:';

function safeStorage(): Storage | null {
  try {
    return typeof window !== 'undefined' ? window.localStorage : null;
  } catch {
    return null;
  }
}

export function loadOverrides(documentId: string): DocumentOverrides {
  const raw = safeStorage()?.getItem(STORAGE_PREFIX + documentId);
  if (!raw) return {};
  try {
    const parsed = JSON.parse(raw);
    return isPlainObject(parsed) ? (parsed as DocumentOverrides) : {};
  } catch {
    return {};
  }
}

export function saveOverrides(documentId: string, overrides: DocumentOverrides): void {
  const storage = safeStorage();
  if (!storage) return;
  const cleaned = stripEmpty(overrides);
  if (cleaned === undefined) {
    storage.removeItem(STORAGE_PREFIX + documentId);
  } else {
    storage.setItem(STORAGE_PREFIX + documentId, JSON.stringify(cleaned));
  }
}

export function clearOverrides(documentId: string): void {
  safeStorage()?.removeItem(STORAGE_PREFIX + documentId);
}

function isPlainObject(v: unknown): v is Record<string, unknown> {
  return typeof v === 'object' && v !== null && !Array.isArray(v);
}

/** Drop undefined / empty objects so localStorage stays compact and "no override" is truly empty. */
function stripEmpty<T>(value: T): T | undefined {
  if (Array.isArray(value)) {
    return value.length > 0 ? value : undefined;
  }
  if (isPlainObject(value)) {
    const result: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value)) {
      const cleaned = stripEmpty(v);
      if (cleaned !== undefined) result[k] = cleaned;
    }
    return Object.keys(result).length > 0 ? (result as T) : undefined;
  }
  if (value === '' || value === undefined || value === null) return undefined;
  return value;
}
