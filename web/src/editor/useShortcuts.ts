import { useEffect } from 'react';
import { useEditorStoreInstance } from './EditorContext';

/**
 * Global keyboard shortcuts:
 *  - Cmd/Ctrl+Z   undo
 *  - Cmd/Ctrl+Shift+Z   redo
 *  - Cmd/Ctrl+S   save
 *  - Cmd/Ctrl+E   export JSON
 *  - Cmd/Ctrl+.   toggle focus mode
 */
export function useShortcuts(opts: { onToggleFocus(): void; onSave?(): void; onExportJson?(): void }) {
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
      const key = e.key.toLowerCase();
      const isTextEditing = tag === 'input' || tag === 'textarea' || isContentEditable;
      if (isTextEditing && key !== 's' && key !== 'e') return;

      if (key === 'z' && !e.shiftKey) {
        e.preventDefault();
        const t = (store as unknown as { temporal: { getState: () => { undo: () => void } } })
          .temporal;
        t.getState().undo();
      } else if ((key === 'z' && e.shiftKey) || key === 'y') {
        e.preventDefault();
        const t = (store as unknown as { temporal: { getState: () => { redo: () => void } } })
          .temporal;
        t.getState().redo();
      } else if (key === 's') {
        e.preventDefault();
        opts.onSave?.();
      } else if (key === 'e') {
        e.preventDefault();
        opts.onExportJson?.();
      } else if (e.key === '.') {
        e.preventDefault();
        opts.onToggleFocus();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [store, opts]);
}
