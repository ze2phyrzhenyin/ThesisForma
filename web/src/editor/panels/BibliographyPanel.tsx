import { useMemo } from 'react';
import { useEditorActions, useEditorStore } from '../EditorContext';
import { newBlockId } from '../ids';
import type { BibliographyBlock, BibliographyEntry } from '@/types';
import drawerStyles from './drawer.module.css';

/**
 * Bibliography lives inside a `bibliography` section as a single `bibliography` block.
 * If neither exists, we lazy-create them on first add.
 */
export function BibliographyPanel() {
  const sections = useEditorStore((s) => s.envelope.document.sections);
  const actions = useEditorActions();

  const { sectionIndex, blockIndex, entries } = useMemo(() => {
    const sIdx = sections.findIndex((s) => s.kind === 'bibliography');
    if (sIdx < 0) return { sectionIndex: -1, blockIndex: -1, entries: [] as BibliographyEntry[] };
    const bIdx = sections[sIdx].blocks.findIndex((b) => b.type === 'bibliography');
    if (bIdx < 0) return { sectionIndex: sIdx, blockIndex: -1, entries: [] as BibliographyEntry[] };
    const block = sections[sIdx].blocks[bIdx] as BibliographyBlock;
    return { sectionIndex: sIdx, blockIndex: bIdx, entries: block.entries };
  }, [sections]);

  const ensureBlock = (): { sIdx: number; bIdx: number } => {
    let sIdx = sectionIndex;
    if (sIdx < 0) {
      sIdx = actions.ensureSection('bibliography');
    }
    let bIdx = blockIndex;
    if (bIdx < 0) {
      const block: BibliographyBlock = {
        type: 'bibliography',
        id: newBlockId('bib'),
        entries: []
      };
      actions.appendBlock(sIdx, block);
      bIdx = 0;
    }
    return { sIdx, bIdx };
  };

  const addEntry = () => {
    const { sIdx, bIdx } = ensureBlock();
    const newEntry: BibliographyEntry = {
      id: `ref-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 5)}`,
      text: ''
    };
    actions.updateBlock(sIdx, bIdx, (b) => {
      if (b.type !== 'bibliography') return;
      b.entries.push(newEntry);
    });
  };

  const updateEntry = (idx: number, patch: Partial<BibliographyEntry>) => {
    if (sectionIndex < 0 || blockIndex < 0) return;
    actions.updateBlock(sectionIndex, blockIndex, (b) => {
      if (b.type !== 'bibliography') return;
      Object.assign(b.entries[idx], patch);
    });
  };

  const removeEntry = (idx: number) => {
    if (sectionIndex < 0 || blockIndex < 0) return;
    actions.updateBlock(sectionIndex, blockIndex, (b) => {
      if (b.type !== 'bibliography') return;
      b.entries.splice(idx, 1);
    });
  };

  return (
    <div className={drawerStyles.panel}>
      <header className={drawerStyles.header}>
        <h2 className={drawerStyles.title}>参考文献库</h2>
        <button type="button" className={drawerStyles.headerBtn} onClick={addEntry}>
          ＋ 新增
        </button>
      </header>

      {entries.length === 0 ? (
        <div className={drawerStyles.empty}>
          还没有条目。点击「新增」添加，正文里通过 <code>@</code> 选用。
        </div>
      ) : (
        <ul className={drawerStyles.list}>
          {entries.map((entry, i) => (
            <li key={entry.id || i} className={drawerStyles.item}>
              <input
                className={drawerStyles.idInput}
                value={entry.id}
                placeholder="ref-id"
                onChange={(e) => updateEntry(i, { id: e.target.value })}
                title="引用键，正文中 [@id] 使用"
              />
              <textarea
                className={drawerStyles.textInput}
                value={entry.text}
                rows={3}
                placeholder="作者. 题名. 出版社, 年, 页码."
                onChange={(e) => updateEntry(i, { text: e.target.value })}
              />
              <button
                type="button"
                className={drawerStyles.removeBtn}
                onClick={() => removeEntry(i)}
                title="删除"
                aria-label={`删除 ${entry.id}`}
              >
                ✕
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
