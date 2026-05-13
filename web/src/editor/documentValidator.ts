import type { ApiIssue, Block, ThesisDocument } from '@/types';
import { issue, SECTION_KINDS, SUPPORTED_SCHEMA_VERSIONS } from './documentContractUtils';
import { inlinesPlainText, walkBlocks, walkInlines } from './documentWalker';

export function validateThesisDocument(document: ThesisDocument): ApiIssue[] {
  const issues: ApiIssue[] = [];
  if (!SUPPORTED_SCHEMA_VERSIONS.has(document.schemaVersion)) {
    issues.push(
      issue(
        'document.schemaVersion.unsupported',
        `暂不支持 schemaVersion=${document.schemaVersion}。`,
        'error',
        '$.schemaVersion'
      )
    );
  }
  validateMetadata(document, issues);
  validateSections(document, issues);
  validateBibliographyCitations(document, issues);
  validateReferences(document, issues);
  validateNotes(document, issues);
  return issues;
}

function validateMetadata(document: ThesisDocument, issues: ApiIssue[]) {
  const required = ['title', 'author', 'college', 'major', 'studentId', 'advisor', 'date', 'language'] as const;
  for (const key of required) {
    if (!document.metadata[key]?.trim()) {
      issues.push(issue('metadata.required', `${key} 是必填字段。`, 'error', `$.metadata.${key}`));
    }
  }
  if (document.metadata.language && !/^[a-z]{2}(-[A-Z]{2})?$/.test(document.metadata.language)) {
    issues.push(
      issue('metadata.language.invalid', 'language 应形如 zh-CN 或 en。', 'error', '$.metadata.language')
    );
  }
}

function validateSections(document: ThesisDocument, issues: ApiIssue[]) {
  if (!document.sections.length) {
    issues.push(issue('sections.empty', '至少需要一个 section。', 'error', '$.sections'));
  }
  document.sections.forEach((section, sectionIndex) => {
    if (!SECTION_KINDS.has(section.kind)) {
      issues.push(issue('section.kind.invalid', 'section.kind 不受支持。', 'error', `$.sections[${sectionIndex}].kind`));
    }
    section.blocks.forEach((block, blockIndex) => {
      validateBlock(block, `$.sections[${sectionIndex}].blocks[${blockIndex}]`, issues);
    });
  });
}

function validateBlock(block: Block, path: string, issues: ApiIssue[]) {
  if ((block.type === 'paragraph' || block.type === 'heading' || block.type === 'quote') && block.inlines.length === 0) {
    issues.push(issue('block.emptyText', '文本块为空。', 'warning', `${path}.inlines`));
  }
  if (block.type === 'figure') {
    if (!block.caption.trim()) issues.push(issue('figure.caption.empty', '图片题注为空。', 'warning', `${path}.caption`));
    if (!block.imagePath && !block.imageDataBase64) {
      issues.push(issue('figure.image.missing', '图片缺少 imagePath 或 imageDataBase64。', 'warning', path));
    }
  }
  if (block.type === 'equation') {
    const hasSource = Boolean(block.omml || block.latex || block.plainText || block.placeholder);
    if (!hasSource) issues.push(issue('equation.source.empty', '公式内容为空。', 'warning', path));
  }
  if (block.type === 'bibliography' && block.entries.length === 0) {
    issues.push(issue('bibliography.empty', '参考文献块没有条目。', 'warning', `${path}.entries`));
  }
  if (block.type === 'table') validateTableBlock(block, path, issues);
  if ((block.type === 'footnote' || block.type === 'endnote') && block.inlines.length === 0) {
    issues.push(issue('note.empty', '注释内容为空。', 'error', `${path}.inlines`));
  }
}

function validateTableBlock(block: Extract<Block, { type: 'table' }>, path: string, issues: ApiIssue[]) {
  if (!block.caption.trim()) issues.push(issue('table.caption.empty', '表格题注为空。', 'warning', `${path}.caption`));
  let expected = -1;
  const active = new Set<number>();
  block.rows.forEach((row, rowIndex) => {
    let col = 0;
    const next = new Set<number>();
    row.cells.forEach((cell, cellIndex) => {
      const span = cell.gridSpan ?? 1;
      if (span < 1) {
        issues.push(issue('table.gridSpan.invalid', 'gridSpan 必须大于等于 1。', 'error', `${path}.rows[${rowIndex}].cells[${cellIndex}].gridSpan`));
      }
      for (let i = col; i < col + Math.max(1, span); i++) {
        if (cell.verticalMerge === 'continue' && !active.has(i)) {
          issues.push(issue('table.verticalMerge.invalidChain', 'vMerge continue 上方必须有 restart 或 continue。', 'error', `${path}.rows[${rowIndex}].cells[${cellIndex}].verticalMerge`));
        }
        if (cell.verticalMerge === 'restart' || cell.verticalMerge === 'continue') next.add(i);
      }
      col += Math.max(1, span);
    });
    if (expected < 0) expected = col;
    else if (col !== expected) {
      issues.push(issue('table.grid.inconsistent', `第 ${rowIndex + 1} 行逻辑列数 ${col} 与首行 ${expected} 不一致。`, 'error', `${path}.rows[${rowIndex}]`));
    }
    active.clear();
    next.forEach((v) => active.add(v));
  });
}

function validateBibliographyCitations(document: ThesisDocument, issues: ApiIssue[]) {
  const refs = new Set<string>();
  walkBlocks(document, (block) => {
    if (block.type === 'bibliography') {
      for (const entry of block.entries) {
        if (!entry.id.trim()) issues.push(issue('bibliography.id.empty', '参考文献 id 为空。', 'error'));
        if (!entry.text.trim()) issues.push(issue('bibliography.entry.empty', `参考文献 ${entry.id || '(空 id)'} 内容为空。`, 'warning'));
        refs.add(entry.id);
      }
    }
  });
  walkInlines(document, (inline, path) => {
    if (inline.type === 'citation' && !refs.has(inline.targetId)) {
      issues.push(issue('citation.target.missing', `引用目标 ${inline.targetId} 不存在于参考文献库。`, 'warning', path));
    }
  });
}

function validateReferences(document: ThesisDocument, issues: ApiIssue[]) {
  const bookmarks = new Set<string>();
  walkBlocks(document, (block) => {
    if ((block.type === 'heading' || block.type === 'equation') && block.bookmarkName) bookmarks.add(block.bookmarkName);
    if ((block.type === 'figure' || block.type === 'table' || block.type === 'equation') && block.id) bookmarks.add(block.id);
    if ((block.type === 'table' || block.type === 'equation') && block.bookmarkId) bookmarks.add(block.bookmarkId);
  });
  walkInlines(document, (inline, path) => {
    if (inline.type === 'bookmark') bookmarks.add(inline.name);
    if (inline.type === 'reference' && !bookmarks.has(inline.bookmarkName)) {
      issues.push(issue('reference.target.missing', `交叉引用目标 ${inline.bookmarkName} 不存在。`, 'warning', path));
    }
  });
}

function validateNotes(document: ThesisDocument, issues: ApiIssue[]) {
  const seenFootnotes = new Map<string, string>();
  const seenEndnotes = new Map<string, string>();
  walkBlocks(document, (block, path) => {
    if (block.type === 'footnote') addNote(block.noteId, path, seenFootnotes, 'duplicate.footnoteId', issues);
    if (block.type === 'endnote') addNote(block.noteId, path, seenEndnotes, 'duplicate.endnoteId', issues);
  });
  walkInlines(document, (inline, path) => {
    if (inline.type === 'footnote') {
      addNote(inline.noteId, path, seenFootnotes, 'duplicate.footnoteId', issues);
      if (!inlinesPlainText(inline.inlines).trim()) issues.push(issue('note.empty', '脚注内容为空。', 'error', path));
    }
    if (inline.type === 'endnote') {
      addNote(inline.noteId, path, seenEndnotes, 'duplicate.endnoteId', issues);
      if (!inlinesPlainText(inline.inlines).trim()) issues.push(issue('note.empty', '尾注内容为空。', 'error', path));
    }
  });
}

function addNote(noteId: string, path: string | undefined, seen: Map<string, string>, code: string, issues: ApiIssue[]) {
  if (!noteId.trim()) {
    issues.push(issue('note.id.empty', 'noteId 不能为空。', 'error', path));
    return;
  }
  const previous = seen.get(noteId);
  if (previous) {
    issues.push(issue(code, `noteId=${noteId} 重复；首次出现于 ${previous}。`, 'error', path));
  } else {
    seen.set(noteId, path ?? '$');
  }
}

