import { useEditorActions, useEditorStore } from '../EditorContext';
import { SECTION_META, SECTION_ORDER } from '../sectionMeta';
import { inlinesToPlainText } from '../inlines';
import type { HeadingBlock, SectionKind } from '@/types';
import styles from './SectionNav.module.css';

export function SectionNav() {
  const view = useEditorStore((s) => s.view);
  const sections = useEditorStore((s) => s.envelope.document.sections);
  const actions = useEditorActions();

  const presentKinds = new Set<SectionKind>(sections.map((s) => s.kind));

  const handleSectionClick = (kind: SectionKind) => {
    const idx = sections.findIndex((s) => s.kind === kind);
    if (idx >= 0) {
      actions.setView({ kind: 'section', sectionIndex: idx });
    } else {
      const newIdx = actions.ensureSection(kind);
      actions.setView({ kind: 'section', sectionIndex: newIdx });
    }
  };

  const activeSectionKind: SectionKind | null =
    view.kind === 'section' ? sections[view.sectionIndex]?.kind ?? null : null;

  const activeSection = activeSectionKind ? sections.find((s) => s.kind === activeSectionKind) : null;

  const headings: { level: number; text: string; sectionIndex: number; blockIndex: number }[] = [];
  if (activeSection) {
    const sIdx = sections.findIndex((s) => s.kind === activeSection.kind);
    activeSection.blocks.forEach((b, bIdx) => {
      if (b.type === 'heading') {
        const h = b as HeadingBlock;
        headings.push({
          level: h.level,
          text: inlinesToPlainText(h.inlines).trim() || '（无标题）',
          sectionIndex: sIdx,
          blockIndex: bIdx
        });
      }
    });
  }

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
          元数据
        </button>
        <button
          type="button"
          className={`${styles.item} ${view.kind === 'variables' ? styles.itemActive : ''}`}
          onClick={() => actions.setView({ kind: 'variables' })}
        >
          <span className={styles.itemDot} aria-hidden />
          模板变量
        </button>
        <button
          type="button"
          className={`${styles.item} ${view.kind === 'overrides' ? styles.itemActive : ''}`}
          onClick={() => actions.setView({ kind: 'overrides' })}
        >
          <span className={styles.itemDot} aria-hidden />
          格式覆盖
        </button>
      </div>

      <div className={styles.group}>
        <div className={styles.groupTitle}>章节</div>
        {SECTION_ORDER.map((kind) => {
          const meta = SECTION_META[kind];
          const present = presentKinds.has(kind);
          const isActive = activeSectionKind === kind;
          return (
            <button
              key={kind}
              type="button"
              className={`${styles.item} ${isActive ? styles.itemActive : ''} ${
                !present ? styles.itemMuted : ''
              }`}
              onClick={() => handleSectionClick(kind)}
              title={meta.description}
            >
              <span className={styles.itemDot} aria-hidden />
              {meta.label}
              {!present && <span className={styles.itemAdd}>＋</span>}
            </button>
          );
        })}
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
