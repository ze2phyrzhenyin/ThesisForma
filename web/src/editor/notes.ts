import type { Block, Inline, ThesisDocument } from '@/types';
import { newBlockId } from './ids';
import { inlinesToPlainText, plainTextToInlines } from './inlines';

export type NoteKind = 'footnote' | 'endnote';

export interface NoteEntry {
  kind: NoteKind;
  noteId: string;
  text: string;
  sectionIndex: number;
  blockIndex: number;
  inlineIndex: number | null;
  locationLabel: string;
}

export function collectNotes(document: ThesisDocument): NoteEntry[] {
  const notes: NoteEntry[] = [];
  document.sections.forEach((section, sectionIndex) => {
    section.blocks.forEach((block, blockIndex) => {
      const locationLabel = `${section.title ?? section.kind} / 块 ${blockIndex + 1}`;
      if (block.type === 'footnote' || block.type === 'endnote') {
        notes.push({
          kind: block.type,
          noteId: block.noteId,
          text: inlinesToPlainText(block.inlines),
          sectionIndex,
          blockIndex,
          inlineIndex: null,
          locationLabel
        });
      }
      getEditableInlines(block)?.forEach((inline, inlineIndex) => {
        if (inline.type === 'footnote' || inline.type === 'endnote') {
          notes.push({
            kind: inline.type,
            noteId: inline.noteId,
            text: inlinesToPlainText(inline.inlines),
            sectionIndex,
            blockIndex,
            inlineIndex,
            locationLabel
          });
        }
      });
    });
  });
  return notes;
}

export function insertNoteAtBlockEnd(
  document: ThesisDocument,
  sectionIndex: number,
  blockIndex: number,
  kind: NoteKind,
  text = ''
): NoteEntry | null {
  const block = document.sections[sectionIndex]?.blocks[blockIndex];
  const noteId = newNoteId(kind);
  const note = makeNoteInline(kind, noteId, text || (kind === 'footnote' ? '脚注内容' : '尾注内容'));
  const inlines = getEditableInlines(block);
  if (inlines) {
    inlines.push(note);
    return {
      kind,
      noteId,
      text: inlinesToPlainText(note.inlines),
      sectionIndex,
      blockIndex,
      inlineIndex: inlines.length - 1,
      locationLabel: `${document.sections[sectionIndex].title ?? document.sections[sectionIndex].kind} / 块 ${blockIndex + 1}`
    };
  }
  const bodyIndex = document.sections.findIndex((section) => section.kind === 'body');
  const targetSectionIndex = bodyIndex >= 0 ? bodyIndex : 0;
  const targetSection = document.sections[targetSectionIndex];
  const fallbackBlock: Block = {
    type: 'paragraph',
    id: newBlockId('p'),
    inlines: [{ type: 'text', text: '' }, note]
  };
  targetSection.blocks.push(fallbackBlock);
  return {
    kind,
    noteId,
    text: inlinesToPlainText(note.inlines),
    sectionIndex: targetSectionIndex,
    blockIndex: targetSection.blocks.length - 1,
    inlineIndex: 1,
    locationLabel: `${targetSection.title ?? targetSection.kind} / 块 ${targetSection.blocks.length}`
  };
}

export function updateNoteText(document: ThesisDocument, entry: NoteEntry, text: string): void {
  const block = document.sections[entry.sectionIndex]?.blocks[entry.blockIndex];
  if (!block) return;
  if (entry.inlineIndex === null) {
    if (block.type === 'footnote' || block.type === 'endnote') {
      block.inlines = plainTextToInlines(text);
    }
    return;
  }
  const inlines = getEditableInlines(block);
  const inline = inlines?.[entry.inlineIndex];
  if (inline?.type === 'footnote' || inline?.type === 'endnote') {
    inline.inlines = plainTextToInlines(text);
  }
}

export function deleteNote(document: ThesisDocument, entry: NoteEntry): void {
  const section = document.sections[entry.sectionIndex];
  const block = section?.blocks[entry.blockIndex];
  if (!section || !block) return;
  if (entry.inlineIndex === null) {
    section.blocks.splice(entry.blockIndex, 1);
    return;
  }
  const inlines = getEditableInlines(block);
  if (!inlines) return;
  inlines.splice(entry.inlineIndex, 1);
}

export function noteLabel(kind: NoteKind, index: number): string {
  return kind === 'footnote' ? `脚注 ${index + 1}` : `尾注 ${index + 1}`;
}

export function makeNoteInline(kind: NoteKind, noteId: string, text: string) {
  const inlines = plainTextToInlines(text);
  return kind === 'footnote'
    ? ({ type: 'footnote', noteId, inlines } as const)
    : ({ type: 'endnote', noteId, inlines } as const);
}

function getEditableInlines(block: Block | undefined): Inline[] | null {
  if (!block) return null;
  if (block.type === 'paragraph' || block.type === 'heading' || block.type === 'quote') {
    return block.inlines;
  }
  return null;
}

function newNoteId(kind: NoteKind): string {
  const prefix = kind === 'footnote' ? 'fn' : 'en';
  const random =
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID().replace(/-/g, '').slice(0, 8)
      : Math.random().toString(36).slice(2, 10);
  return `${prefix}-${Date.now().toString(36)}-${random}`;
}
