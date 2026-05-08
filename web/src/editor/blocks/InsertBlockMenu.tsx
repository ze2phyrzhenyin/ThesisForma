import { useEffect, useRef, useState } from 'react';
import type { BlockType } from '@/types';
import styles from './InsertBlockMenu.module.css';

interface BlockOption {
  type: BlockType;
  label: string;
  description: string;
  hint?: string;
}

const BLOCK_OPTIONS: BlockOption[] = [
  { type: 'paragraph', label: '段落', description: '正文段落', hint: 'p' },
  { type: 'heading', label: '标题', description: '一至六级标题', hint: 'h' },
  { type: 'list', label: '列表', description: '有序 / 无序列表', hint: 'list' },
  { type: 'quote', label: '引文', description: '块级引用' },
  { type: 'figure', label: '图', description: '上传图片 + 题注' },
  { type: 'table', label: '表', description: '行列网格 + 题注' },
  { type: 'equation', label: '公式', description: 'LaTeX 或 OMML' },
  { type: 'pageBreak', label: '分页', description: '强制分页' },
  { type: 'sectionBreak', label: '分节', description: '页面属性变化' }
];

interface Props {
  /** Which block types should be enabled. Phase 1: paragraph, heading, list, quote, pageBreak, sectionBreak */
  enabled: ReadonlySet<BlockType>;
  onPick(type: BlockType): void;
  className?: string;
  /** 'inline' renders as a +-button between blocks; 'large' renders as a 'press / for menu' hint at the empty section bottom */
  variant?: 'inline' | 'large';
}

export function InsertBlockMenu({ enabled, onPick, className, variant = 'inline' }: Props) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onClick = (e: MouseEvent) => {
      if (!containerRef.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onClick);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onClick);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  return (
    <div ref={containerRef} className={`${styles.wrap} ${className ?? ''}`} data-variant={variant}>
      <button
        type="button"
        className={variant === 'large' ? styles.largeTrigger : styles.inlineTrigger}
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
      >
        {variant === 'large' ? '＋ 添加内容…' : '＋'}
      </button>

      {open && (
        <div className={styles.menu} role="menu">
          {BLOCK_OPTIONS.map((opt) => {
            const isEnabled = enabled.has(opt.type);
            return (
              <button
                key={opt.type}
                role="menuitem"
                type="button"
                className={styles.item}
                disabled={!isEnabled}
                onClick={() => {
                  onPick(opt.type);
                  setOpen(false);
                }}
              >
                <span className={styles.itemLabel}>{opt.label}</span>
                <span className={styles.itemDesc}>
                  {isEnabled ? opt.description : '阶段 2 / 后续上线'}
                </span>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
