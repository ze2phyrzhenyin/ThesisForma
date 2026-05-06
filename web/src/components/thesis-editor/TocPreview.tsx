import { EmptyState } from '../design-system/Primitives';
import type { ThesisEditorState } from './types';

export function TocPreview({ state }: { state: ThesisEditorState }) {
  const headings = state.sections.flatMap(section => section.blocks.filter(block => block.type === 'heading'));
  return (
    <div className="stack" data-testid="toc-preview">
      <p className="muted">目录由标题自动生成，导出 DOCX 时写入 Word TOC field。</p>
      {headings.length === 0 ? <EmptyState title="还没有标题。添加标题后目录会自动更新。" /> : null}
      {headings.map(heading => (
        <div key={heading.id} style={{ paddingLeft: (heading.level - 1) * 16 }}>
          <span>{heading.text || '未命名标题'}</span>
        </div>
      ))}
    </div>
  );
}
