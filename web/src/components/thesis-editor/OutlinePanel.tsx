import { Badge, Panel } from '../ui/Primitives';
import type { BlockNode, ThesisEditorState } from './types';

const BLOCK_LABEL: Record<string, string> = {
  heading: '标题',
  paragraph: '段落',
  abstract: '摘要',
  table: '表格',
  figure: '图片',
  equation: '公式',
  pageBreak: '分页'
};

export function OutlinePanel({
  state,
  onSelect
}: {
  state: ThesisEditorState;
  onSelect: (blockId?: string) => void;
}) {
  const issueBlocks = new Set(state.validationIssues.map(issue => issue.blockId).filter(Boolean));
  const hasErrors = state.validationIssues.some(issue => issue.severity === 'error');
  const totalBlocks = state.sections.flatMap(section => section.blocks).length;

  return (
    <Panel
      title="论文结构大纲"
      description="点击标题或节跳转到编辑位置。"
      contentClassName="outline"
    >
      <div className="outline-summary">
        <Badge
          tone={hasErrors ? 'danger' : state.validationIssues.length ? 'warning' : 'success'}
        >
          {state.validationIssues.length ? `${state.validationIssues.length} 校验项` : '结构可用'}
        </Badge>
        <span className="muted">共 {totalBlocks} 块</span>
      </div>

      <nav aria-label="论文结构大纲">
        {state.sections.map(section => {
          const headingBlocks = section.blocks.filter(b => b.type === 'heading');
          const otherBlocks = section.blocks.filter(b => b.type !== 'heading');
          return (
            <div key={section.id} className="outline-section">
              <button
                type="button"
                className="outline-section-title"
                onClick={() => onSelect(undefined)}
                aria-label={`跳转到${section.title}`}
              >
                <span>{section.title}</span>
                <span className="outline-section-count">{section.blocks.length}</span>
              </button>

              {headingBlocks.map(block =>
                block.type !== 'heading' ? null : (
                  <button
                    key={block.id}
                    type="button"
                    className={`outline-item indent-${Math.min(3, block.level)} ${
                      state.selectedBlockId === block.id ? 'active' : ''
                    }`}
                    onClick={() => onSelect(block.id)}
                    aria-label={`标题 H${block.level}：${block.text || '未命名标题'}`}
                  >
                    <span className="outline-item-text">{block.text || '未命名标题'}</span>
                    <span className="outline-item-meta">
                      {issueBlocks.has(block.id) ? (
                        <Badge tone="warning">!</Badge>
                      ) : (
                        <span>H{block.level}</span>
                      )}
                    </span>
                  </button>
                )
              )}

              {otherBlocks.length > 0 && headingBlocks.length === 0 ? (
                <div className="outline-block-summary">{summarize(otherBlocks)}</div>
              ) : null}
            </div>
          );
        })}
      </nav>
    </Panel>
  );
}

function summarize(blocks: BlockNode[]) {
  const counts: Record<string, number> = {};
  for (const block of blocks) counts[block.type] = (counts[block.type] ?? 0) + 1;
  return Object.entries(counts).map(([type, count]) => (
    <span key={type} className="outline-summary-chip">
      {BLOCK_LABEL[type] ?? type} ×{count}
    </span>
  ));
}
