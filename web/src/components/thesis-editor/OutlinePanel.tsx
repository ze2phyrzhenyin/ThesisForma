import { Badge, Panel } from '../design-system/Primitives';
import type { ThesisEditorState } from './types';

const blockTypeLabel: Record<string, string> = {
  heading: '标题',
  paragraph: '段落',
  abstract: '摘要',
  table: '表格',
  figure: '图片',
  equation: '公式',
  pageBreak: '分页'
};

export function OutlinePanel({ state, onSelect }: { state: ThesisEditorState; onSelect: (blockId?: string) => void }) {
  const issueBlocks = new Set(state.validationIssues.map(issue => issue.blockId).filter(Boolean));
  const hasErrors = state.validationIssues.some(issue => issue.severity === 'error');
  const allBlocks = state.sections.flatMap(section => section.blocks);
  const totalBlocks = allBlocks.length;

  return (
    <Panel title="论文结构大纲" description="点击标题或节跳转到编辑位置。">
      <div className="outline-summary">
        <Badge tone={hasErrors ? 'danger' : state.validationIssues.length ? 'warning' : 'success'}>
          {state.validationIssues.length ? `${state.validationIssues.length} 校验项` : '结构可用'}
        </Badge>
        <span className="muted">{totalBlocks} 个内容块</span>
      </div>
      <nav className="outline-tree" aria-label="论文结构大纲">
        {state.sections.map(section => {
          const headingBlocks = section.blocks.filter(block => block.type === 'heading');
          const otherBlocks = section.blocks.filter(block => block.type !== 'heading');
          return (
            <div key={section.id} className="outline-section">
              <button
                className="outline-section-title"
                onClick={() => onSelect(undefined)}
                aria-label={`跳转到${section.title}`}
              >
                <strong>{section.title}</strong>
                <span className="outline-section-count">{section.blocks.length}</span>
              </button>
              {headingBlocks.map(block => {
                if (block.type !== 'heading') return null;
                return (
                  <button
                    key={block.id}
                    className={`outline-item outline-indent-${Math.min(3, block.level)} ${state.selectedBlockId === block.id ? 'active' : ''}`}
                    onClick={() => onSelect(block.id)}
                    aria-label={`标题 H${block.level}：${block.text || '未命名标题'}`}
                  >
                    <span className="outline-item-text">{block.text || '未命名标题'}</span>
                    <span className="outline-item-meta">
                      {issueBlocks.has(block.id) ? (
                        <Badge tone="warning">!</Badge>
                      ) : (
                        <span className="muted">H{block.level}</span>
                      )}
                    </span>
                  </button>
                );
              })}
              {otherBlocks.length > 0 && headingBlocks.length === 0 ? (
                <div className="outline-block-summary">
                  {summarizeBlocks(otherBlocks)}
                </div>
              ) : null}
            </div>
          );
        })}
      </nav>
    </Panel>
  );
}

function summarizeBlocks(blocks: ThesisEditorState['sections'][number]['blocks']) {
  const counts: Record<string, number> = {};
  for (const block of blocks) {
    counts[block.type] = (counts[block.type] ?? 0) + 1;
  }
  return Object.entries(counts).map(([type, count]) => (
    <span key={type} className="outline-summary-chip">
      {blockTypeLabel[type] ?? type} ×{count}
    </span>
  ));
}
