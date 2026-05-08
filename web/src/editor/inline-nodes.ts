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
