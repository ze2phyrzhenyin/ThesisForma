import { useEffect, useRef, useState } from 'react';
import { useEditorActions, useEditorStore } from '../EditorContext';
import { SECTION_META, SECTION_ORDER } from '../sectionMeta';
import { inlinesToPlainText } from '../inlines';
import type { HeadingBlock, SectionKind } from '@/types';
import styles from './SectionNav.module.css';

export function SectionNav() {
  const view = useEditorStore((s) => s.view);
  const sections = useEditorStore((s) => s.envelope.document.sections);
  const actions = useEditorActions();

  const [renameTarget, setRenameTarget] = useState<number | null>(null);
  const [renameDraft, setRenameDraft] = useState('');
  const [addOpen, setAddOpen] = useState(false);
  const addRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!addOpen) return;
    const onClick = (e: MouseEvent) => {
      if (!addRef.current?.contains(e.target as Node)) setAddOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setAddOpen(false);
    };
    document.addEventListener('mousedown', onClick);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onClick);
      document.removeEventListener('keydown', onKey);
    };
  }, [addOpen]);

  const activeSectionIndex = view.kind === 'section' ? view.sectionIndex : -1;
  const activeSection = activeSectionIndex >= 0 ? sections[activeSectionIndex] : null;

  const headings: { level: number; text: string; sectionIndex: number; blockIndex: number }[] = [];
  if (activeSection) {
    activeSection.blocks.forEach((b, bIdx) => {
      if (b.type === 'heading') {
        const h = b as HeadingBlock;
        headings.push({
          level: h.level,
          text: inlinesToPlainText(h.inlines).trim() || '（无标题）',
          sectionIndex: activeSectionIndex,
          blockIndex: bIdx
        });
      }
    });
  }

  const startRename = (idx: number) => {
    const s = sections[idx];
    if (!s) return;
    setRenameTarget(idx);
    setRenameDraft(s.title ?? SECTION_META[s.kind].label);
  };

  const commitRename = () => {
    if (renameTarget === null) return;
    actions.renameSection(renameTarget, renameDraft);
    setRenameTarget(null);
  };

  const cancelRename = () => {
    setRenameTarget(null);
    setRenameDraft('');
  };

  const handleDelete = (idx: number) => {
    const section = sections[idx];
    if (!section) return;
    const label = section.title ?? SECTION_META[section.kind].label;
    const ok = window.confirm(`删除章节「${label}」？该章节内的所有内容都会一起删除。`);
    if (!ok) return;
    actions.removeSection(idx);
  };

  const handleAdd = (kind: SectionKind) => {
    const insertAt =
      activeSectionIndex >= 0 ? activeSectionIndex + 1 : sections.length;
    const newIdx = actions.addSection(kind, insertAt);
    actions.setView({ kind: 'section', sectionIndex: newIdx });
    setAddOpen(false);
  };

  return (
    <nav className={styles.nav} aria-label="文档导航">
      <div className={styles.group}>
        <div className={styles.groupTitle}>信息</div>
        <button
          type="button"
          className={`${styles.item} ${view.kind === 'metadata' ? styles.itemActive : ''}`}
          onClick={() => actions.setView({ kind: 'metadata' })}
        >
          <span className={styles.itemDot} aria-hidden />
          <span className={styles.itemLabel}>元数据</span>
        </button>
        <button
          type="button"
          className={`${styles.item} ${view.kind === 'variables' ? styles.itemActive : ''}`}
          onClick={() => actions.setView({ kind: 'variables' })}
        >
          <span className={styles.itemDot} aria-hidden />
          <span className={styles.itemLabel}>模板变量</span>
        </button>
        <button
          type="button"
          className={`${styles.item} ${view.kind === 'overrides' ? styles.itemActive : ''}`}
          onClick={() => actions.setView({ kind: 'overrides' })}
        >
          <span className={styles.itemDot} aria-hidden />
          <span className={styles.itemLabel}>格式覆盖</span>
        </button>
      </div>

      <div className={styles.group}>
        <div className={styles.groupTitle}>章节</div>
        {sections.length === 0 && (
          <div className={styles.emptyHint}>还没有章节，点下方「添加章节」开始。</div>
        )}
        {sections.map((section, idx) => {
          const meta = SECTION_META[section.kind];
          const isActive = activeSectionIndex === idx;
          const isRenaming = renameTarget === idx;
          const displayName = section.title ?? meta.label;

          return (
            <div
              key={section.id ?? `${section.kind}-${idx}`}
              className={`${styles.row} ${isActive ? styles.rowActive : ''}`}
              data-renaming={isRenaming}
            >
              {isRenaming ? (
                <input
                  className={styles.renameInput}
                  autoFocus
                  value={renameDraft}
                  onChange={(e) => setRenameDraft(e.target.value)}
                  onBlur={commitRename}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      e.preventDefault();
                      commitRename();
                    } else if (e.key === 'Escape') {
                      e.preventDefault();
                      cancelRename();
                    }
                  }}
                />
              ) : (
                <button
                  type="button"
                  className={styles.rowMain}
                  onClick={() => actions.setView({ kind: 'section', sectionIndex: idx })}
                  onDoubleClick={(e) => {
                    e.preventDefault();
                    startRename(idx);
                  }}
                  title={`${meta.label}（双击重命名）`}
                >
                  <span className={styles.itemDot} aria-hidden />
                  <span className={styles.itemLabel}>{displayName}</span>
                  {section.title && section.title !== meta.label && (
                    <span className={styles.kindHint}>{meta.label}</span>
                  )}
                </button>
              )}

              {!isRenaming && (
                <div className={styles.rowActions}>
                  <button
                    type="button"
                    className={styles.iconBtn}
                    onClick={() => actions.moveSection(idx, idx - 1)}
                    disabled={idx === 0}
                    title="上移"
                    aria-label="上移"
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    className={styles.iconBtn}
                    onClick={() => actions.moveSection(idx, idx + 1)}
                    disabled={idx === sections.length - 1}
                    title="下移"
                    aria-label="下移"
                  >
                    ↓
                  </button>
                  <button
                    type="button"
                    className={styles.iconBtn}
                    onClick={() => startRename(idx)}
                    title="重命名"
                    aria-label="重命名"
                  >
                    ✎
                  </button>
                  <button
                    type="button"
                    className={styles.iconBtn}
                    onClick={() => handleDelete(idx)}
                    title="删除"
                    aria-label="删除"
                    data-danger
                  >
                    ✕
                  </button>
                </div>
              )}
            </div>
          );
        })}

        <div className={styles.addWrap} ref={addRef}>
          <button
            type="button"
            className={styles.addTrigger}
            onClick={() => setAddOpen((v) => !v)}
            aria-haspopup="menu"
            aria-expanded={addOpen}
          >
            ＋ 添加章节
          </button>
          {addOpen && (
            <div className={styles.addMenu} role="menu">
              {SECTION_ORDER.map((kind) => {
                const meta = SECTION_META[kind];
                const existing = sections.filter((s) => s.kind === kind).length;
                return (
                  <button
                    key={kind}
                    type="button"
                    role="menuitem"
                    className={styles.addItem}
                    onClick={() => handleAdd(kind)}
                  >
                    <span className={styles.addItemLabel}>{meta.label}</span>
                    <span className={styles.addItemDesc}>
                      {existing > 0 ? `已有 ${existing} 个 · ${meta.description}` : meta.description}
                    </span>
                  </button>
                );
              })}
            </div>
          )}
        </div>
      </div>

      {headings.length > 0 && (
        <div className={styles.group}>
          <div className={styles.groupTitle}>大纲</div>
          {headings.map((h, i) => (
            <button
              key={`${h.sectionIndex}-${h.blockIndex}-${i}`}
              type="button"
              className={styles.outlineItem}
              data-level={h.level}
              onClick={() => {
                actions.setView({ kind: 'section', sectionIndex: h.sectionIndex });
                actions.selectBlock(h.sectionIndex, h.blockIndex);
                queueMicrotask(() => {
                  const el = document.querySelector<HTMLElement>(
                    `[data-block-index="${h.sectionIndex}-${h.blockIndex}"]`
                  );
                  el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                });
              }}
              title={h.text}
            >
              {h.text}
            </button>
          ))}
        </div>
      )}
    </nav>
  );
}
