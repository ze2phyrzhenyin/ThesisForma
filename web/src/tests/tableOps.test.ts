import { describe, expect, it } from 'vitest';
import {
  deleteTableColumn,
  deleteTableRow,
  mergeTableRange,
  splitMergedCell,
  validateTableGrid
} from '@/editor/tableOps';
import type { TableBlock } from '@/types';

function table(): TableBlock {
  return {
    type: 'table',
    caption: '表 1',
    rows: [
      { cells: [{ text: 'A' }, { text: 'B' }, { text: 'C' }] },
      { cells: [{ text: 'D' }, { text: 'E' }, { text: 'F' }] },
      { cells: [{ text: 'G' }, { text: 'H' }, { text: 'I' }] }
    ]
  };
}

describe('tableOps', () => {
  it('merges adjacent cells horizontally with gridSpan', () => {
    const result = mergeTableRange(table(), { rowStart: 0, rowEnd: 0, colStart: 0, colEnd: 1 });
    expect(result.issues).toHaveLength(0);
    expect(result.table.rows[0].cells).toHaveLength(2);
    expect(result.table.rows[0].cells[0].gridSpan).toBe(2);
    expect(result.table.rows[0].cells[0].text).toBe('A\nB');
    expect(validateTableGrid(result.table)).toHaveLength(0);
  });

  it('merges cells vertically with restart and continue markers', () => {
    const result = mergeTableRange(table(), { rowStart: 0, rowEnd: 1, colStart: 0, colEnd: 0 });
    expect(result.issues).toHaveLength(0);
    expect(result.table.rows[0].cells[0].verticalMerge).toBe('restart');
    expect(result.table.rows[1].cells[0].verticalMerge).toBe('continue');
    expect(result.table.rows[0].cells[0].text).toBe('A\nD');
    expect(validateTableGrid(result.table)).toHaveLength(0);
  });

  it('merges rectangular ranges with gridSpan and verticalMerge', () => {
    const result = mergeTableRange(table(), { rowStart: 0, rowEnd: 1, colStart: 0, colEnd: 1 });
    expect(result.issues).toHaveLength(0);
    expect(result.table.rows[0].cells[0]).toMatchObject({ gridSpan: 2, verticalMerge: 'restart' });
    expect(result.table.rows[1].cells[0]).toMatchObject({ gridSpan: 2, verticalMerge: 'continue' });
    expect(result.table.rows[0].cells[0].text).toBe('A\nB\nD\nE');
    expect(validateTableGrid(result.table)).toHaveLength(0);
  });

  it('splits a selected merged cell back into grid cells', () => {
    const merged = mergeTableRange(table(), { rowStart: 0, rowEnd: 1, colStart: 0, colEnd: 1 }).table;
    const split = splitMergedCell(merged, { row: 0, col: 0 });
    expect(split.issues).toHaveLength(0);
    expect(split.table.rows[0].cells).toHaveLength(3);
    expect(split.table.rows[1].cells).toHaveLength(3);
    expect(split.table.rows[0].cells[0].verticalMerge).toBeUndefined();
    expect(validateTableGrid(split.table)).toHaveLength(0);
  });

  it('repairs dangling vertical merges after deleting rows and columns', () => {
    const merged = mergeTableRange(table(), { rowStart: 0, rowEnd: 1, colStart: 0, colEnd: 0 }).table;
    const withoutTop = deleteTableRow(merged, 0).table;
    expect(validateTableGrid(withoutTop)).toHaveLength(0);
    const withoutCol = deleteTableColumn(table(), 1).table;
    expect(validateTableGrid(withoutCol)).toHaveLength(0);
    expect(withoutCol.rows[0].cells.map((cell) => cell.text)).toEqual(['A', 'C']);
  });
});
