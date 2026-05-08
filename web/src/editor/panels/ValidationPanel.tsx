import { useEditorActions, useEditorStore } from '../EditorContext';
import drawerStyles from './drawer.module.css';
import styles from './ValidationPanel.module.css';
import type { ApiIssue } from '@/types';

interface Props {
  issues: ApiIssue[];
  isValid: boolean | null;
  isRunning: boolean;
  lastCheckedAt: string | null;
  onRun(): void;
}

export function ValidationPanel({ issues, isValid, isRunning, lastCheckedAt, onRun }: Props) {
  const sections = useEditorStore((s) => s.envelope.document.sections);
  const actions = useEditorActions();

  const focusByPath = (path: string | null | undefined) => {
    if (!path) return;
    // Crude path resolver: $.sections[N].blocks[M].…
    const m = path.match(/sections\[(\d+)\](?:\.blocks\[(\d+)\])?/);
    if (!m) return;
    const sIdx = Number(m[1]);
    const bIdx = m[2] !== undefined ? Number(m[2]) : null;
    if (Number.isFinite(sIdx) && sections[sIdx]) {
      actions.setView({ kind: 'section', sectionIndex: sIdx });
      if (bIdx !== null) {
        actions.selectBlock(sIdx, bIdx);
        queueMicrotask(() => {
          const el = document.querySelector<HTMLElement>(
            `[data-block-index="${sIdx}-${bIdx}"]`
          );
          el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        });
      }
    }
  };

  return (
    <div className={drawerStyles.panel}>
      <header className={drawerStyles.header}>
        <h2 className={drawerStyles.title}>校验问题</h2>
        <button type="button" className={drawerStyles.headerBtn} onClick={onRun} disabled={isRunning}>
          {isRunning ? '校验中…' : '重新校验'}
        </button>
      </header>

      <div className={styles.statusBar} data-status={statusOf(isValid)}>
        {isValid === null && '尚未校验'}
        {isValid === true && '通过 · 无问题'}
        {isValid === false && `共 ${issues.length} 个问题`}
        {lastCheckedAt && <span className={styles.checkedAt}>· {lastCheckedAt}</span>}
      </div>

      {isValid === false && issues.length === 0 && (
        <div className={drawerStyles.empty}>校验失败但无具体问题；查看模板与 schema 版本。</div>
      )}

      {issues.length > 0 && (
        <ul className={styles.list}>
          {issues.map((issue, i) => (
            <li key={i} className={styles.item} data-severity={issue.severity}>
              <div className={styles.itemHeader}>
                <span className={styles.severity}>{labelSeverity(issue.severity)}</span>
                <code className={styles.code}>{issue.code}</code>
              </div>
              <div className={styles.message}>{issue.message}</div>
              {issue.path && (
                <button
                  type="button"
                  className={styles.pathBtn}
                  onClick={() => focusByPath(issue.path)}
                  title="跳转到对应位置"
                >
                  {issue.path} →
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function statusOf(isValid: boolean | null) {
  if (isValid === null) return 'idle';
  return isValid ? 'ok' : 'error';
}

function labelSeverity(s: string) {
  switch (s) {
    case 'error':
      return '错误';
    case 'warning':
      return '警告';
    case 'info':
      return '提示';
    default:
      return s;
  }
}
