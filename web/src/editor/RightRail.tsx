import { Suspense, lazy, useEffect, useState } from 'react';
import type { ApiIssue } from '@/types';
import styles from './RightRail.module.css';

export type DrawerKey = 'bibliography' | 'notes' | 'validation' | null;

const BibliographyPanel = lazy(() =>
  import('./panels/BibliographyPanel').then((module) => ({ default: module.BibliographyPanel }))
);
const NotesPanel = lazy(() => import('./panels/NotesPanel').then((module) => ({ default: module.NotesPanel })));
const ValidationPanel = lazy(() =>
  import('./panels/ValidationPanel').then((module) => ({ default: module.ValidationPanel }))
);

const DRAWERS: { key: NonNullable<DrawerKey>; label: string; icon: string }[] = [
  { key: 'bibliography', label: '参考文献', icon: '文' },
  { key: 'notes', label: '脚注尾注', icon: '注' },
  { key: 'validation', label: '校验问题', icon: '⚠' }
];

interface Props {
  open: DrawerKey;
  onChange(key: DrawerKey): void;
  validation: {
    isValid: boolean | null;
    issues: ApiIssue[];
    isRunning: boolean;
    lastCheckedAt: string | null;
  };
  onRunValidation(): void;
}

export function RightRail({ open, onChange, validation, onRunValidation }: Props) {
  const [internal, setInternal] = useState<DrawerKey>(open);
  useEffect(() => {
    setInternal(open);
  }, [open]);
  const setOpen = (k: DrawerKey) => onChange(k);

  // Allow Cmd/Ctrl+B to toggle bibliography
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'b') {
        e.preventDefault();
        setOpen(internal === 'bibliography' ? null : 'bibliography');
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [internal, onChange]);

  const issueCount = validation.issues.length;

  return (
    <aside className={styles.rail} data-open={open !== null}>
      <nav className={styles.iconStrip} aria-label="右侧工具">
        {DRAWERS.map((d) => (
          <button
            key={d.key}
            type="button"
            className={styles.iconBtn}
            data-active={open === d.key}
            onClick={() => setOpen(open === d.key ? null : d.key)}
            title={d.label}
            aria-label={d.label}
          >
            <span className={styles.iconGlyph} aria-hidden>
              {d.icon}
            </span>
            <span className={styles.iconLabel}>{d.label}</span>
            {d.key === 'validation' && issueCount > 0 && (
              <span className={styles.badge}>{issueCount}</span>
            )}
          </button>
        ))}
      </nav>
      {open !== null && (
        <div className={styles.drawer} role="region" aria-label="右侧抽屉">
          <Suspense fallback={<DrawerFallback label={DRAWERS.find((drawer) => drawer.key === open)?.label ?? '面板'} />}>
            {open === 'bibliography' && <BibliographyPanel />}
            {open === 'notes' && <NotesPanel />}
            {open === 'validation' && (
              <ValidationPanel
                issues={validation.issues}
                isValid={validation.isValid}
                isRunning={validation.isRunning}
                lastCheckedAt={validation.lastCheckedAt}
                onRun={onRunValidation}
              />
            )}
          </Suspense>
        </div>
      )}
    </aside>
  );
}

function DrawerFallback({ label }: { label: string }) {
  return (
    <div className={styles.drawerFallback} role="status">
      正在加载{label}…
    </div>
  );
}
