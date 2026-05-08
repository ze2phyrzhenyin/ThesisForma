import { useMemo, useState } from 'react';
import { useEditorActions } from '../EditorContext';
import { BlockShell } from './BlockShell';
import type { ApiIssue, TableBlock as TableBlockData, TableCell } from '@/types';
import {
  addTableColumn,
  addTableRow,
  deleteTableColumn,
  deleteTableRow,
  mergeTableRange,
  normalizeRange,
  splitMergedCell,
  tableGridWidth,
  validateTableGrid,
  type CellAddress,
  type CellRange
} from '../tableOps';
import styles from './blocks.module.css';
import tStyles from './TableBlock.module.css';

interface Props {
  block: TableBlockData;
  sectionIndex: number;
  blockIndex: number;
  selected: boolean;
  totalBlocks: number;
}

export function TableBlock({ block, sectionIndex, blockIndex, selected, totalBlocks }: Props) {
  const actions = useEditorActions();
  const [anchor, setAnchor] = useState<CellAddress | null>(null);
  const [range, setRange] = useState<CellRange | null>(null);
  const [opIssues, setOpIssues] = useState<ApiIssue[]>([]);
  const cols = tableGridWidth(block);
  const gridIssues = useMemo(() => validateTableGrid(block), [block]);

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
      Object.assign(b, addTableRow(b, afterRowIdx));
    });

  const deleteRow = (rowIdx: number) =>
    update((b) => {
      const result = deleteTableRow(b, rowIdx);
      setOpIssues(result.issues);
      Object.assign(b, result.table);
    });

  const addCol = (afterColIdx: number) =>
    update((b) => {
      Object.assign(b, addTableColumn(b, afterColIdx));
    });

  const deleteCol = (colIdx: number) =>
    update((b) => {
      const result = deleteTableColumn(b, colIdx);
      setOpIssues(result.issues);
      Object.assign(b, result.table);
    });

  const selectCell = (address: CellAddress) => {
    if (!anchor) {
      setAnchor(address);
      setRange(normalizeRange(address, address));
      setOpIssues([]);
      return;
    }
    const next = normalizeRange(anchor, address);
    setRange(next);
    setOpIssues([]);
  };

  const clearSelection = () => {
    setAnchor(null);
    setRange(null);
    setOpIssues([]);
  };

  const mergeSelection = () => {
    if (!range) return;
    update((b) => {
      const result = mergeTableRange(b, range);
      setOpIssues(result.issues);
      Object.assign(b, result.table);
    });
  };

  const splitSelection = () => {
    const target = range ? { row: range.rowStart, col: range.colStart } : anchor;
    if (!target) return;
    update((b) => {
      const result = splitMergedCell(b, target);
      setOpIssues(result.issues);
      Object.assign(b, result.table);
    });
  };

  const hasMergeSelection =
    Boolean(range) && (range!.rowStart !== range!.rowEnd || range!.colStart !== range!.colEnd);

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
          <span className={styles.toolbarSeparator}>·</span>
          <button
            type="button"
            className={styles.toolbarBtn}
            onClick={mergeSelection}
            disabled={!hasMergeSelection}
          >
            合并所选
          </button>
          <button
            type="button"
            className={styles.toolbarBtn}
            onClick={splitSelection}
            disabled={!range}
          >
            拆分单元格
          </button>
          <button type="button" className={styles.toolbarBtn} onClick={clearSelection} disabled={!range}>
            清除选择
          </button>
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
          <label className={tStyles.repeatControl}>
            表头行
            <input
              type="number"
              min={0}
              max={20}
              value={block.repeatHeaderRows ?? block.rows.filter((row) => row.isHeader).length}
              onChange={(e) =>
                update((b) => {
                  const n = Number(e.target.value);
                  if (Number.isFinite(n) && n >= 0) b.repeatHeaderRows = Math.min(20, Math.floor(n));
                })
              }
            />
          </label>
        </div>

        {(gridIssues.length > 0 || opIssues.length > 0) && (
          <div className={tStyles.issues} role="alert">
            {[...opIssues, ...gridIssues].slice(0, 4).map((issue, i) => (
              <div key={`${issue.code}-${i}`}>{issue.message}</div>
            ))}
          </div>
        )}

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
                  {row.cells.map((cell, cIdx) => {
                    const colStart = row.cells
                      .slice(0, cIdx)
                      .reduce((sum, current) => sum + (current.gridSpan ?? 1), 0);
                    const colEnd = colStart + (cell.gridSpan ?? 1) - 1;
                    const selectedCell =
                      range &&
                      rIdx >= range.rowStart &&
                      rIdx <= range.rowEnd &&
                      colEnd >= range.colStart &&
                      colStart <= range.colEnd;
                    return (
                      <td
                        key={cell.id ?? cIdx}
                        className={tStyles.cell}
                        data-selected={selectedCell === true}
                        data-vmerge={cell.verticalMerge ?? 'none'}
                        colSpan={cell.gridSpan ?? 1}
                        onClick={(e) => {
                          e.stopPropagation();
                          selectCell({ row: rIdx, col: colStart });
                        }}
                      >
                        <textarea
                          rows={1}
                          className={tStyles.cellInput}
                          value={cell.text ?? ''}
                          placeholder={cell.verticalMerge === 'continue' ? '纵向合并延续' : '…'}
                          disabled={cell.verticalMerge === 'continue'}
                          onChange={(e) => {
                            update((b) => {
                              const r = b.rows[rIdx];
                              if (!r) return;
                              const target = r.cells[cIdx];
                              target.text = e.target.value;
                              delete target.blocks;
                            });
                            updateCell(rIdx, cIdx, () => undefined);
                          }}
                        />
                        {(cell.gridSpan ?? 1) > 1 && (
                          <span className={tStyles.mergeBadge}>×{cell.gridSpan}</span>
                        )}
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
                      title="禁止跨页断行"
                      onClick={() =>
                        update((b) => {
                          const r = b.rows[rIdx];
                          if (!r) return;
                          if (r.cantSplit) delete r.cantSplit;
                          else r.cantSplit = true;
                        })
                      }
                      className={tStyles.miniBtn}
                      data-active={row.cantSplit === true}
                    >
                      S
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
          {range && (
            <span className={tStyles.selectionHint}>
              已选 {range.rowEnd - range.rowStart + 1} × {range.colEnd - range.colStart + 1}
            </span>
          )}
        </div>
      </div>
    </BlockShell>
  );
}
