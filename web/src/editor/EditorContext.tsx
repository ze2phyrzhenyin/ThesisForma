import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { useStore } from 'zustand';
import { useShallow } from 'zustand/react/shallow';
import { createEditorStore, type EditorState, type EditorStore } from './store';
import type { DocumentEnvelope } from '@/types';

const EditorContext = createContext<EditorStore | null>(null);

interface ProviderProps {
  envelope: DocumentEnvelope;
  children: ReactNode;
}

export function EditorProvider({ envelope, children }: ProviderProps) {
  const store = useMemo(() => createEditorStore(envelope), [envelope.id]);
  return <EditorContext.Provider value={store}>{children}</EditorContext.Provider>;
}

export function useEditorStore<T>(selector: (state: EditorState) => T): T {
  const store = useContext(EditorContext);
  if (!store) throw new Error('useEditorStore must be used within EditorProvider');
  return useStore(store, selector);
}

export function useEditorStoreInstance(): EditorStore {
  const store = useContext(EditorContext);
  if (!store) throw new Error('useEditorStoreInstance must be used within EditorProvider');
  return store;
}

interface TemporalState {
  undo: () => void;
  redo: () => void;
  pastStates: unknown[];
  futureStates: unknown[];
}

export function useTemporal<T>(selector: (state: TemporalState) => T): T {
  const store = useContext(EditorContext) as
    | (EditorStore & { temporal: { getState: () => TemporalState; subscribe: (listener: (s: TemporalState) => void) => () => void } })
    | null;
  if (!store) throw new Error('useTemporal must be used within EditorProvider');
  return useStore(store.temporal as unknown as Parameters<typeof useStore>[0], selector as never) as T;
}

export function useEditorActions() {
  return useEditorStore(
    useShallow((s) => ({
      setView: s.setView,
      selectBlock: s.selectBlock,
      clearSelection: s.clearSelection,
      updateMetadata: s.updateMetadata,
      updateDocument: s.updateDocument,
      ensureSection: s.ensureSection,
      addSection: s.addSection,
      removeSection: s.removeSection,
      moveSection: s.moveSection,
      renameSection: s.renameSection,
      updateSection: s.updateSection,
      insertBlock: s.insertBlock,
      appendBlock: s.appendBlock,
      deleteBlock: s.deleteBlock,
      moveBlock: s.moveBlock,
      updateBlock: s.updateBlock,
      replaceInlines: s.replaceInlines,
      updateOverrides: s.updateOverrides,
      markDirty: s.markDirty,
      markSaved: s.markSaved
    }))
  );
}
