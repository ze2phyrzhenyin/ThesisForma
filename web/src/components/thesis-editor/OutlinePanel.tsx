import { Badge, Panel } from '../design-system/Primitives';
import type { ThesisEditorState } from './types';

export function OutlinePanel({ state, onSelect }: { state: ThesisEditorState; onSelect: (blockId?: string) => void }) {
  return (
    <Panel title="论文结构大纲">
      <nav className="stack" aria-label="论文结构大纲">
        {state.sections.map(section => (
          <div key={section.id}>
            <button className="insert-option" onClick={() => onSelect(undefined)} aria-label={`跳转到${section.title}`}>
              <strong>{section.title}</strong>
              <span>{section.blocks.length} 个内容块</span>
            </button>
            <div className="stack" style={{ margin: '8px 0 0 12px', gap: 4 }}>
              {section.blocks.filter(block => block.type === 'heading').map(block => (
                <button
                  key={block.id}
                  className="insert-option"
                  style={{ padding: '7px 9px' }}
                  onClick={() => onSelect(block.id)}
                >
                  <strong>{'　'.repeat(Math.max(0, block.level - 1))}{block.text}</strong>
                  <span>Heading {block.level}</span>
                </button>
              ))}
            </div>
          </div>
        ))}
      </nav>
      <div style={{ marginTop: 12 }}>
        <Badge tone={state.validationIssues.some(issue => issue.severity === 'error') ? 'danger' : 'success'}>
          {state.validationIssues.length ? `${state.validationIssues.length} 个校验项` : '结构可用'}
        </Badge>
      </div>
    </Panel>
  );
}
