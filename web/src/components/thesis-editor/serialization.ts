import type { BibliographyEntry, BlockNode, InlineNode, SectionKind, SectionNode, ThesisEditorState, ValidationIssue } from './types';

const sectionKindMap: Record<SectionKind, string> = {
  cover: 'cover',
  originalityStatement: 'originalityStatement',
  abstract: 'abstract',
  toc: 'toc',
  body: 'body',
  acknowledgements: 'acknowledgements',
  bibliography: 'bibliography',
  appendix: 'appendix'
};

export function newId(prefix: string) {
  return `${prefix}-${Math.random().toString(36).slice(2, 10)}`;
}

export function createInitialState(templateId = 'example-university-engineering'): ThesisEditorState {
  return {
    templateId,
    metadata: {
      title: '',
      subtitle: '',
      author: '',
      college: '',
      major: '',
      studentId: '',
      advisor: '',
      date: ''
    },
    sections: [
      { id: 'cover', kind: 'cover', title: '封面', blocks: [] },
      { id: 'declaration', kind: 'originalityStatement', title: '声明', blocks: [] },
      { id: 'abstract-cn', kind: 'abstract', title: '中文摘要', blocks: [{ type: 'abstract', id: 'abstract-zh', language: 'zh', text: '', keywords: [] }] },
      { id: 'toc', kind: 'toc', title: '目录', blocks: [] },
      { id: 'body', kind: 'body', title: '正文', blocks: [{ type: 'heading', id: 'heading-1', level: 1, text: '绪论', bookmarkName: 'heading-1', numbered: true }] },
      { id: 'ack', kind: 'acknowledgements', title: '致谢', blocks: [] },
      { id: 'refs', kind: 'bibliography', title: '参考文献', blocks: [] },
      { id: 'appendix', kind: 'appendix', title: '附录', blocks: [] }
    ],
    bibliography: [],
    assets: [],
    validationIssues: [],
    autosaveStatus: 'unsaved'
  };
}

export function serializeToThesisDocument(state: ThesisEditorState) {
  return {
    schemaVersion: '1.1.0',
    metadata: {
      title: state.metadata.title,
      subtitle: state.metadata.subtitle || undefined,
      author: state.metadata.author,
      college: state.metadata.college,
      major: state.metadata.major,
      studentId: state.metadata.studentId,
      advisor: state.metadata.advisor,
      date: state.metadata.date,
      language: 'zh-CN'
    },
    sections: state.sections.map(section => serializeSection(section, state.bibliography))
  };
}

export function deserializeFromThesisDocument(document: any, templateId = 'example-university-engineering'): ThesisEditorState {
  const initial = createInitialState(templateId);
  const sections = Array.isArray(document?.sections)
    ? document.sections.map((section: any) => ({
      id: String(section.id ?? newId('section')),
      kind: reverseSectionKind(String(section.kind ?? 'body')),
      title: String(section.title ?? section.kind ?? 'Section'),
      blocks: Array.isArray(section.blocks) ? section.blocks.flatMap(deserializeBlock) : []
    }))
    : initial.sections;

  const bibliographySection = document?.sections?.find((section: any) => section.kind === 'bibliography');
  const bibliographyBlock = bibliographySection?.blocks?.find((block: any) => block.type === 'bibliography');

  return {
    ...initial,
    metadata: {
      title: document?.metadata?.title ?? '',
      subtitle: document?.metadata?.subtitle ?? '',
      author: document?.metadata?.author ?? '',
      college: document?.metadata?.college ?? '',
      major: document?.metadata?.major ?? '',
      studentId: document?.metadata?.studentId ?? '',
      advisor: document?.metadata?.advisor ?? '',
      date: document?.metadata?.date ?? ''
    },
    sections,
    bibliography: Array.isArray(bibliographyBlock?.entries)
      ? bibliographyBlock.entries.map((entry: any) => ({ id: String(entry.id ?? entry.key ?? newId('ref')), key: String(entry.id ?? entry.key), text: String(entry.text ?? ''), entryType: 'other' as const }))
      : []
  };
}

function serializeSection(section: SectionNode, bibliography: BibliographyEntry[]) {
  const blocks = section.kind === 'bibliography'
    ? [{ type: 'bibliography', id: 'bibliography', entries: bibliography.map(entry => ({ id: entry.key, text: entry.text })) }]
    : section.blocks.flatMap(serializeBlock);

  return {
    id: section.id,
    kind: sectionKindMap[section.kind],
    title: section.title,
    blocks
  };
}

function reverseSectionKind(kind: string): SectionKind {
  return (Object.entries(sectionKindMap).find(([, value]) => value === kind)?.[0] as SectionKind | undefined) ?? 'body';
}

function deserializeBlock(block: any): BlockNode[] {
  switch (block?.type) {
    case 'heading':
      return [{ type: 'heading', id: String(block.id ?? newId('heading')), level: Number(block.level ?? 1), text: String(block.text ?? ''), bookmarkName: block.bookmarkId ?? block.bookmarkName, numbered: true }];
    case 'paragraph':
      return [{ type: 'paragraph', id: String(block.id ?? newId('paragraph')), inlines: Array.isArray(block.inlines) ? block.inlines.map(deserializeInline) : [{ type: 'text', text: block.text ?? '' }] }];
    case 'table':
      return [{
        type: 'table',
        id: String(block.id ?? newId('table')),
        caption: String(block.caption ?? ''),
        style: block.style === 'threeLine' ? 'threeLine' : 'normal',
        bookmarkId: block.bookmarkId,
        repeatHeaderRows: block.repeatHeaderRows,
        rows: Array.isArray(block.rows) ? block.rows.map((row: any) => ({
          id: String(row.id ?? newId('row')),
          isHeader: Boolean(row.isHeader),
          cells: Array.isArray(row.cells) ? row.cells.map((cell: any) => ({ id: String(cell.id ?? newId('cell')), text: String(cell.text ?? '') })) : []
        })) : []
      }];
    case 'figure':
      return [{ type: 'figure', id: String(block.id ?? newId('figure')), caption: String(block.caption ?? ''), altText: String(block.altText ?? block.caption ?? ''), imagePath: block.imagePath, imageContentType: block.contentType, widthCm: block.widthCm ?? 8 }];
    case 'equation':
      return [{ type: 'equation', id: String(block.id ?? newId('equation')), plainText: String(block.plainText ?? block.latex ?? ''), caption: block.caption, bookmarkName: block.bookmarkId }];
    case 'pageBreak':
      return [{ type: 'pageBreak', id: String(block.id ?? newId('page-break')) }];
    case 'bibliography':
      return [];
    default:
      return [];
  }
}

function deserializeInline(inline: any): InlineNode {
  if (inline?.type === 'citation') {
    return { type: 'citation', targetId: String(inline.targetId ?? inline.key ?? ''), displayText: String(inline.displayText ?? `[${inline.targetId ?? ''}]`) };
  }
  if (inline?.type === 'reference') {
    return { type: 'reference', bookmarkName: String(inline.bookmarkName ?? inline.targetId ?? ''), fallbackText: inline.fallbackText };
  }
  if (inline?.type === 'footnote') {
    return { type: 'footnote', noteId: String(inline.noteId ?? newId('fn')), inlines: [{ type: 'text', text: String(inline.text ?? '') }] };
  }
  return { type: 'text', text: String(inline?.text ?? '') };
}

function serializeBlock(block: BlockNode): any[] {
  if (block.type === 'abstract') {
    return [
      {
        type: 'paragraph',
        id: block.id,
        inlines: [{ type: 'text', text: block.text }]
      },
      {
        type: 'paragraph',
        id: `${block.id}-keywords`,
        inlines: [{ type: 'text', text: `${block.language === 'zh' ? '关键词' : 'Key words'}：${block.keywords.join(block.language === 'zh' ? '；' : '; ')}` }]
      }
    ];
  }

  if (block.type === 'heading') {
    return [{
      type: 'heading',
      id: block.id,
      level: block.level,
      bookmarkName: block.bookmarkName ?? block.id,
      numbered: block.numbered ?? true,
      inlines: [{ type: 'text', text: block.text }]
    }];
  }

  if (block.type === 'paragraph') {
    return [{ type: 'paragraph', id: block.id, inlines: block.inlines }];
  }

  if (block.type === 'table') {
    return [{
      type: 'table',
      id: block.id,
      bookmarkId: block.bookmarkId ?? block.id,
      caption: block.caption,
      style: block.style,
      repeatHeaderRows: block.repeatHeaderRows,
      rows: block.rows.map(row => ({
        id: row.id,
        isHeader: row.isHeader,
        cells: row.cells.map(cell => ({ id: cell.id, text: cell.text, gridSpan: cell.gridSpan ?? 1 }))
      }))
    }];
  }

  if (block.type === 'figure') {
    return [{
      type: 'figure',
      id: block.id,
      caption: block.caption,
      imagePath: block.imagePath,
      imageContentType: block.imageContentType ?? 'image/png',
      widthCm: block.widthCm ?? 8
    }];
  }

  if (block.type === 'equation') {
    return [{
      type: 'equation',
      id: block.id,
      sourceType: 'plain',
      plainText: block.plainText,
      caption: block.caption,
      bookmarkName: block.bookmarkName ?? block.id,
      display: true,
      alignment: 'center'
    }];
  }

  return [{ type: 'pageBreak', id: block.id }];
}

export function validateEditorState(state: ThesisEditorState): ValidationIssue[] {
  const issues: ValidationIssue[] = [];
  if (!state.metadata.title.trim()) {
    issues.push({ code: 'metadata.title.required', severity: 'error', message: '论文题目不能为空。', suggestedAction: '在元信息表单中填写论文题目。' });
  }
  if (!state.metadata.author.trim()) {
    issues.push({ code: 'metadata.author.required', severity: 'warning', message: '作者姓名尚未填写。', suggestedAction: '补充作者信息。' });
  }

  const headings = state.sections.flatMap(section => section.blocks.filter((block): block is Extract<BlockNode, { type: 'heading' }> => block.type === 'heading'));
  let previous = 0;
  for (const heading of headings) {
    if (heading.level > previous + 1) {
      issues.push({ code: 'heading.levelJump', severity: 'error', message: `标题层级从 ${previous} 跳到 ${heading.level}。`, blockId: heading.id, suggestedAction: '调整标题级别，避免从一级直接跳到三级。' });
    }
    previous = heading.level;
  }

  const bibliographyKeys = new Set(state.bibliography.map(entry => entry.key));
  for (const block of state.sections.flatMap(section => section.blocks)) {
    if (block.type === 'table' && !block.caption.trim()) {
      issues.push({ code: 'table.caption.required', severity: 'error', message: '表格缺少表名。', blockId: block.id, suggestedAction: '填写表名，格式由模板控制。' });
    }
    if (block.type === 'figure' && !block.caption.trim()) {
      issues.push({ code: 'figure.caption.required', severity: 'error', message: '图片缺少图名。', blockId: block.id, suggestedAction: '填写图名或图注。' });
    }
    if (block.type === 'paragraph') {
      validateInlines(block.inlines, bibliographyKeys, block.id, issues);
    }
  }

  return issues;
}

function validateInlines(inlines: InlineNode[], bibliographyKeys: Set<string>, blockId: string, issues: ValidationIssue[]) {
  for (const inline of inlines) {
    if (inline.type === 'citation' && !bibliographyKeys.has(inline.targetId)) {
      issues.push({ code: 'citation.targetMissing', severity: 'error', message: `引用目标 ${inline.targetId} 不存在。`, blockId, suggestedAction: '在参考文献管理中添加对应 key，或更换引用目标。' });
    }
  }
}

export function collectReferenceTargets(state: ThesisEditorState) {
  return state.sections.flatMap(section => section.blocks).flatMap(block => {
    if (block.type === 'heading') return [{ id: block.bookmarkName ?? block.id, label: block.text, type: 'heading' }];
    if (block.type === 'table') return [{ id: block.bookmarkId ?? block.id, label: block.caption || '未命名表格', type: 'table' }];
    if (block.type === 'figure') return [{ id: block.id, label: block.caption || '未命名图片', type: 'figure' }];
    if (block.type === 'equation') return [{ id: block.bookmarkName ?? block.id, label: block.caption || block.plainText, type: 'equation' }];
    return [];
  });
}

export function createTableBlock(rows = 3, columns = 3, caption = '表名待填写'): Extract<BlockNode, { type: 'table' }> {
  const id = newId('table');
  return {
    type: 'table',
    id,
    bookmarkId: id,
    caption,
    style: 'threeLine',
    repeatHeaderRows: 1,
    rows: Array.from({ length: rows }, (_, rowIndex) => ({
      id: newId('row'),
      isHeader: rowIndex === 0,
      cells: Array.from({ length: columns }, () => ({ id: newId('cell'), text: '' }))
    }))
  };
}
