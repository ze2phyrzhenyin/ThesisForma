import { EmptyState } from '../ui/Primitives';
import type { ThesisEditorState } from './types';

export function TocPreview({ state }: { state: ThesisEditorState }) {
  const headings = state.sections.flatMap(section =>
    section.blocks.filter(block => block.type === 'heading')
  );

  return (
    <div className="stack" data-testid="toc-preview">
      <p className="helper">
        目录由标题自动生成，导出 DOCX 时写入 Word TOC field。标题文本就是目录条目，格式由模板控制。
      </p>
      {headings.length === 0 ? (
        <EmptyState title="还没有标题。添加标题后目录会自动更新。" />
      ) : (
        <div className="toc-list">
          {headings.map(heading =>
            heading.type !== 'heading' ? null : (
              <div
                key={heading.id}
                className="toc-entry"
                style={{ paddingLeft: (heading.level - 1) * 16 }}
              >
                <span className="toc-level">H{heading.level}</span>
                <span className="toc-text">{heading.text || '未命名标题'}</span>
                <span className="toc-dots" aria-hidden="true" />
              </div>
            )
          )}
        </div>
      )}
    </div>
  );
}
