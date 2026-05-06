import { Button, Badge } from '../design-system/Primitives';
import type { EditorAction } from './editorReducer';
import type { BlockNode } from './types';
import {
  AbstractBlockEditor,
  EquationBlockEditor,
  FigureBlockEditor,
  HeadingBlockEditor,
  ParagraphBlockEditor,
  TableBlockEditor
} from './BlockEditors';

export function BlockEditor({ block, active, dispatch, bibliographyKeys, referenceTargets }: {
  block: BlockNode;
  active: boolean;
  dispatch: React.Dispatch<EditorAction>;
  bibliographyKeys: string[];
  referenceTargets: Array<{ id: string; label: string; type: string }>;
}) {
  return (
    <article
      className={`block-card ${active ? 'active' : ''}`}
      data-block-id={block.id}
      data-testid={`block-${block.type}`}
      onFocus={() => dispatch({ type: 'selectBlock', blockId: block.id })}
    >
      <div className="block-header">
        <Badge>{labelFor(block)}</Badge>
        <div className="toolbar-actions">
          <Button type="button" onClick={() => dispatch({ type: 'moveBlock', blockId: block.id, direction: 'up' })}>上移</Button>
          <Button type="button" onClick={() => dispatch({ type: 'moveBlock', blockId: block.id, direction: 'down' })}>下移</Button>
          <Button type="button" variant="danger" onClick={() => window.confirm('删除该内容块？') && dispatch({ type: 'deleteBlock', blockId: block.id })}>删除</Button>
        </div>
      </div>
      <div className="block-body">
        {renderBlock(block, dispatch, bibliographyKeys, referenceTargets)}
      </div>
    </article>
  );
}

function renderBlock(block: BlockNode, dispatch: React.Dispatch<EditorAction>, bibliographyKeys: string[], referenceTargets: Array<{ id: string; label: string; type: string }>) {
  switch (block.type) {
    case 'heading':
      return <HeadingBlockEditor block={block} dispatch={dispatch} />;
    case 'paragraph':
      return <ParagraphBlockEditor block={block} dispatch={dispatch} bibliographyKeys={bibliographyKeys} referenceTargets={referenceTargets} />;
    case 'abstract':
      return <AbstractBlockEditor block={block} dispatch={dispatch} />;
    case 'table':
      return <TableBlockEditor block={block} dispatch={dispatch} />;
    case 'figure':
      return <FigureBlockEditor block={block} dispatch={dispatch} />;
    case 'equation':
      return <EquationBlockEditor block={block} dispatch={dispatch} />;
    case 'pageBreak':
      return <p className="muted">分页结构块。具体分页样式由模板控制。</p>;
    default:
      return null;
  }
}

function labelFor(block: BlockNode) {
  return {
    heading: '标题',
    paragraph: '正文段落',
    abstract: '摘要',
    table: '表格',
    figure: '图片',
    equation: '公式',
    pageBreak: '分页'
  }[block.type];
}
