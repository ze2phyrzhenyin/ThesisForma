import { Button, Badge } from '../design-system/Primitives';
import type { EditorAction } from './editorReducer';
import type { BlockNode, ValidationIssue } from './types';
import {
  AbstractBlockEditor,
  EquationBlockEditor,
  FigureBlockEditor,
  HeadingBlockEditor,
  ParagraphBlockEditor,
  TableBlockEditor
} from './BlockEditors';

export function BlockEditor({ block, active, dispatch, bibliographyKeys, referenceTargets, issues = [] }: {
  block: BlockNode;
  active: boolean;
  dispatch: React.Dispatch<EditorAction>;
  bibliographyKeys: string[];
  referenceTargets: Array<{ id: string; label: string; type: string }>;
  issues?: ValidationIssue[];
}) {
  const blockIssues = issues;
  return (
    <article
      className={`block-card ${active ? 'active' : ''}`}
      data-block-id={block.id}
      data-testid={`block-${block.type}`}
      onFocus={() => dispatch({ type: 'selectBlock', blockId: block.id })}
    >
      <div className="block-header">
        <div className="block-kicker">
          <Badge>{labelFor(block)}</Badge>
          {blockIssues.length ? <Badge tone={blockIssues.some(issue => issue.severity === 'error') ? 'danger' : 'warning'}>{blockIssues.length} 个问题</Badge> : <Badge tone="success">结构可用</Badge>}
          <small>{descriptionFor(block)}</small>
        </div>
        <div className="block-actions">
          <Button type="button" variant="ghost" onClick={() => dispatch({ type: 'moveBlock', blockId: block.id, direction: 'up' })}>上移</Button>
          <Button type="button" variant="ghost" onClick={() => dispatch({ type: 'moveBlock', blockId: block.id, direction: 'down' })}>下移</Button>
          <Button type="button" variant="ghost" onClick={() => dispatch({ type: 'duplicateBlock', blockId: block.id })}>复制</Button>
          <Button type="button" variant="danger" onClick={() => window.confirm('删除该内容块？') && dispatch({ type: 'deleteBlock', blockId: block.id })}>删除</Button>
        </div>
      </div>
      <div className="block-body">
        {renderBlock(block, dispatch, bibliographyKeys, referenceTargets)}
      </div>
      {blockIssues.length ? (
        <div className="block-warning stack-tight">
          {blockIssues.map(issue => (
            <div key={issue.code} className={`issue-row ${issue.severity}`}>
              <strong>{issue.message}</strong>
              {issue.suggestedAction ? <span className="muted">{issue.suggestedAction}</span> : null}
            </div>
          ))}
        </div>
      ) : null}
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

function descriptionFor(block: BlockNode) {
  if (block.type === 'heading') return `H${block.level}，最终标题样式由模板控制`;
  if (block.type === 'table') return `${block.rows.length} 行 x ${block.rows[0]?.cells.length ?? 0} 列`;
  if (block.type === 'figure') return block.imagePath ? '已有图片资产' : '等待上传图片';
  if (block.type === 'paragraph') return '正文内容，不提供手工排版';
  if (block.type === 'abstract') return block.language === 'zh' ? '中文摘要与关键词' : '英文摘要与 Key words';
  if (block.type === 'equation') return 'OMML 渲染由后端负责';
  return '结构分页标记';
}
