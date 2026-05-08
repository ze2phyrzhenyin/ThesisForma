import { useState, type ReactNode } from 'react';
import clsx from 'clsx';
import styles from './BlockShell.module.css';

interface BlockShellProps {
  selected?: boolean;
  onSelect?: () => void;
  onDelete?: () => void;
  onMoveUp?: () => void;
  onMoveDown?: () => void;
  canMoveUp?: boolean;
  canMoveDown?: boolean;
  /** Right-side label, e.g. block type ("段落", "标题 1", "图") */
  badge?: string;
  /** Bottom toolbar, contextual to block type (alignment, level, etc.) */
  toolbar?: ReactNode;
  children: ReactNode;
}

export function BlockShell({
  selected,
  onSelect,
  onDelete,
  onMoveUp,
  onMoveDown,
  canMoveUp = true,
  canMoveDown = true,
  badge,
  toolbar,
  children
}: BlockShellProps) {
  const [hover, setHover] = useState(false);
  const showControls = hover || selected;
  return (
    <div
      className={clsx(styles.shell, selected && styles.selected)}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      onPointerDown={onSelect}
    >
      <div className={styles.gutter} aria-hidden>
        {showControls && (
          <div className={styles.gutterControls}>
            <button
              type="button"
              className={styles.iconBtn}
              onClick={(e) => {
                e.stopPropagation();
                onMoveUp?.();
              }}
              disabled={!canMoveUp}
              aria-label="上移块"
              title="上移"
            >
              ↑
            </button>
            <button
              type="button"
              className={styles.iconBtn}
              onClick={(e) => {
                e.stopPropagation();
                onMoveDown?.();
              }}
              disabled={!canMoveDown}
              aria-label="下移块"
              title="下移"
            >
              ↓
            </button>
            <button
              type="button"
              className={styles.iconBtn}
              onClick={(e) => {
                e.stopPropagation();
                onDelete?.();
              }}
              aria-label="删除块"
              title="删除"
            >
              ✕
            </button>
          </div>
        )}
      </div>
      <div className={styles.body}>
        <div className={styles.bodyInner}>{children}</div>
        {selected && toolbar && <div className={styles.toolbar}>{toolbar}</div>}
      </div>
      {badge && <div className={styles.badge}>{badge}</div>}
    </div>
  );
}
