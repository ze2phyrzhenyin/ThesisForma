/**
 * Custom TipTap nodes for citation and reference inlines.
 * Both render as atomic chips that can be selected and deleted as a unit.
 */

import { Node, mergeAttributes } from '@tiptap/core';

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    citationChip: {
      insertCitation: (attrs: { targetId: string; displayText: string }) => ReturnType;
    };
    referenceChip: {
      insertReference: (attrs: { bookmarkName: string; fallbackText: string }) => ReturnType;
    };
    noteChip: {
      insertFootnote: (attrs: { noteId: string; inlinesJson: string; label: string }) => ReturnType;
      insertEndnote: (attrs: { noteId: string; inlinesJson: string; label: string }) => ReturnType;
    };
  }
}

export const CitationNode = Node.create({
  name: 'citation',
  group: 'inline',
  inline: true,
  atom: true,
  selectable: true,

  addAttributes() {
    return {
      targetId: { default: '' },
      displayText: { default: '' }
    };
  },

  parseHTML() {
    return [{ tag: 'span[data-citation]' }];
  },

  renderHTML({ HTMLAttributes, node }) {
    return [
      'span',
      mergeAttributes(HTMLAttributes, {
        'data-citation': node.attrs.targetId,
        class: 'tf-citation-chip'
      }),
      node.attrs.displayText || `[${node.attrs.targetId}]`
    ];
  },

  addCommands() {
    return {
      insertCitation:
        (attrs) =>
        ({ commands }) =>
          commands.insertContent({ type: this.name, attrs })
    };
  }
});

export const ReferenceNode = Node.create({
  name: 'reference',
  group: 'inline',
  inline: true,
  atom: true,
  selectable: true,

  addAttributes() {
    return {
      bookmarkName: { default: '' },
      fallbackText: { default: '' }
    };
  },

  parseHTML() {
    return [{ tag: 'span[data-reference]' }];
  },

  renderHTML({ HTMLAttributes, node }) {
    return [
      'span',
      mergeAttributes(HTMLAttributes, {
        'data-reference': node.attrs.bookmarkName,
        class: 'tf-reference-chip'
      }),
      node.attrs.fallbackText || `参见 ${node.attrs.bookmarkName}`
    ];
  },

  addCommands() {
    return {
      insertReference:
        (attrs) =>
        ({ commands }) =>
          commands.insertContent({ type: this.name, attrs })
    };
  }
});

function noteNode(name: 'footnote' | 'endnote', labelPrefix: string) {
  return Node.create({
    name,
    group: 'inline',
    inline: true,
    atom: true,
    selectable: true,

    addAttributes() {
      return {
        noteId: { default: '' },
        inlinesJson: { default: '[]' },
        label: { default: '' }
      };
    },

    parseHTML() {
      return [
        {
          tag: `span[data-${name}]`,
          getAttrs: (node) => {
            if (!(node instanceof HTMLElement)) return false;
            return {
              noteId: node.getAttribute(`data-${name}`) ?? '',
              inlinesJson: node.getAttribute('data-inlines-json') ?? '[]',
              label: node.getAttribute('data-label') ?? node.textContent ?? ''
            };
          }
        }
      ];
    },

    renderHTML({ HTMLAttributes, node }) {
      return [
        'span',
        mergeAttributes(HTMLAttributes, {
          [`data-${name}`]: node.attrs.noteId,
          'data-inlines-json': node.attrs.inlinesJson,
          'data-label': node.attrs.label,
          title: node.attrs.noteId,
          class: name === 'footnote' ? 'tf-footnote-chip' : 'tf-endnote-chip'
        }),
        node.attrs.label || `[${labelPrefix} ${node.attrs.noteId}]`
      ];
    },

    addCommands() {
      const commandName = name === 'footnote' ? 'insertFootnote' : 'insertEndnote';
      return {
        [commandName]:
          (attrs: { noteId: string; inlinesJson: string; label: string }) =>
          ({ commands }: { commands: { insertContent: (value: unknown) => boolean } }) =>
            commands.insertContent({ type: this.name, attrs })
      };
    }
  });
}

export const FootnoteNode = noteNode('footnote', '脚注');
export const EndnoteNode = noteNode('endnote', '尾注');
