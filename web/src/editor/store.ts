import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { temporal } from 'zundo';
import type {
  Block,
  BlockType,
  DocumentEnvelope,
  Inline,
  Metadata,
  Section,
  SectionKind,
  ThesisDocument
} from '@/types';
import { SECTION_META } from './sectionMeta';
import { plainTextToInlines } from './inlines';
import { newBlockId } from './ids';

export type EditorView =
  | { kind: 'metadata' }
  | { kind: 'variables' }
  | { kind: 'overrides' }
  | { kind: 'section'; sectionIndex: number };

export interface EditorState {
  envelope: DocumentEnvelope;
  view: EditorView;
  selectedBlock: { sectionIndex: number; blockIndex: number } | null;
  dirty: boolean;
  lastSavedAt: string;

  // Mutation actions ------------------------------------------------------
  setView(view: EditorView): void;
  selectBlock(sectionIndex: number, blockIndex: number): void;
  clearSelection(): void;

  updateMetadata(patch: Partial<Metadata>): void;
  updateDocument(updater: (doc: ThesisDocument) => void): void;

  ensureSection(kind: SectionKind): number;
  removeSection(kind: SectionKind): void;
  updateSection(sectionIndex: number, updater: (section: Section) => void): void;

  insertBlock(sectionIndex: number, blockIndex: number, block: Block): void;
  appendBlock(sectionIndex: number, block: Block): void;
  deleteBlock(sectionIndex: number, blockIndex: number): void;
  moveBlock(sectionIndex: number, from: number, to: number): void;
  updateBlock(sectionIndex: number, blockIndex: number, updater: (block: Block) => void): void;
  replaceInlines(sectionIndex: number, blockIndex: number, inlines: Inline[]): void;

  markDirty(): void;
  markSaved(at: string): void;
}

export const createEditorStore = (envelope: DocumentEnvelope) =>
  create<EditorState>()(
    temporal(
      immer((set) => ({
      envelope,
      view: pickInitialView(envelope.document),
      selectedBlock: null,
      dirty: false,
      lastSavedAt: envelope.updatedAt,

      setView: (view) =>
        set((s) => {
          s.view = view;
          s.selectedBlock = null;
        }),

      selectBlock: (sectionIndex, blockIndex) =>
        set((s) => {
          s.selectedBlock = { sectionIndex, blockIndex };
        }),

      clearSelection: () =>
        set((s) => {
          s.selectedBlock = null;
        }),

      updateMetadata: (patch) =>
        set((s) => {
          Object.assign(s.envelope.document.metadata, patch);
          s.dirty = true;
        }),

      updateDocument: (updater) =>
        set((s) => {
          updater(s.envelope.document);
          s.dirty = true;
        }),

      ensureSection: (kind) => {
        let resultIndex = -1;
        set((s) => {
          const existing = s.envelope.document.sections.findIndex((sec) => sec.kind === kind);
          if (existing >= 0) {
            resultIndex = existing;
            return;
          }
          const newSection: Section = {
            id: kind,
            kind,
            title: SECTION_META[kind].label,
            blocks: []
          };
          // insert in canonical order
          const order = SECTION_META[kind].order;
          let insertAt = s.envelope.document.sections.length;
          for (let i = 0; i < s.envelope.document.sections.length; i++) {
            const otherKind = s.envelope.document.sections[i].kind;
            if (SECTION_META[otherKind].order > order) {
              insertAt = i;
              break;
            }
          }
          s.envelope.document.sections.splice(insertAt, 0, newSection);
          resultIndex = insertAt;
          s.dirty = true;
        });
        return resultIndex;
      },

      removeSection: (kind) =>
        set((s) => {
          const idx = s.envelope.document.sections.findIndex((sec) => sec.kind === kind);
          if (idx >= 0) {
            s.envelope.document.sections.splice(idx, 1);
            s.dirty = true;
          }
        }),

      updateSection: (sectionIndex, updater) =>
        set((s) => {
          const section = s.envelope.document.sections[sectionIndex];
          if (!section) return;
          updater(section);
          s.dirty = true;
        }),

      insertBlock: (sectionIndex, blockIndex, block) =>
        set((s) => {
          const section = s.envelope.document.sections[sectionIndex];
          if (!section) return;
          section.blocks.splice(blockIndex, 0, block);
          s.dirty = true;
          s.selectedBlock = { sectionIndex, blockIndex };
        }),

      appendBlock: (sectionIndex, block) =>
        set((s) => {
          const section = s.envelope.document.sections[sectionIndex];
          if (!section) return;
          section.blocks.push(block);
          s.dirty = true;
          s.selectedBlock = { sectionIndex, blockIndex: section.blocks.length - 1 };
        }),

      deleteBlock: (sectionIndex, blockIndex) =>
        set((s) => {
          const section = s.envelope.document.sections[sectionIndex];
          if (!section) return;
          section.blocks.splice(blockIndex, 1);
          s.dirty = true;
          if (
            s.selectedBlock &&
            s.selectedBlock.sectionIndex === sectionIndex &&
            s.selectedBlock.blockIndex >= section.blocks.length
          ) {
            s.selectedBlock = null;
          }
        }),

      moveBlock: (sectionIndex, from, to) =>
        set((s) => {
          const section = s.envelope.document.sections[sectionIndex];
          if (!section) return;
          if (to < 0 || to >= section.blocks.length) return;
          const [block] = section.blocks.splice(from, 1);
          section.blocks.splice(to, 0, block);
          s.dirty = true;
          s.selectedBlock = { sectionIndex, blockIndex: to };
        }),

      updateBlock: (sectionIndex, blockIndex, updater) =>
        set((s) => {
          const block = s.envelope.document.sections[sectionIndex]?.blocks[blockIndex];
          if (!block) return;
          updater(block);
          s.dirty = true;
        }),

      replaceInlines: (sectionIndex, blockIndex, inlines) =>
        set((s) => {
          const block = s.envelope.document.sections[sectionIndex]?.blocks[blockIndex];
          if (!block) return;
          if (block.type === 'paragraph' || block.type === 'heading' || block.type === 'quote') {
            block.inlines = inlines;
            s.dirty = true;
          }
        }),

      markDirty: () =>
        set((s) => {
          s.dirty = true;
        }),

      markSaved: (at) =>
        set((s) => {
          s.dirty = false;
          s.lastSavedAt = at;
        })
    })),
    {
      // zundo: only track document content for undo/redo, ignore view / selection / dirty
      partialize: (state) => ({
        envelope: { ...state.envelope, document: state.envelope.document }
      }) as Partial<EditorState>,
      limit: 200,
      equality: (a, b) =>
        JSON.stringify((a as { envelope?: { document?: unknown } })?.envelope?.document) ===
        JSON.stringify((b as { envelope?: { document?: unknown } })?.envelope?.document)
    }
    )
  );

export type EditorStore = ReturnType<typeof createEditorStore>;

function pickInitialView(doc: ThesisDocument): EditorView {
  const bodyIdx = doc.sections.findIndex((s) => s.kind === 'body');
  if (bodyIdx >= 0) return { kind: 'section', sectionIndex: bodyIdx };
  const firstAuthored = doc.sections.findIndex((s) => SECTION_META[s.kind].authored);
  if (firstAuthored >= 0) return { kind: 'section', sectionIndex: firstAuthored };
  return { kind: 'metadata' };
}

// ───── Block factories ─────────────────────────────────────────────────────

export const blockFactory = {
  paragraph: (text = ''): Block => ({
    type: 'paragraph',
    id: newBlockId('p'),
    inlines: plainTextToInlines(text)
  }),
  heading: (level: 1 | 2 | 3 | 4 | 5 | 6 = 1, text = ''): Block => ({
    type: 'heading',
    id: newBlockId('h'),
    level,
    bookmarkName: newBlockId('h'),
    inlines: plainTextToInlines(text || '新标题')
  }),
  list: (ordered = false): Block => ({
    type: 'list',
    id: newBlockId('list'),
    ordered,
    items: [
      {
        blocks: [
          {
            type: 'paragraph',
            id: newBlockId('p'),
            inlines: []
          }
        ]
      }
    ]
  }),
  quote: (): Block => ({
    type: 'quote',
    id: newBlockId('q'),
    inlines: []
  }),
  pageBreak: (): Block => ({ type: 'pageBreak', id: newBlockId('pb') }),
  sectionBreak: (): Block => ({ type: 'sectionBreak', id: newBlockId('sb') }),
  figure: (): Block => ({
    type: 'figure',
    id: newBlockId('fig'),
    caption: '',
    imageContentType: 'image/png'
  }),
  table: (): Block => ({
    type: 'table',
    id: newBlockId('tbl'),
    caption: '',
    rows: [
      {
        id: newBlockId('tr'),
        isHeader: true,
        cells: [
          { id: newBlockId('tc'), text: '列 1' },
          { id: newBlockId('tc'), text: '列 2' },
          { id: newBlockId('tc'), text: '列 3' }
        ]
      },
      {
        id: newBlockId('tr'),
        cells: [
          { id: newBlockId('tc'), text: '' },
          { id: newBlockId('tc'), text: '' },
          { id: newBlockId('tc'), text: '' }
        ]
      }
    ]
  }),
  equation: (): Block => ({
    type: 'equation',
    id: newBlockId('eq'),
    sourceType: 'latex',
    latex: '',
    display: true
  })
} as const;

export type SimpleBlockType = keyof typeof blockFactory;

export function blockFactoryFor(type: BlockType): Block | null {
  switch (type) {
    case 'paragraph':
      return blockFactory.paragraph();
    case 'heading':
      return blockFactory.heading(1);
    case 'list':
      return blockFactory.list(false);
    case 'quote':
      return blockFactory.quote();
    case 'pageBreak':
      return blockFactory.pageBreak();
    case 'sectionBreak':
      return blockFactory.sectionBreak();
    case 'figure':
      return blockFactory.figure();
    case 'table':
      return blockFactory.table();
    case 'equation':
      return blockFactory.equation();
    default:
      return null;
  }
}
