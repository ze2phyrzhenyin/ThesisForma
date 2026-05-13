import { useEffect, useRef } from 'react';
import { useSaveDocument } from '@/api/queries';
import { useEditorStoreInstance } from './EditorContext';

const DEBOUNCE_MS = 1500;

/**
 * Subscribes to the editor store and persists changes to the API.
 * Saves are debounced; failures back off but do not lose user work
 * (the in-memory store is the source of truth until success).
 */
export function useAutoSave() {
  const store = useEditorStoreInstance();
  const save = useSaveDocument();
  const saveRef = useRef(save);
  saveRef.current = save;
  const timer = useRef<number | null>(null);
  const inflight = useRef(false);

  useEffect(() => {
    const unsubscribe = store.subscribe((state, prev) => {
      if (state.envelope === prev.envelope && state.dirty === prev.dirty) return;
      if (!state.dirty) return;
      if (timer.current !== null) clearTimeout(timer.current);
      timer.current = window.setTimeout(async () => {
        if (inflight.current) return;
        const current = store.getState();
        if (!current.dirty) return;
        inflight.current = true;
        const savedSnapshot = JSON.stringify({ document: current.envelope.document, overrides: current.envelope.overrides ?? null });
        try {
          const env = await saveRef.current.mutateAsync({
            id: current.envelope.id,
            document: current.envelope.document,
            templateId: current.envelope.templateId ?? null,
            overrides: current.envelope.overrides ?? null
          });
          const after = store.getState();
          const stillDirty = JSON.stringify({ document: after.envelope.document, overrides: after.envelope.overrides ?? null }) !== savedSnapshot;
          store.setState({ envelope: { ...after.envelope, overrides: env.overrides ?? after.envelope.overrides ?? null }, lastSavedAt: env.updatedAt, dirty: stillDirty });
        } catch {
          // Leave dirty=true so the next debounce attempt retries.
        } finally {
          inflight.current = false;
        }
      }, DEBOUNCE_MS);
    });
    return () => {
      unsubscribe();
      if (timer.current !== null) clearTimeout(timer.current);
    };
  }, [store]);
}
