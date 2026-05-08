import type { SectionKind } from '@/types';

export interface SectionMeta {
  kind: SectionKind;
  label: string;
  description: string;
  /** Whether this section should appear in the canonical order */
  order: number;
  /** Whether the section's content is mostly authored on the canvas */
  authored: boolean;
}

export const SECTION_META: Record<SectionKind, SectionMeta> = {
  cover: {
    kind: 'cover',
    label: '封面',
    description: '由模板自动渲染，使用元数据与模板变量。',
    order: 0,
    authored: false
  },
  originalityStatement: {
    kind: 'originalityStatement',
    label: '原创声明',
    description: '由模板提供文本，通常无需修改。',
    order: 1,
    authored: false
  },
  abstract: {
    kind: 'abstract',
    label: '摘要',
    description: '中文 / 英文摘要与关键词。',
    order: 2,
    authored: true
  },
  toc: {
    kind: 'toc',
    label: '目录',
    description: '由 Word 自动生成，无需手填。',
    order: 3,
    authored: false
  },
  body: {
    kind: 'body',
    label: '正文',
    description: '论文主体：章节、图、表、公式。',
    order: 4,
    authored: true
  },
  acknowledgements: {
    kind: 'acknowledgements',
    label: '致谢',
    description: '简短的致谢段落。',
    order: 5,
    authored: true
  },
  bibliography: {
    kind: 'bibliography',
    label: '参考文献',
    description: '在右栏管理条目；正文里通过 [@] 引用。',
    order: 6,
    authored: true
  },
  appendix: {
    kind: 'appendix',
    label: '附录',
    description: '附加材料、代码清单等。',
    order: 7,
    authored: true
  }
};

export const SECTION_ORDER: SectionKind[] = (
  Object.values(SECTION_META) as SectionMeta[]
)
  .slice()
  .sort((a, b) => a.order - b.order)
  .map((m) => m.kind);
