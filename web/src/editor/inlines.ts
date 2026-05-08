/**
 * Conversion between schema's Inline[] and TipTap's ProseMirror JSON.
 *
 * Supported:
 *  - text with marks: bold, italic, underline, link (hyperlink)
 *  - atomic inline nodes: citation, reference, footnote, endnote
 *
 * Not yet surfaced in the inline toolbar:
 *  - bookmark
 */

import type {
  CitationInline,
  EndnoteInline,
  FootnoteInline,
  HyperlinkInline,
  Inline,
  ReferenceInline,
  TextInline
} from '@/types';

export interface PMTextNode {
  type: 'text';
  text: string;
  marks?: PMMark[];
}

export interface PMMark {
  type: 'bold' | 'italic' | 'underline' | 'link';
  attrs?: Record<string, unknown>;
}

export interface PMCitationNode {
  type: 'citation';
  attrs: { targetId: string; displayText: string };
}

export interface PMReferenceNode {
  type: 'reference';
  attrs: { bookmarkName: string; fallbackText: string };
}

export interface PMNoteNode {
  type: 'footnote' | 'endnote';
  attrs: { noteId: string; inlinesJson: string; label: string };
}

export type PMInlineNode = PMTextNode | PMCitationNode | PMReferenceNode | PMNoteNode;

export interface PMParagraphNode {
  type: 'paragraph';
  content?: PMInlineNode[];
}

export interface PMDoc {
  type: 'doc';
  content: PMParagraphNode[];
}

export function inlinesToPMDoc(inlines: Inline[]): PMDoc {
  const content: PMInlineNode[] = [];
  for (const inline of inlines) {
    if (inline.type === 'text' && inline.text) {
      const marks: PMMark[] = [];
      if (inline.bold) marks.push({ type: 'bold' });
      if (inline.italic) marks.push({ type: 'italic' });
      if (inline.underline) marks.push({ type: 'underline' });
      content.push({
        type: 'text',
        text: inline.text,
        ...(marks.length ? { marks } : {})
      });
    } else if (inline.type === 'hyperlink' && inline.text) {
      content.push({
        type: 'text',
        text: inline.text,
        marks: [{ type: 'link', attrs: { href: inline.uri } }]
      });
    } else if (inline.type === 'citation') {
      content.push({
        type: 'citation',
        attrs: { targetId: inline.targetId, displayText: inline.displayText }
      });
    } else if (inline.type === 'reference') {
      content.push({
        type: 'reference',
        attrs: {
          bookmarkName: inline.bookmarkName,
          fallbackText: inline.fallbackText ?? ''
        }
      });
    } else if (inline.type === 'footnote' || inline.type === 'endnote') {
      content.push({
        type: inline.type,
        attrs: {
          noteId: inline.noteId,
          inlinesJson: JSON.stringify(inline.inlines),
          label: inline.type === 'footnote' ? `[脚注 ${inline.noteId}]` : `[尾注 ${inline.noteId}]`
        }
      });
    }
    // bookmark: not surfaced in this editor yet.
  }
  return {
    type: 'doc',
    content: [{ type: 'paragraph', ...(content.length ? { content } : {}) }]
  };
}

export function pmDocToInlines(doc: PMDoc): Inline[] {
  const out: Inline[] = [];
  const para = doc.content[0];
  if (!para?.content) return out;
  for (const node of para.content) {
    if (node.type === 'text' && node.text) {
      const marks = new Set((node.marks ?? []).map((m) => m.type));
      const linkMark = (node.marks ?? []).find((m) => m.type === 'link');
      if (linkMark && linkMark.attrs?.href) {
        const link: HyperlinkInline = {
          type: 'hyperlink',
          text: node.text,
          uri: String(linkMark.attrs.href)
        };
        out.push(link);
        continue;
      }
      const text: TextInline = { type: 'text', text: node.text };
      if (marks.has('bold')) text.bold = true;
      if (marks.has('italic')) text.italic = true;
      if (marks.has('underline')) text.underline = true;
      out.push(text);
    } else if (node.type === 'citation') {
      const c: CitationInline = {
        type: 'citation',
        targetId: node.attrs.targetId,
        displayText: node.attrs.displayText
      };
      out.push(c);
    } else if (node.type === 'reference') {
      const r: ReferenceInline = {
        type: 'reference',
        bookmarkName: node.attrs.bookmarkName,
        ...(node.attrs.fallbackText ? { fallbackText: node.attrs.fallbackText } : {})
      };
      out.push(r);
    } else if (node.type === 'footnote') {
      const f: FootnoteInline = {
        type: 'footnote',
        noteId: node.attrs.noteId,
        inlines: parseInlinesJson(node.attrs.inlinesJson)
      };
      out.push(f);
    } else if (node.type === 'endnote') {
      const e: EndnoteInline = {
        type: 'endnote',
        noteId: node.attrs.noteId,
        inlines: parseInlinesJson(node.attrs.inlinesJson)
      };
      out.push(e);
    }
  }
  return out;
}

export function plainTextToInlines(text: string): Inline[] {
  return text.length ? [{ type: 'text', text }] : [];
}

export function inlinesToPlainText(inlines: Inline[]): string {
  return inlines
    .map((inline) => {
      if (inline.type === 'text') return inline.text;
      if (inline.type === 'hyperlink') return inline.text;
      if (inline.type === 'citation') return inline.displayText;
      if (inline.type === 'reference') return inline.fallbackText ?? '[ref]';
      if (inline.type === 'footnote') return `[脚注 ${inline.noteId}]`;
      if (inline.type === 'endnote') return `[尾注 ${inline.noteId}]`;
      if (inline.type === 'bookmark') return inlinesToPlainText(inline.inlines);
      return '';
    })
    .join('');
}

function parseInlinesJson(value: string): Inline[] {
  try {
    const parsed = JSON.parse(value) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(isInline);
  } catch {
    return [];
  }
}

function isInline(value: unknown): value is Inline {
  return value !== null && typeof value === 'object' && 'type' in value;
}
