import { blockFactories, type EditorAction } from './editorReducer';

const options = [
  { type: 'heading', title: '标题', description: '添加一级到六级标题，自动进入大纲。' },
  { type: 'paragraph', title: '正文段落', description: '录入论文正文，支持引用和交叉引用。' },
  { type: 'table', title: '表格', description: '填写表名、行列和单元格内容。' },
  { type: 'figure', title: '图片', description: '上传图片并填写图名和替代文本。' },
  { type: 'equation', title: '公式', description: '输入简单公式或公式占位。' },
  { type: 'pageBreak', title: '分页', description: '插入结构化分页标记。' }
] as const;

export function InsertBlockMenu({ sectionId, dispatch }: { sectionId: string; dispatch: React.Dispatch<EditorAction> }) {
  return (
    <div className="insert-menu">
      <strong>+ 插入内容</strong>
      <div className="insert-grid" style={{ marginTop: 8 }}>
        {options.map(option => (
          <button
            key={option.type}
            className="insert-option"
            onClick={() => dispatch({ type: 'insertBlock', sectionId, block: blockFactories[option.type]() })}
          >
            <strong>{option.title}</strong>
            <span>{option.description}</span>
          </button>
        ))}
      </div>
    </div>
  );
}
