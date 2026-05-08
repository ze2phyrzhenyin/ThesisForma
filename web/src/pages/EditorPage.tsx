import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useDocument, useSaveDocument, useValidateDocument } from '@/api/queries';
import { Brand } from '@/components/Brand';
import { EditorProvider, useEditorActions, useEditorStore } from '@/editor/EditorContext';
import { EditorTopbar } from '@/editor/EditorTopbar';
import { SectionNav } from '@/editor/canvas/SectionNav';
import { Canvas } from '@/editor/canvas/Canvas';
import { RightRail, type DrawerKey } from '@/editor/RightRail';
import { ThesisApiError } from '@/api/client';
import { useAutoSave } from '@/editor/useAutoSave';
import { useShortcuts } from '@/editor/useShortcuts';
import type { ApiIssue } from '@/types';
import styles from './EditorPage.module.css';

interface ValidationState {
  isValid: boolean | null;
  issues: ApiIssue[];
  checkedAt: string | null;
}

function EditorInner() {
  const envelope = useEditorStore((s) => s.envelope);
  const dirty = useEditorStore((s) => s.dirty);
  const actions = useEditorActions();
  const save = useSaveDocument();
  const validate = useValidateDocument();

  const [drawer, setDrawer] = useState<DrawerKey>(null);
  const [focusMode, setFocusMode] = useState(false);
  const [validation, setValidation] = useState<ValidationState>({
    isValid: null,
    issues: [],
    checkedAt: null
  });

  useAutoSave();
  useShortcuts({ onToggleFocus: () => setFocusMode((v) => !v) });

  const runValidate = async () => {
    try {
      if (dirty) {
        const saved = await save.mutateAsync({
          id: envelope.id,
          document: envelope.document,
          templateId: envelope.templateId ?? null
        });
        actions.markSaved(saved.updatedAt);
      }
      const v = await validate.mutateAsync({
        id: envelope.id,
        templateId: envelope.templateId ?? null
      });
      setValidation({
        isValid: v.isValid,
        issues: v.issues,
        checkedAt: new Date().toLocaleTimeString()
      });
    } catch (e) {
      const msg = e instanceof ThesisApiError ? e.payload?.message ?? e.message : (e as Error).message;
      const issues: ApiIssue[] =
        e instanceof ThesisApiError && e.payload?.issues
          ? e.payload.issues
          : [
              {
                code: 'request.failed',
                message: msg,
                severity: 'error',
                path: null,
                suggestedAction: null
              }
            ];
      setValidation({
        isValid: false,
        issues,
        checkedAt: new Date().toLocaleTimeString()
      });
    }
  };

  return (
    <div className={styles.page} data-focus-mode={focusMode}>
      <EditorTopbar
        onValidationResult={(r) =>
          setValidation({ isValid: r.isValid, issues: r.issues, checkedAt: r.checkedAt })
        }
        onOpenValidationDrawer={() => setDrawer('validation')}
        focusMode={focusMode}
        onToggleFocus={() => setFocusMode((v) => !v)}
      />
      <div className={styles.layout}>
        {!focusMode && (
          <aside className={styles.leftRail}>
            <SectionNav />
          </aside>
        )}
        <main className={styles.canvas}>
          <Canvas />
        </main>
        {!focusMode && (
          <RightRail
            open={drawer}
            onChange={setDrawer}
            validation={{
              isValid: validation.isValid,
              issues: validation.issues,
              isRunning: validate.isPending,
              lastCheckedAt: validation.checkedAt
            }}
            onRunValidation={runValidate}
          />
        )}
      </div>
    </div>
  );
}

export function EditorPage() {
  const { docId } = useParams<{ docId: string }>();
  const { data: envelope, isLoading, isError, error } = useDocument(docId);

  if (isLoading) {
    return (
      <div className={styles.page}>
        <header className={styles.topbarFallback}>
          <Brand size="sm" />
        </header>
        <main className={styles.loading}>正在加载文档…</main>
      </div>
    );
  }

  if (isError || !envelope) {
    return (
      <div className={styles.page}>
        <header className={styles.topbarFallback}>
          <Brand size="sm" />
        </header>
        <main className={styles.loading}>
          {error instanceof Error ? error.message : '文档加载失败'}
        </main>
      </div>
    );
  }

  return (
    <EditorProvider envelope={envelope}>
      <EditorInner />
    </EditorProvider>
  );
}
