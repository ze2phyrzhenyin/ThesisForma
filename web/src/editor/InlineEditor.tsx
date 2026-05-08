import { useEditor, EditorContent, type Editor } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Underline from '@tiptap/extension-underline';
import Placeholder from '@tiptap/extension-placeholder';
import Link from '@tiptap/extension-link';
import { Extension } from '@tiptap/core';
import { CitationNode, ReferenceNode } from './inline-nodes';
import { useEffect, useRef } from 'react';
import type { Inline } from '@/types';
import { inlinesToPMDoc, pmDocToInlines, type PMDoc } from './inlines';
import styles from './InlineEditor.module.css';

interface Props {
  inlines: Inline[];
  placeholder?: string;
  ariaLabel?: string;
  autofocus?: boolean;
  onChange(inlines: Inline[]): void;
  onEnter?(): void;
  onBackspaceEmpty?(): void;
  onFocus?(): void;
  /** Receives the TipTap editor instance once mounted. */
  onEditorReady?(editor: Editor | null): void;
}


export function InlineEditor({
  inlines,
  placeholder,
  ariaLabel,
  autofocus,
  onChange,
  onEnter,
  onBackspaceEmpty,
  onFocus,
  onEditorReady
}: Props) {
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;
  const onEnterRef = useRef(onEnter);
  onEnterRef.current = onEnter;
  const onBackspaceRef = useRef(onBackspaceEmpty);
  onBackspaceRef.current = onBackspaceEmpty;

  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        heading: false,
        codeBlock: false,
        blockquote: false,
        bulletList: false,
        orderedList: false,
        listItem: false,
        horizontalRule: false,
        code: false,
        strike: false
      }),
      Underline,
      Link.configure({
        openOnClick: false,
        autolink: false,
        HTMLAttributes: { rel: 'noopener noreferrer', target: '_blank' }
      }),
      CitationNode,
      ReferenceNode,
      Placeholder.configure({
        placeholder: placeholder ?? '',
        emptyEditorClass: 'empty',
        emptyNodeClass: 'empty'
      }),
      // Intercept Enter / Backspace at boundary so block-level commands fire.
      Extension.create({
        name: 'blockBoundary',
        addKeyboardShortcuts() {
          return {
            Enter: () => {
              onEnterRef.current?.();
              return true;
            },
            'Shift-Enter': () => {
              this.editor.commands.setHardBreak();
              return true;
            },
            Backspace: () => {
              const { from, to } = this.editor.state.selection;
              if (from === to && this.editor.state.doc.textContent.length === 0) {
                onBackspaceRef.current?.();
                return true;
              }
              return false;
            }
          };
        }
      })
    ],
    content: inlinesToPMDoc(inlines),
    editorProps: {
      attributes: {
        class: styles.editor,
        ...(ariaLabel ? { 'aria-label': ariaLabel } : {})
      }
    },
    autofocus: autofocus ? 'end' : false,
    onUpdate({ editor }) {
      const json = editor.getJSON() as unknown as PMDoc;
      onChangeRef.current(pmDocToInlines(json));
    },
    onFocus() {
      onFocus?.();
    }
  });

  // Keep editor content in sync if external state changes (e.g. import / undo)
  // but avoid disrupting the user's caret while they're typing.
  useEffect(() => {
    if (!editor) return;
    const current = pmDocToInlines(editor.getJSON() as unknown as PMDoc);
    if (sameInlines(current, inlines)) return;
    // Only resync when not focused, to avoid stomping on the user.
    if (editor.isFocused) return;
    editor.commands.setContent(inlinesToPMDoc(inlines), false);
  }, [editor, inlines]);

  const onReadyRef = useRef(onEditorReady);
  onReadyRef.current = onEditorReady;
  useEffect(() => {
    onReadyRef.current?.(editor ?? null);
    return () => onReadyRef.current?.(null);
  }, [editor]);

  // Keep placeholder text in sync
  useEffect(() => {
    if (!editor) return;
    const ext = editor.extensionManager.extensions.find((e) => e.name === 'placeholder');
    if (ext) {
      ext.options.placeholder = placeholder ?? '';
      editor.view.dispatch(editor.state.tr);
    }
  }, [editor, placeholder]);

  return <EditorContent editor={editor} />;
}

function sameInlines(a: Inline[], b: Inline[]): boolean {
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i++) {
    const x = a[i];
    const y = b[i];
    if (x.type !== y.type) return false;
    if (x.type === 'text' && y.type === 'text') {
      if (x.text !== y.text) return false;
      if ((x.bold ?? false) !== (y.bold ?? false)) return false;
      if ((x.italic ?? false) !== (y.italic ?? false)) return false;
      if ((x.underline ?? false) !== (y.underline ?? false)) return false;
    }
  }
  return true;
}
