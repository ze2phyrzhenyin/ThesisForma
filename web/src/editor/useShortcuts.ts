import { useEffect } from 'react';
import { useEditorStoreInstance } from './EditorContext';

/**
 * Global keyboard shortcuts:
 *  - Cmd/Ctrl+Z   undo
 *  - Cmd/Ctrl+Shift+Z   redo
 *  - Cmd/Ctrl+.   toggle focus mode
 */
export function useShortcuts(opts: { onToggleFocus(): void }) {
  const store = useEditorStoreInstance();

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const mod = e.metaKey || e.ctrlKey;
      if (!mod) return;

      // Don't intercept if user is editing form input that already handles undo
      // (TipTap handles its own undo, but our store-level undo covers block ops).
      const target = e.target as HTMLElement | null;
      const tag = target?.tagName?.toLowerCase();
      const isContentEditable = target?.isContentEditable;
      if (tag === 'input' || tag === 'textarea' || isContentEditable) return;

      if (e.key.toLowerCase() === 'z' && !e.shiftKey) {
        e.preventDefault();
        const t = (store as unknown as { temporal: { getState: () => { undo: () => void } } })
          .temporal;
        t.getState().undo();
      } else if ((e.key.toLowerCase() === 'z' && e.shiftKey) || e.key.toLowerCase() === 'y') {
        e.preventDefault();
        const t = (store as unknown as { temporal: { getState: () => { redo: () => void } } })
          .temporal;
        t.getState().redo();
      } else if (e.key === '.') {
        e.preventDefault();
        opts.onToggleFocus();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [store, opts]);
}
