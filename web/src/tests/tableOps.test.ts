import { describe, expect, it } from 'vitest';
import {
  deleteTableColumn,
  deleteTableRow,
  getMergeRangeIssues,
  mergeTableRange,
  splitMergedCell,
  validateTableGrid
} from '@/editor/tableOps';
import { cleanThesisDocument } from '@/editor/documentContract';
import type { TableBlock, ThesisDocument } from '@/types';

function table(rows = 3, cols = 3): TableBlock {
  let code = 'A'.charCodeAt(0);
  return {
    type: 'table',
    caption: '表 1',
    rows: Array.from({ length: rows }, () => ({
      cells: Array.from({ length: cols }, () => ({ text: String.fromCharCode(code++) }))
    }))
  };
}

function docWithTable(block: TableBlock): ThesisDocument {
  return {
    schemaVersion: '1.1.0',
    metadata: {
      title: '测试',
      author: '作者',
      college: '学院',
      major: '专业',
      studentId: '1',
      advisor: '导师',
      date: '2026-05-08',
      language: 'zh-CN'
    },
    sections: [{ kind: 'body', blocks: [block] }]
  };
}

describe('tableOps', () => {
  it('merges a 2x2 table horizontally with a stable text order', () => {
    const result = mergeTableRange(table(2, 2), { rowStart: 0, rowEnd: 0, colStart: 0, colEnd: 1 });

    expect(result.issues).toHaveLength(0);
    expect(result.table.rows[0].cells).toHaveLength(1);
    expect(result.table.rows[0].cells[0]).toMatchObject({ gridSpan: 2, text: 'A\nB' });
    expect(validateTableGrid(result.table)).toHaveLength(0);
  });

  it('merges a 2x2 table vertically with restart and continue markers', () => {
    const result = mergeTableRange(table(2, 2), { rowStart: 0, rowEnd: 1, colStart: 0, colEnd: 0 });

    expect(result.issues).toHaveLength(0);
    expect(result.table.rows[0].cells[0]).toMatchObject({ verticalMerge: 'restart', text: 'A\nC' });
    expect(result.table.rows[1].cells[0]).toMatchObject({ verticalMerge: 'continue' });
    expect(validateTableGrid(result.table)).toHaveLength(0);
  });

  it('merges a 3x3 rectangular range into legal gridSpan and vMerge cells', () => {
    const result = mergeTableRange(table(3, 3), { rowStart: 0, rowEnd: 2, colStart: 0, colEnd: 2 });

    expect(result.issues).toHaveLength(0);
    expect(result.table.rows[0].cells[0]).toMatchObject({
      gridSpan: 3,
      verticalMerge: 'restart',
      text: 'A\nB\nC\nD\nE\nF\nG\nH\nI'
    });
    expect(result.table.rows[1].cells[0]).toMatchObject({ gridSpan: 3, verticalMerge: 'continue' });
    expect(result.table.rows[2].cells[0]).toMatchObject({ gridSpan: 3, verticalMerge: 'continue' });
    expect(validateTableGrid(result.table)).toHaveLength(0);
  });

  it('splits a merged rectangle without leaving vertical merge artifacts', () => {
    const merged = mergeTableRange(table(3, 3), { rowStart: 0, rowEnd: 1, colStart: 0, colEnd: 1 }).table;
    const split = splitMergedCell(merged, { row: 0, col: 0 });

    expect(split.issues).toHaveLength(0);
    expect(split.table.rows.map((row) => row.cells.length)).toEqual([3, 3, 3]);
    expect(split.table.rows.flatMap((row) => row.cells).some((cell) => cell.verticalMerge)).toBe(false);
    expect(validateTableGrid(split.table)).toHaveLength(0);
  });

  it('deletes a column inside a gridSpan and keeps row widths consistent', () => {
    const merged = mergeTableRange(table(2, 3), { rowStart: 0, rowEnd: 0, colStart: 0, colEnd: 1 }).table;
    const result = deleteTableColumn(merged, 0);

    expect(result.issues).toHaveLength(0);
    expect(result.table.rows[0].cells[0].gridSpan).toBeUndefined();
    expect(result.table.rows[0].cells.map((cell) => cell.text)).toEqual(['A\nB', 'C']);
    expect(validateTableGrid(result.table)).toHaveLength(0);
  });

  it('deletes a row from a vertical merge and repairs orphan continuations', () => {
    const merged = mergeTableRange(table(3, 2), { rowStart: 0, rowEnd: 2, colStart: 0, colEnd: 0 }).table;
    const result = deleteTableRow(merged, 0);

    expect(result.issues).toHaveLength(0);
    expect(result.table.rows[0].cells[0].verticalMerge).toBeUndefined();
    expect(validateTableGrid(result.table)).toHaveLength(0);
  });

  it('rejects a non-rectangular selection that cuts through an existing merge', () => {
    const merged = mergeTableRange(table(2, 3), { rowStart: 0, rowEnd: 0, colStart: 0, colEnd: 1 }).table;
    const issues = getMergeRangeIssues(merged, { rowStart: 0, rowEnd: 1, colStart: 1, colEnd: 1 });

    expect(issues.some((issue) => issue.code === 'table.selection.partialMerge')).toBe(true);
  });

  it('requires existing merged regions to be split before another merge', () => {
    const merged = mergeTableRange(table(2, 3), { rowStart: 0, rowEnd: 0, colStart: 0, colEnd: 1 }).table;
    const result = mergeTableRange(merged, { rowStart: 0, rowEnd: 0, colStart: 0, colEnd: 2 });

    expect(result.issues.some((issue) => issue.code === 'table.selection.existingHorizontalMerge')).toBe(true);
  });

  it('handles empty, single-row, and single-column table boundaries', () => {
    expect(validateTableGrid({ type: 'table', caption: '空', rows: [] })).toEqual([
      expect.objectContaining({ code: 'table.rows.empty', severity: 'error' })
    ]);

    const oneRow = mergeTableRange(table(1, 2), { rowStart: 0, rowEnd: 0, colStart: 0, colEnd: 1 });
    expect(oneRow.issues).toHaveLength(0);

    const oneCol = mergeTableRange(table(2, 1), { rowStart: 0, rowEnd: 1, colStart: 0, colEnd: 0 });
    expect(oneCol.issues).toHaveLength(0);
  });

  it('does not leak UI-only table selection fields into exported document JSON', () => {
    const dirtyTable = {
      ...mergeTableRange(table(2, 2), { rowStart: 0, rowEnd: 0, colStart: 0, colEnd: 1 }).table,
      selection: { rowStart: 0, rowEnd: 0, colStart: 0, colEnd: 1 }
    } as TableBlock & { selection: unknown };

    const exported = cleanThesisDocument(docWithTable(dirtyTable));
    const exportedTable = exported.sections[0].blocks[0] as TableBlock;

    expect('selection' in exportedTable).toBe(false);
    expect(exportedTable.rows[0].cells[0].gridSpan).toBe(2);
  });
});
