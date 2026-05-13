import { useEffect, useState } from 'react';
import { useEditorActions, useEditorStore } from './EditorContext';
import { useRenderDocument, useSaveDocument, useValidateDocument } from '@/api/queries';
import { runDownloadUrl, ThesisApiError } from '@/api/client';
import type { ApiIssue, RenderRunResponse } from '@/types';
import styles from './ExportModal.module.css';

interface Props {
  open: boolean;
  onClose(): void;
}

type Phase = 'idle' | 'saving' | 'validating' | 'rendering' | 'done' | 'error';

export function ExportModal({ open, onClose }: Props) {
  const envelope = useEditorStore((s) => s.envelope);
  const dirty = useEditorStore((s) => s.dirty);
  const actions = useEditorActions();

  const save = useSaveDocument();
  const validate = useValidateDocument();
  const render = useRenderDocument();

  const [phase, setPhase] = useState<Phase>('idle');
  const [run, setRun] = useState<RenderRunResponse | null>(null);
  const [issues, setIssues] = useState<ApiIssue[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    const go = async () => {
      setRun(null);
      setIssues([]);
      setError(null);
      try {
        // 1. Save (always — assume the user has unsaved changes)
        if (dirty) {
          setPhase('saving');
          const saved = await save.mutateAsync({
            id: envelope.id,
            document: envelope.document,
            templateId: envelope.templateId ?? null,
            overrides: envelope.overrides ?? null
          });
          actions.markSaved(saved.updatedAt);
        }
        if (cancelled) return;
        // 2. Validate
        setPhase('validating');
        const v = await validate.mutateAsync({
          id: envelope.id,
          templateId: envelope.templateId ?? null,
          overrides: envelope.overrides ?? null
        });
        if (cancelled) return;
        if (!v.isValid) {
          setIssues(v.issues);
          setPhase('error');
          return;
        }
        // 3. Render
        setPhase('rendering');
        const r = await render.mutateAsync({
          id: envelope.id,
          templateId: envelope.templateId ?? null,
          overrides: envelope.overrides ?? null
        });
        if (cancelled) return;
        setRun(r);
        if (r.issues?.length) setIssues(r.issues);
        setPhase('done');
      } catch (e) {
        if (cancelled) return;
        if (e instanceof ThesisApiError) {
          if (e.payload?.issues?.length) setIssues(e.payload.issues);
          setError(e.payload?.message ?? e.message);
        } else if (e instanceof Error) {
          setError(e.message);
        } else {
          setError('导出失败');
        }
        setPhase('error');
      }
    };
    void go();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  if (!open) return null;

  return (
    <div className={styles.backdrop} role="dialog" aria-modal>
      <div className={styles.modal}>
        <header className={styles.header}>
          <h2 className={styles.title}>导出 DOCX</h2>
          <button type="button" className={styles.close} onClick={onClose} aria-label="关闭">
            ✕
          </button>
        </header>

        <ol className={styles.steps}>
          <Step
            label="保存草稿"
            done={['validating', 'rendering', 'done'].includes(phase)}
            active={phase === 'saving'}
            errored={phase === 'error' && !run && !issues.length && Boolean(error)}
          />
          <Step
            label="结构校验"
            done={['rendering', 'done'].includes(phase)}
            active={phase === 'validating'}
            errored={phase === 'error' && issues.length > 0 && !run}
          />
          <Step
            label="渲染 DOCX"
            done={phase === 'done'}
            active={phase === 'rendering'}
            errored={phase === 'error' && Boolean(run) === false && phase === 'error' && !issues.length}
          />
        </ol>

        {phase === 'error' && (
          <div className={styles.alert} data-tone="error">
            <strong>{error ?? '校验未通过，请处理以下问题后重试。'}</strong>
            {issues.length > 0 && (
              <ul className={styles.issueList}>
                {issues.slice(0, 8).map((issue, i) => (
                  <li key={i}>
                    <code>{issue.code}</code> · {issue.message}
                    {issue.path && <span className={styles.issuePath}> ({issue.path})</span>}
                  </li>
                ))}
                {issues.length > 8 && <li>… 还有 {issues.length - 8} 个问题</li>}
              </ul>
            )}
          </div>
        )}

        {phase === 'done' && run && (
          <div className={styles.alert} data-tone="success">
            <strong>已生成 DOCX</strong>
            <div className={styles.runMeta}>
              OpenXML 校验：{run.openXmlValid ? '通过' : `${run.openXmlErrorCount} 个问题`} ·
              格式校验：{run.formatValid ? '通过' : `${run.formatErrorCount} 个问题`}
            </div>
            <a className={styles.downloadBtn} href={runDownloadUrl(run.runId)} download>
              下载 DOCX
            </a>
          </div>
        )}

        <footer className={styles.footer}>
          <button type="button" className={styles.btnGhost} onClick={onClose}>
            {phase === 'done' ? '完成' : '关闭'}
          </button>
        </footer>
      </div>
    </div>
  );
}

interface StepProps {
  label: string;
  done: boolean;
  active: boolean;
  errored: boolean;
}

function Step({ label, done, active, errored }: StepProps) {
  let state: 'pending' | 'active' | 'done' | 'error' = 'pending';
  if (errored) state = 'error';
  else if (done) state = 'done';
  else if (active) state = 'active';
  return (
    <li className={styles.step} data-state={state}>
      <span className={styles.stepIcon} aria-hidden>
        {state === 'done' ? '✓' : state === 'error' ? '✕' : state === 'active' ? '…' : '·'}
      </span>
      <span className={styles.stepLabel}>{label}</span>
    </li>
  );
}
