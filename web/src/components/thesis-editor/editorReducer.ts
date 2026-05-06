import type { AssetRef, BibliographyEntry, BlockNode, InlineNode, SectionKind, ThesisEditorState } from './types';
import { createTableBlock, newId, validateEditorState } from './serialization';

export type EditorAction =
  | { type: 'setMetadata'; field: keyof ThesisEditorState['metadata']; value: string }
  | { type: 'setDocumentId'; id: string }
  | { type: 'replaceState'; state: ThesisEditorState }
  | { type: 'setTemplate'; templateId: string; template?: ThesisEditorState['template'] }
  | { type: 'setAutosaveStatus'; status: ThesisEditorState['autosaveStatus'] }
  | { type: 'selectBlock'; blockId?: string }
  | { type: 'insertBlock'; sectionId: string; block: BlockNode; afterBlockId?: string }
  | { type: 'updateBlock'; blockId: string; block: BlockNode }
  | { type: 'deleteBlock'; blockId: string }
  | { type: 'duplicateBlock'; blockId: string }
  | { type: 'moveBlock'; blockId: string; direction: 'up' | 'down' }
  | { type: 'addTableRow'; blockId: string }
  | { type: 'addTableColumn'; blockId: string }
  | { type: 'deleteTableRow'; blockId: string; rowId: string }
  | { type: 'deleteTableColumn'; blockId: string; columnIndex: number }
  | { type: 'updateTableCell'; blockId: string; rowId: string; cellId: string; text: string }
  | { type: 'addBibliographyEntry'; entry?: BibliographyEntry }
  | { type: 'updateBibliographyEntry'; key: string; patch: Partial<BibliographyEntry> }
  | { type: 'deleteBibliographyEntry'; key: string }
  | { type: 'insertCitation'; blockId: string; key: string }
  | { type: 'insertReference'; blockId: string; bookmarkName: string; label: string }
  | { type: 'addAsset'; asset: AssetRef }
  | { type: 'setValidationIssues'; issues: ThesisEditorState['validationIssues'] }
  | { type: 'setRenderRun'; run: ThesisEditorState['renderRun'] };

export function editorReducer(state: ThesisEditorState, action: EditorAction): ThesisEditorState {
  switch (action.type) {
    case 'setMetadata':
      return markUnsaved({ ...state, metadata: { ...state.metadata, [action.field]: action.value } });
    case 'setDocumentId':
      return { ...state, documentId: action.id };
    case 'replaceState':
      return action.state;
    case 'setTemplate':
      return markUnsaved({ ...state, templateId: action.templateId, template: action.template });
    case 'setAutosaveStatus':
      return { ...state, autosaveStatus: action.status };
    case 'selectBlock':
      return { ...state, selectedBlockId: action.blockId };
    case 'insertBlock':
      return markUnsaved({
        ...state,
        selectedBlockId: action.block.id,
        sections: state.sections.map(section => section.id === action.sectionId ? { ...section, blocks: insertBlock(section.blocks, action.block, action.afterBlockId) } : section)
      });
    case 'updateBlock':
      return markUnsaved(mapBlocks(state, block => block.id === action.blockId ? action.block : block));
    case 'deleteBlock':
      return markUnsaved({ ...mapBlocks(state, block => block.id === action.blockId ? undefined : block), selectedBlockId: undefined });
    case 'duplicateBlock':
      return markUnsaved({
        ...state,
        selectedBlockId: `${action.blockId}-copy`,
        sections: state.sections.map(section => {
          const index = section.blocks.findIndex(block => block.id === action.blockId);
          if (index < 0) return section;
          const copy = cloneBlock(section.blocks[index]);
          return { ...section, blocks: [...section.blocks.slice(0, index + 1), copy, ...section.blocks.slice(index + 1)] };
        })
      });
    case 'moveBlock':
      return markUnsaved({
        ...state,
        sections: state.sections.map(section => ({ ...section, blocks: moveBlock(section.blocks, action.blockId, action.direction) }))
      });
    case 'addTableRow':
      return updateTable(state, action.blockId, table => ({
        ...table,
        rows: [...table.rows, { id: newId('row'), cells: table.rows[0]?.cells.map(() => ({ id: newId('cell'), text: '' })) ?? [] }]
      }));
    case 'addTableColumn':
      return updateTable(state, action.blockId, table => ({
        ...table,
        rows: table.rows.map(row => ({ ...row, cells: [...row.cells, { id: newId('cell'), text: '' }] }))
      }));
    case 'deleteTableRow':
      return updateTable(state, action.blockId, table => ({ ...table, rows: table.rows.filter(row => row.id !== action.rowId) }));
    case 'deleteTableColumn':
      return updateTable(state, action.blockId, table => ({
        ...table,
        rows: table.rows.map(row => ({ ...row, cells: row.cells.filter((_, index) => index !== action.columnIndex) }))
      }));
    case 'updateTableCell':
      return updateTable(state, action.blockId, table => ({
        ...table,
        rows: table.rows.map(row => row.id === action.rowId
          ? { ...row, cells: row.cells.map(cell => cell.id === action.cellId ? { ...cell, text: action.text } : cell) }
          : row)
      }));
    case 'addBibliographyEntry': {
      const entry = action.entry ?? { id: newId('ref'), key: `ref-${state.bibliography.length + 1}`, text: '', entryType: 'other' as const };
      return markUnsaved({ ...state, bibliography: [...state.bibliography, entry] });
    }
    case 'updateBibliographyEntry':
      return markUnsaved({ ...state, bibliography: state.bibliography.map(entry => entry.key === action.key ? { ...entry, ...action.patch } : entry) });
    case 'deleteBibliographyEntry':
      return markUnsaved({ ...state, bibliography: state.bibliography.filter(entry => entry.key !== action.key) });
    case 'insertCitation':
      return markUnsaved(mapBlocks(state, block => block.id === action.blockId && block.type === 'paragraph'
        ? { ...block, inlines: [...block.inlines, { type: 'citation', targetId: action.key, displayText: `[${action.key}]` }] }
        : block));
    case 'insertReference':
      return markUnsaved(mapBlocks(state, block => block.id === action.blockId && block.type === 'paragraph'
        ? { ...block, inlines: [...block.inlines, { type: 'reference', bookmarkName: action.bookmarkName, fallbackText: action.label }] }
        : block));
    case 'addAsset':
      return { ...state, assets: [...state.assets, action.asset] };
    case 'setValidationIssues':
      return { ...state, validationIssues: action.issues };
    case 'setRenderRun':
      return { ...state, renderRun: action.run };
    default:
      return state;
  }
}

export function localValidate(state: ThesisEditorState) {
  return validateEditorState(state);
}

export const blockFactories = {
  heading: (): BlockNode => ({ type: 'heading', id: newId('heading'), level: 1, text: '新标题', bookmarkName: newId('bookmark'), numbered: true }),
  paragraph: (): BlockNode => ({ type: 'paragraph', id: newId('paragraph'), inlines: [{ type: 'text', text: '' }] }),
  table: (rows = 3, columns = 3, caption = '表名待填写'): BlockNode => createTableBlock(rows, columns, caption),
  figure: (asset?: AssetRef): BlockNode => ({ type: 'figure', id: newId('figure'), caption: '图名待填写', altText: '', imagePath: asset?.imagePath, previewUrl: asset?.previewUrl, imageContentType: asset?.contentType ?? 'image/png', widthCm: 8 }),
  equation: (): BlockNode => ({ type: 'equation', id: newId('equation'), plainText: 'E=mc^2', caption: '公式说明', bookmarkName: newId('eq') }),
  pageBreak: (): BlockNode => ({ type: 'pageBreak', id: newId('page-break') })
};

function markUnsaved(state: ThesisEditorState): ThesisEditorState {
  return { ...state, autosaveStatus: 'unsaved', validationIssues: validateEditorState(state) };
}

function insertBlock(blocks: BlockNode[], block: BlockNode, afterBlockId?: string) {
  if (!afterBlockId) return [...blocks, block];
  const index = blocks.findIndex(item => item.id === afterBlockId);
  if (index < 0) return [...blocks, block];
  return [...blocks.slice(0, index + 1), block, ...blocks.slice(index + 1)];
}

function moveBlock(blocks: BlockNode[], blockId: string, direction: 'up' | 'down') {
  const index = blocks.findIndex(block => block.id === blockId);
  if (index < 0) return blocks;
  const target = direction === 'up' ? index - 1 : index + 1;
  if (target < 0 || target >= blocks.length) return blocks;
  const copy = [...blocks];
  [copy[index], copy[target]] = [copy[target], copy[index]];
  return copy;
}

function mapBlocks(state: ThesisEditorState, mapper: (block: BlockNode) => BlockNode | undefined): ThesisEditorState {
  return {
    ...state,
    sections: state.sections.map(section => ({
      ...section,
      blocks: section.blocks.map(mapper).filter((block): block is BlockNode => Boolean(block))
    }))
  };
}

function updateTable(state: ThesisEditorState, blockId: string, update: (table: Extract<BlockNode, { type: 'table' }>) => Extract<BlockNode, { type: 'table' }>) {
  return markUnsaved(mapBlocks(state, block => block.id === blockId && block.type === 'table' ? update(block) : block));
}

export function setParagraphText(block: Extract<BlockNode, { type: 'paragraph' }>, text: string): Extract<BlockNode, { type: 'paragraph' }> {
  const firstText: InlineNode = { type: 'text', text };
  return { ...block, inlines: [firstText, ...block.inlines.filter(inline => inline.type !== 'text')] };
}

function cloneBlock(block: BlockNode): BlockNode {
  const serialized = JSON.parse(JSON.stringify(block)) as BlockNode;
  const id = `${block.id}-copy`;
  if (serialized.type === 'table') {
    return {
      ...serialized,
      id,
      bookmarkId: id,
      rows: serialized.rows.map(row => ({
        ...row,
        id: newId('row'),
        cells: row.cells.map(cell => ({ ...cell, id: newId('cell') }))
      }))
    };
  }
  if (serialized.type === 'heading') return { ...serialized, id, bookmarkName: id };
  if (serialized.type === 'figure') return { ...serialized, id };
  if (serialized.type === 'equation') return { ...serialized, id, bookmarkName: id };
  return { ...serialized, id };
}
