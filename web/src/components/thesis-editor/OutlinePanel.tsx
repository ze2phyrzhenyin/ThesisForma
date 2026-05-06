import { Badge, Panel } from '../design-system/Primitives';
import type { ThesisEditorState } from './types';

export function OutlinePanel({ state, onSelect }: { state: ThesisEditorState; onSelect: (blockId?: string) => void }) {
  const issueBlocks = new Set(state.validationIssues.map(issue => issue.blockId).filter(Boolean));
  return (
    <Panel title="论文结构大纲" description="点击标题或 section 跳转到编辑位置。">
      <nav className="outline-tree" aria-label="论文结构大纲">
        {state.sections.map(section => (
          <div key={section.id}>
            <button className="outline-item" onClick={() => onSelect(undefined)} aria-label={`跳转到${section.title}`}>
              <strong>{section.title}</strong>
              <span className="muted">{section.blocks.length}</span>
            </button>
            <div className="outline-tree">
              {section.blocks.filter(block => block.type === 'heading').map(block => (
                <button
                  key={block.id}
                  className={`outline-item outline-indent-${Math.min(3, block.level)} ${state.selectedBlockId === block.id ? 'active' : ''}`}
                  onClick={() => onSelect(block.id)}
                >
                  <span>{block.text || '未命名标题'}</span>
                  {issueBlocks.has(block.id) ? <Badge tone="warning">待处理</Badge> : <span className="muted">H{block.level}</span>}
                </button>
              ))}
            </div>
          </div>
        ))}
      </nav>
      <div className="spacer-md" />
      <div>
        <Badge tone={state.validationIssues.some(issue => issue.severity === 'error') ? 'danger' : 'success'}>
          {state.validationIssues.length ? `${state.validationIssues.length} 个校验项` : '结构可用'}
        </Badge>
      </div>
    </Panel>
  );
}
