import type { ApiIssue, TableBlock, TableCell, TableRow } from '@/types';
import { newBlockId } from './ids';

export interface CellAddress {
  row: number;
  col: number;
}

export interface CellRange {
  rowStart: number;
  rowEnd: number;
  colStart: number;
  colEnd: number;
}

export interface LocatedCell {
  row: number;
  cellIndex: number;
  colStart: number;
  colEnd: number;
  cell: TableCell;
}

interface ActiveVerticalMerge {
  startCol: number;
  endCol: number;
}

export interface TableOpResult {
  table: TableBlock;
  issues: ApiIssue[];
}

export function normalizeRange(a: CellAddress, b: CellAddress): CellRange {
  return {
    rowStart: Math.min(a.row, b.row),
    rowEnd: Math.max(a.row, b.row),
    colStart: Math.min(a.col, b.col),
    colEnd: Math.max(a.col, b.col)
  };
}

export function gridWidth(row: TableRow): number {
  return row.cells.reduce((sum, cell) => sum + cellSpan(cell), 0);
}

export function tableGridWidth(table: TableBlock): number {
  return table.rows.reduce((max, row) => Math.max(max, gridWidth(row)), 0);
}

export function locateCell(row: TableRow | undefined, logicalCol: number): LocatedCell | null {
  if (!row) return null;
  let col = 0;
  for (let cellIndex = 0; cellIndex < row.cells.length; cellIndex++) {
    const cell = row.cells[cellIndex];
    const span = cellSpan(cell);
    const end = col + span - 1;
    if (logicalCol >= col && logicalCol <= end) {
      return { row: -1, cellIndex, colStart: col, colEnd: end, cell };
    }
    col += span;
  }
  return null;
}

export function locateCellsForRange(table: TableBlock, range: CellRange): LocatedCell[] {
  const located: LocatedCell[] = [];
  for (let rowIndex = range.rowStart; rowIndex <= range.rowEnd; rowIndex++) {
    const row = table.rows[rowIndex];
    if (!row) continue;
    let col = 0;
    for (let cellIndex = 0; cellIndex < row.cells.length; cellIndex++) {
      const cell = row.cells[cellIndex];
      const span = cellSpan(cell);
      const colStart = col;
      const colEnd = col + span - 1;
      if (colEnd >= range.colStart && colStart <= range.colEnd) {
        located.push({ row: rowIndex, cellIndex, colStart, colEnd, cell });
      }
      col += span;
    }
  }
  return located;
}

function hasVerticalMerge(cell: TableCell): boolean {
  return cell.verticalMerge === 'restart' || cell.verticalMerge === 'continue';
}

function rangesOverlap(aStart: number, aEnd: number, bStart: number, bEnd: number): boolean {
  return aStart <= bEnd && bStart <= aEnd;
}

export function validateTableGrid(table: TableBlock): ApiIssue[] {
  const issues: ApiIssue[] = [];
  if (table.rows.length === 0) {
    issues.push({
      code: 'table.rows.empty',
      message: '表格至少需要一行。',
      severity: 'error',
      path: 'rows',
      suggestedAction: '添加表格行，或删除这个空表格块。'
    });
    return issues;
  }

  let expected = -1;
  const activeVerticalMerges = new Map<number, ActiveVerticalMerge>();
  table.rows.forEach((row, rowIndex) => {
    if (row.cells.length === 0) {
      issues.push({
        code: 'table.row.empty',
        message: '表格行至少需要一个单元格。',
        severity: 'error',
        path: `rows[${rowIndex}].cells`,
        suggestedAction: '添加单元格或删除这一行。'
      });
    }

    let col = 0;
    const nextActive = new Map<number, ActiveVerticalMerge>();
    row.cells.forEach((cell, cellIndex) => {
      const rawSpan = cell.gridSpan ?? 1;
      if (!Number.isInteger(rawSpan) || rawSpan < 1) {
        issues.push(tableIssue('table.gridSpan.invalid', 'gridSpan 必须是大于 0 的整数。', rowIndex, cellIndex));
      }
      if (Number.isInteger(rawSpan) && rawSpan > 32) {
        issues.push(tableIssue('table.gridSpan.tooWide', 'gridSpan 超出安全范围。', rowIndex, cellIndex));
      }
      if (cell.verticalMerge !== undefined && cell.verticalMerge !== 'none' && !hasVerticalMerge(cell)) {
        issues.push(tableIssue('table.verticalMerge.invalidValue', 'verticalMerge 只能是 restart 或 continue。', rowIndex, cellIndex));
      }

      const span = cellSpan(cell);
      for (let logical = col; logical < col + span; logical++) {
        const active = activeVerticalMerges.get(logical);
        if (
          cell.verticalMerge === 'continue' &&
          (!active || active.startCol !== col || active.endCol !== col + span - 1)
        ) {
          issues.push(
            tableIssue('table.verticalMerge.invalidChain', '纵向合并 continuation 上方必须有 restart 或 continuation。', rowIndex, cellIndex)
          );
          break;
        }
      }
      if (hasVerticalMerge(cell)) {
        for (let logical = col; logical < col + span; logical++) {
          nextActive.set(logical, { startCol: col, endCol: col + span - 1 });
        }
      }
      col += span;
    });
    if (expected < 0) expected = col;
    else if (col !== expected) {
      issues.push({
        code: 'table.grid.inconsistent',
        message: `第 ${rowIndex + 1} 行逻辑列数 ${col} 与首行 ${expected} 不一致。`,
        severity: 'error',
        path: `rows[${rowIndex}]`,
        suggestedAction: '拆分或补齐单元格，让每行逻辑列数一致。'
      });
    }
    activeVerticalMerges.clear();
    nextActive.forEach((value, key) => activeVerticalMerges.set(key, value));
  });
  return issues;
}

export function mergeTableRange(table: TableBlock, range: CellRange): TableOpResult {
  const normalized = normalizeRange(
    { row: range.rowStart, col: range.colStart },
    { row: range.rowEnd, col: range.colEnd }
  );
  const width = normalized.colEnd - normalized.colStart + 1;
  const height = normalized.rowEnd - normalized.rowStart + 1;
  if (width < 1 || height < 1) return result(table, []);
  if (width === 1 && height === 1) return result(table, []);

  const cloned = cloneTable(table);
  const preflight = assertExactRange(cloned, normalized);
  if (preflight.length) return result(table, preflight);

  const selected = locateCellsForRange(cloned, normalized);
  const mergedText = selected
    .map((entry) => cellText(entry.cell).trim())
    .filter(Boolean)
    .join('\n');

  for (let rowIndex = normalized.rowStart; rowIndex <= normalized.rowEnd; rowIndex++) {
    mergeRowCells(cloned.rows[rowIndex], normalized.colStart, normalized.colEnd, rowIndex === normalized.rowStart ? mergedText : '');
    const merged = locateCell(cloned.rows[rowIndex], normalized.colStart);
    if (merged) {
      if (height > 1) {
        merged.cell.verticalMerge = rowIndex === normalized.rowStart ? 'restart' : 'continue';
      } else {
        delete merged.cell.verticalMerge;
      }
    }
  }

  return result(cloned, validateTableGrid(cloned));
}

export function splitMergedCell(table: TableBlock, address: CellAddress): TableOpResult {
  const cloned = cloneTable(table);
  const located = locateCell(cloned.rows[address.row], address.col);
  if (!located) return result(table, [opIssue('table.cell.missing', '没有选中可拆分的单元格。')]);
  const root = findVerticalMergeRoot(cloned, address.row, located.colStart);
  const rootLocated = locateCell(cloned.rows[root], located.colStart);
  if (!rootLocated) return result(table, [opIssue('table.cell.missing', '没有找到纵向合并起始单元格。')]);
  const span = cellSpan(rootLocated.cell);
  const chainEnd = findVerticalMergeEnd(cloned, root, located.colStart);

  if (span === 1 && !hasVerticalMerge(rootLocated.cell)) return result(table, []);

  for (let rowIndex = root; rowIndex <= chainEnd; rowIndex++) {
    const row = cloned.rows[rowIndex];
    const current = locateCell(row, located.colStart);
    if (!current) continue;
    const replacement = Array.from({ length: span }, (_, i) => {
      const cell: TableCell = {
        id: i === 0 ? current.cell.id ?? newBlockId('tc') : newBlockId('tc'),
        text: rowIndex === root && i === 0 ? cellText(current.cell) : ''
      };
      return cell;
    });
    row.cells.splice(current.cellIndex, 1, ...replacement);
  }

  return result(cloned, validateTableGrid(cloned));
}

export function addTableRow(table: TableBlock, afterRow: number): TableBlock {
  const cloned = cloneTable(table);
  const width = Math.max(tableGridWidth(cloned), 1);
  const insertAt = Math.min(Math.max(afterRow + 1, 0), cloned.rows.length);
  cloned.rows.splice(insertAt, 0, {
    id: newBlockId('tr'),
    cells: Array.from({ length: width }, () => ({ id: newBlockId('tc'), text: '' }))
  });
  repairDanglingVerticalMerges(cloned);
  return cloned;
}

export function deleteTableRow(table: TableBlock, rowIndex: number): TableOpResult {
  if (table.rows.length <= 1) return result(table, [opIssue('table.row.minimum', '表格至少保留一行。', 'warning')]);
  const cloned = cloneTable(table);
  cloned.rows.splice(rowIndex, 1);
  repairDanglingVerticalMerges(cloned);
  return result(cloned, validateTableGrid(cloned));
}

export function addTableColumn(table: TableBlock, afterCol: number): TableBlock {
  const cloned = cloneTable(table);
  cloned.rows.forEach((row) => {
    const insertAt = Math.max(0, afterCol + 1);
    const located = locateCell(row, insertAt);
    if (located && located.colStart < insertAt && located.colEnd >= insertAt) {
      located.cell.gridSpan = cellSpan(located.cell) + 1;
    } else {
      const cellIndex = located ? located.cellIndex : row.cells.length;
      row.cells.splice(cellIndex, 0, { id: newBlockId('tc'), text: '' });
    }
  });
  return cloned;
}

export function deleteTableColumn(table: TableBlock, colIndex: number): TableOpResult {
  const width = tableGridWidth(table);
  if (width <= 1) return result(table, [opIssue('table.column.minimum', '表格至少保留一列。', 'warning')]);
  const cloned = cloneTable(table);
  cloned.rows.forEach((row) => {
    const located = locateCell(row, colIndex);
    if (!located) return;
    const span = cellSpan(located.cell);
    if (span > 1) {
      const nextSpan = span - 1;
      if (nextSpan > 1) located.cell.gridSpan = nextSpan;
      else delete located.cell.gridSpan;
    } else {
      row.cells.splice(located.cellIndex, 1);
    }
  });
  repairDanglingVerticalMerges(cloned);
  return result(cloned, validateTableGrid(cloned));
}

export function cellText(cell: TableCell): string {
  if (cell.text !== undefined) return cell.text;
  if (!cell.blocks) return '';
  return cell.blocks
    .map((block) => {
      if ('inlines' in block) {
        return block.inlines
          .map((inline) => ('text' in inline ? inline.text : 'displayText' in inline ? inline.displayText : ''))
          .join('');
      }
      return '';
    })
    .filter(Boolean)
    .join('\n');
}

function mergeRowCells(row: TableRow, colStart: number, colEnd: number, text: string): void {
  const first = locateCell(row, colStart);
  const last = locateCell(row, colEnd);
  if (!first || !last) return;
  const span = colEnd - colStart + 1;
  const cell: TableCell = {
    ...first.cell,
    text,
    gridSpan: span > 1 ? span : undefined
  };
  delete cell.blocks;
  row.cells.splice(first.cellIndex, last.cellIndex - first.cellIndex + 1, cell);
}

export function getMergeRangeIssues(table: TableBlock, range: CellRange): ApiIssue[] {
  return assertExactRange(
    table,
    normalizeRange({ row: range.rowStart, col: range.colStart }, { row: range.rowEnd, col: range.colEnd })
  );
}

export function isMergedCellAt(table: TableBlock, address: CellAddress): boolean {
  const located = locateCell(table.rows[address.row], address.col);
  return Boolean(located && (cellSpan(located.cell) > 1 || hasVerticalMerge(located.cell)));
}

export function willDeleteRowAffectMerges(table: TableBlock, rowIndex: number): boolean {
  const row = table.rows[rowIndex];
  if (!row) return false;
  if (row.cells.some((cell) => cellSpan(cell) > 1 || hasVerticalMerge(cell))) return true;
  return Boolean(table.rows[rowIndex + 1]?.cells.some((cell) => cell.verticalMerge === 'continue'));
}

export function willDeleteColumnAffectMerges(table: TableBlock, colIndex: number): boolean {
  return table.rows.some((row) => {
    let col = 0;
    for (const cell of row.cells) {
      const span = cellSpan(cell);
      const colEnd = col + span - 1;
      if (rangesOverlap(col, colEnd, colIndex, colIndex) && (span > 1 || hasVerticalMerge(cell))) {
        return true;
      }
      col += span;
    }
    return false;
  });
}

function assertExactRange(table: TableBlock, range: CellRange): ApiIssue[] {
  const issues: ApiIssue[] = validateTableGrid(table);
  if (issues.some((issue) => issue.severity === 'error')) {
    return issues;
  }

  if (!table.rows[range.rowStart] || !table.rows[range.rowEnd]) {
    return [opIssue('table.selection.invalid', '选择范围超出表格行数。')];
  }
  if (range.rowStart < 0 || range.colStart < 0) {
    return [opIssue('table.selection.invalid', '选择范围不能为负数。')];
  }

  for (let rowIndex = range.rowStart; rowIndex <= range.rowEnd; rowIndex++) {
    const row = table.rows[rowIndex];
    const start = locateCell(row, range.colStart);
    const end = locateCell(row, range.colEnd);
    if (!start || !end) {
      issues.push(opIssue('table.selection.invalid', '选择范围超出表格列数。'));
      continue;
    }
    if (start.colStart !== range.colStart || end.colEnd !== range.colEnd) {
      issues.push(opIssue('table.selection.partialMerge', '选择范围切到了已有合并单元格内部；请先拆分。'));
    }
    const cells = row.cells.slice(start.cellIndex, end.cellIndex + 1);
    if (cells.some((cell) => hasVerticalMerge(cell))) {
      issues.push(opIssue('table.selection.existingVerticalMerge', '包含已有纵向合并；请先拆分后再合并。'));
    }
    if (cells.some((cell) => cellSpan(cell) > 1)) {
      issues.push(opIssue('table.selection.existingHorizontalMerge', '包含已有横向合并；请先拆分后再合并。'));
    }
  }
  return issues;
}

function findVerticalMergeRoot(table: TableBlock, rowIndex: number, col: number): number {
  let current = rowIndex;
  while (current > 0) {
    const located = locateCell(table.rows[current], col);
    if (!located || located.cell.verticalMerge !== 'continue') return current;
    const prev = locateCell(table.rows[current - 1], col);
    if (!prev || !(prev.cell.verticalMerge === 'restart' || prev.cell.verticalMerge === 'continue')) return current;
    current -= 1;
  }
  return current;
}

function findVerticalMergeEnd(table: TableBlock, root: number, col: number): number {
  const rootLocated = locateCell(table.rows[root], col);
  if (!rootLocated || rootLocated.cell.verticalMerge !== 'restart') return root;
  let rowIndex = root;
  while (rowIndex + 1 < table.rows.length) {
    const next = locateCell(table.rows[rowIndex + 1], col);
    if (!next || next.cell.verticalMerge !== 'continue') break;
    rowIndex += 1;
  }
  return rowIndex;
}

function repairDanglingVerticalMerges(table: TableBlock): void {
  const active = new Map<number, ActiveVerticalMerge>();
  table.rows.forEach((row) => {
    let col = 0;
    const next = new Map<number, ActiveVerticalMerge>();
    row.cells.forEach((cell) => {
      const span = cellSpan(cell);
      if (cell.verticalMerge === 'continue') {
        let valid = true;
        for (let logical = col; logical < col + span; logical++) {
          const activeRange = active.get(logical);
          if (!activeRange || activeRange.startCol !== col || activeRange.endCol !== col + span - 1) valid = false;
        }
        if (!valid) delete cell.verticalMerge;
      }
      if (hasVerticalMerge(cell)) {
        for (let logical = col; logical < col + span; logical++) next.set(logical, { startCol: col, endCol: col + span - 1 });
      }
      col += span;
    });
    active.clear();
    next.forEach((value, key) => active.set(key, value));
  });
}

function cellSpan(cell: TableCell): number {
  return Number.isInteger(cell.gridSpan) && (cell.gridSpan ?? 1) > 0 ? cell.gridSpan ?? 1 : 1;
}

function cloneTable(table: TableBlock): TableBlock {
  return JSON.parse(JSON.stringify(table)) as TableBlock;
}

function result(table: TableBlock, issues: ApiIssue[]): TableOpResult {
  return { table, issues };
}

function tableIssue(code: string, message: string, row: number, cell: number): ApiIssue {
  return {
    code,
    message,
    severity: 'error',
    path: `rows[${row}].cells[${cell}]`,
    suggestedAction: null
  };
}

function opIssue(code: string, message: string, severity: ApiIssue['severity'] = 'error'): ApiIssue {
  return { code, message, severity, path: null, suggestedAction: null };
}
