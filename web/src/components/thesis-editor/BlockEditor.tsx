import { Badge, IconButton } from '../ui/Primitives';
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

const LABEL: Record<BlockNode['type'], string> = {
  heading: '标题',
  paragraph: '正文段落',
  abstract: '摘要',
  table: '表格',
  figure: '图片',
  equation: '公式',
  pageBreak: '分页'
};

export function BlockEditor({
  block,
  active,
  dispatch,
  bibliographyKeys,
  referenceTargets,
  issues = []
}: {
  block: BlockNode;
  active: boolean;
  dispatch: React.Dispatch<EditorAction>;
  bibliographyKeys: string[];
  referenceTargets: Array<{ id: string; label: string; type: string }>;
  issues?: ValidationIssue[];
}) {
  const hasError = issues.some(i => i.severity === 'error');

  return (
    <article
      className={`block ${active ? 'active' : ''} ${hasError ? 'has-error' : ''}`}
      data-block-id={block.id}
      data-testid={`block-${block.type}`}
      onFocus={() => dispatch({ type: 'selectBlock', blockId: block.id })}
    >
      <header className="block-head">
        <div className="block-tags">
          <Badge tone="neutral" outline>
            {LABEL[block.type]}
          </Badge>
          {issues.length ? (
            <Badge tone={hasError ? 'danger' : 'warning'}>{issues.length} 个问题</Badge>
          ) : (
            <Badge tone="success">结构可用</Badge>
          )}
          <small>{describe(block)}</small>
        </div>
        <div className="block-actions">
          <IconButton
            type="button"
            aria-label="上移"
            title="上移"
            onClick={() => dispatch({ type: 'moveBlock', blockId: block.id, direction: 'up' })}
          >
            ↑
          </IconButton>
          <IconButton
            type="button"
            aria-label="下移"
            title="下移"
            onClick={() => dispatch({ type: 'moveBlock', blockId: block.id, direction: 'down' })}
          >
            ↓
          </IconButton>
          <IconButton
            type="button"
            aria-label="复制"
            title="复制"
            onClick={() => dispatch({ type: 'duplicateBlock', blockId: block.id })}
          >
            ⎘
          </IconButton>
          <IconButton
            type="button"
            aria-label="删除"
            title="删除"
            onClick={() =>
              window.confirm('删除该内容块？') &&
              dispatch({ type: 'deleteBlock', blockId: block.id })
            }
          >
            ✕
          </IconButton>
        </div>
      </header>

      <div className="block-body">
        {renderBlock(block, dispatch, bibliographyKeys, referenceTargets)}
      </div>

      {issues.length ? (
        <div className="block-issues">
          {issues.map(issue => (
            <div key={issue.code} className={`issue ${issue.severity}`}>
              <strong>{issue.message}</strong>
              {issue.suggestedAction ? (
                <span className="issue-meta">{issue.suggestedAction}</span>
              ) : null}
            </div>
          ))}
        </div>
      ) : null}
    </article>
  );
}

function renderBlock(
  block: BlockNode,
  dispatch: React.Dispatch<EditorAction>,
  bibliographyKeys: string[],
  referenceTargets: Array<{ id: string; label: string; type: string }>
) {
  switch (block.type) {
    case 'heading':
      return <HeadingBlockEditor block={block} dispatch={dispatch} />;
    case 'paragraph':
      return (
        <ParagraphBlockEditor
          block={block}
          dispatch={dispatch}
          bibliographyKeys={bibliographyKeys}
          referenceTargets={referenceTargets}
        />
      );
    case 'abstract':
      return <AbstractBlockEditor block={block} dispatch={dispatch} />;
    case 'table':
      return <TableBlockEditor block={block} dispatch={dispatch} />;
    case 'figure':
      return <FigureBlockEditor block={block} dispatch={dispatch} />;
    case 'equation':
      return <EquationBlockEditor block={block} dispatch={dispatch} />;
    case 'pageBreak':
      return <p className="helper">分页结构块。具体分页样式由模板控制。</p>;
    default:
      return null;
  }
}

function describe(block: BlockNode) {
  if (block.type === 'heading') return `H${block.level} · 标题样式由模板控制`;
  if (block.type === 'table') return `${block.rows.length} 行 × ${block.rows[0]?.cells.length ?? 0} 列`;
  if (block.type === 'figure') return block.imagePath ? '已有图片资产' : '等待上传图片';
  if (block.type === 'paragraph') return '正文内容，不提供手工排版';
  if (block.type === 'abstract')
    return block.language === 'zh' ? '中文摘要与关键词' : '英文摘要与 Key words';
  if (block.type === 'equation') return 'OMML 渲染由后端负责';
  return '结构分页标记';
}
