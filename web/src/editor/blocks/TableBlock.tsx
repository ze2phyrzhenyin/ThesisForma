import { useEditorActions } from '../EditorContext';
import { BlockShell } from './BlockShell';
import { newBlockId } from '../ids';
import type { TableBlock as TableBlockData, TableRow, TableCell } from '@/types';
import styles from './blocks.module.css';
import tStyles from './TableBlock.module.css';

interface Props {
  block: TableBlockData;
  sectionIndex: number;
  blockIndex: number;
  selected: boolean;
  totalBlocks: number;
}

function newRow(cellCount: number): TableRow {
  return {
    id: newBlockId('tr'),
    cells: Array.from({ length: cellCount }, () => ({ id: newBlockId('tc'), text: '' }))
  };
}

function maxColCount(rows: TableRow[]): number {
  return rows.reduce((max, r) => Math.max(max, r.cells.length), 0);
}

export function TableBlock({ block, sectionIndex, blockIndex, selected, totalBlocks }: Props) {
  const actions = useEditorActions();
  const cols = maxColCount(block.rows);

  const update = (updater: (b: TableBlockData) => void) =>
    actions.updateBlock(sectionIndex, blockIndex, (b) => {
      if (b.type !== 'table') return;
      updater(b);
    });

  const updateCell = (rowIdx: number, cellIdx: number, mut: (cell: TableCell) => void) =>
    update((b) => {
      const cell = b.rows[rowIdx]?.cells[cellIdx];
      if (cell) mut(cell);
    });

  const addRow = (afterRowIdx: number) =>
    update((b) => {
      const cellCount = Math.max(maxColCount(b.rows), 1);
      b.rows.splice(afterRowIdx + 1, 0, newRow(cellCount));
    });

  const deleteRow = (rowIdx: number) =>
    update((b) => {
      if (b.rows.length > 1) b.rows.splice(rowIdx, 1);
    });

  const addCol = (afterColIdx: number) =>
    update((b) => {
      for (const row of b.rows) {
        row.cells.splice(afterColIdx + 1, 0, { id: newBlockId('tc'), text: '' });
      }
    });

  const deleteCol = (colIdx: number) =>
    update((b) => {
      const cellCount = maxColCount(b.rows);
      if (cellCount <= 1) return;
      for (const row of b.rows) {
        if (row.cells.length > colIdx) row.cells.splice(colIdx, 1);
      }
    });

  return (
    <BlockShell
      selected={selected}
      onSelect={() => actions.selectBlock(sectionIndex, blockIndex)}
      onDelete={() => actions.deleteBlock(sectionIndex, blockIndex)}
      onMoveUp={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex - 1)}
      onMoveDown={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex + 1)}
      canMoveUp={blockIndex > 0}
      canMoveDown={blockIndex < totalBlocks - 1}
      badge="表"
      toolbar={
        <>
          <span className={styles.toolbarLabel}>样式：</span>
          {(['normal', 'threeLine', 'custom'] as const).map((s) => (
            <button
              key={s}
              type="button"
              className={styles.toolbarBtn}
              data-active={(block.style ?? 'normal') === s}
              onClick={() =>
                update((b) => {
                  if (s === 'normal') delete b.style;
                  else b.style = s;
                })
              }
            >
              {s === 'normal' ? '普通' : s === 'threeLine' ? '三线' : '自定义'}
            </button>
          ))}
          <span className={styles.toolbarSeparator}>·</span>
          <span className={styles.toolbarLabel}>题注：</span>
          {(['before', 'after'] as const).map((p) => (
            <button
              key={p}
              type="button"
              className={styles.toolbarBtn}
              data-active={(block.captionPosition ?? 'before') === p}
              onClick={() =>
                update((b) => {
                  if (p === 'before') delete b.captionPosition;
                  else b.captionPosition = p;
                })
              }
            >
              {p === 'before' ? '前' : '后'}
            </button>
          ))}
        </>
      }
    >
      <div className={tStyles.tableBlock}>
        <div className={tStyles.captionRow}>
          <span className={tStyles.captionLabel}>题注</span>
          <input
            className={tStyles.captionInput}
            value={block.caption}
            placeholder="表 1-1 …"
            onChange={(e) => update((b) => (b.caption = e.target.value))}
          />
        </div>

        <div className={tStyles.tableWrap} data-style={block.style ?? 'normal'}>
          <table className={tStyles.table}>
            <colgroup>
              {Array.from({ length: cols }).map((_, c) => (
                <col key={c} />
              ))}
            </colgroup>
            <thead>
              <tr>
                {Array.from({ length: cols }).map((_, c) => (
                  <th key={c} className={tStyles.colHeader}>
                    <span className={tStyles.colLabel}>列 {c + 1}</span>
                    <span className={tStyles.colActions}>
                      <button
                        type="button"
                        title="左侧插入列"
                        onClick={() => addCol(c - 1)}
                        className={tStyles.miniBtn}
                      >
                        ←＋
                      </button>
                      <button
                        type="button"
                        title="右侧插入列"
                        onClick={() => addCol(c)}
                        className={tStyles.miniBtn}
                      >
                        ＋→
                      </button>
                      <button
                        type="button"
                        title="删除此列"
                        onClick={() => deleteCol(c)}
                        className={tStyles.miniBtn}
                      >
                        ✕
                      </button>
                    </span>
                  </th>
                ))}
                <th />
              </tr>
            </thead>
            <tbody>
              {block.rows.map((row, rIdx) => (
                <tr key={row.id ?? rIdx} className={row.isHeader ? tStyles.headerRow : ''}>
                  {Array.from({ length: cols }).map((_, cIdx) => {
                    const cell = row.cells[cIdx] ?? { text: '' };
                    return (
                      <td key={cell.id ?? cIdx} className={tStyles.cell}>
                        <textarea
                          rows={1}
                          className={tStyles.cellInput}
                          value={cell.text ?? ''}
                          placeholder="…"
                          onChange={(e) => {
                            // Ensure we have a real cell at this column.
                            update((b) => {
                              const r = b.rows[rIdx];
                              if (!r) return;
                              while (r.cells.length <= cIdx) {
                                r.cells.push({ id: newBlockId('tc'), text: '' });
                              }
                              const target = r.cells[cIdx];
                              target.text = e.target.value;
                              delete target.blocks;
                            });
                            updateCell(rIdx, cIdx, () => undefined);
                          }}
                        />
                      </td>
                    );
                  })}
                  <td className={tStyles.rowActions}>
                    <button
                      type="button"
                      title="行表头标记"
                      onClick={() =>
                        update((b) => {
                          const r = b.rows[rIdx];
                          if (!r) return;
                          if (r.isHeader) delete r.isHeader;
                          else r.isHeader = true;
                        })
                      }
                      className={tStyles.miniBtn}
                      data-active={row.isHeader === true}
                    >
                      H
                    </button>
                    <button
                      type="button"
                      title="下方插入行"
                      onClick={() => addRow(rIdx)}
                      className={tStyles.miniBtn}
                    >
                      ＋
                    </button>
                    <button
                      type="button"
                      title="删除此行"
                      onClick={() => deleteRow(rIdx)}
                      className={tStyles.miniBtn}
                    >
                      ✕
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className={tStyles.bottomActions}>
          <button type="button" className={tStyles.addBtn} onClick={() => addRow(block.rows.length - 1)}>
            ＋ 添加行
          </button>
          <button type="button" className={tStyles.addBtn} onClick={() => addCol(cols - 1)}>
            ＋ 添加列
          </button>
        </div>
      </div>
    </BlockShell>
  );
}
