import { useEffect, useState } from 'react';
import { assetApi } from '../../api/client';
import { Button, Checkbox, Field, InlineAlert, Input, Modal } from '../ui/Primitives';
import { blockFactories, type EditorAction } from './editorReducer';
import { createTableBlock } from './serialization';

const GROUPS = [
  {
    title: '常用',
    items: [
      { type: 'heading', title: '标题', description: '一级到六级标题，自动进入大纲。' },
      { type: 'paragraph', title: '正文段落', description: '录入正文内容，可插入引用和交叉引用。' },
      { type: 'table', title: '表格', description: '设置表名、行列和表头，再填写单元格。' },
      { type: 'figure', title: '图片', description: '上传图片并填写图名和替代文本。' }
    ]
  },
  {
    title: '学术元素',
    items: [
      { type: 'equation', title: '公式', description: '输入简单公式或公式占位。' },
      { type: 'footnote', title: '脚注', description: '在正文段落中插入脚注 marker。' },
      { type: 'citation', title: '引用', description: '从参考文献 key 中选择后插入正文。' },
      { type: 'reference', title: '交叉引用', description: '引用已有标题、图、表或公式。' }
    ]
  },
  {
    title: '结构',
    items: [
      { type: 'pageBreak', title: '分页', description: '插入结构化分页标记。' },
      { type: 'bibliography', title: '参考文献条目', description: '在右侧文献管理中添加条目。' },
      { type: 'appendix', title: '附录提示', description: '在附录 section 中继续添加标题或段落。' }
    ]
  }
] as const;

export function InsertBlockMenu({
  sectionId,
  dispatch
}: {
  sectionId: string;
  dispatch: React.Dispatch<EditorAction>;
}) {
  const [modal, setModal] = useState<'table' | 'figure' | undefined>();
  const [tableCaption, setTableCaption] = useState('表名待填写');
  const [rows, setRows] = useState(3);
  const [columns, setColumns] = useState(3);
  const [hasHeader, setHasHeader] = useState(true);
  const [figureCaption, setFigureCaption] = useState('图名待填写');
  const [figureAlt, setFigureAlt] = useState('');
  const [figureFile, setFigureFile] = useState<File>();
  const [figurePreview, setFigurePreview] = useState('');

  useEffect(() => {
    if (!figureFile || typeof URL.createObjectURL !== 'function') {
      setFigurePreview('');
      return;
    }
    const url = URL.createObjectURL(figureFile);
    setFigurePreview(url);
    return () => URL.revokeObjectURL(url);
  }, [figureFile]);

  function insert(type: string) {
    if (type === 'table') return setModal('table');
    if (type === 'figure') return setModal('figure');
    if (type === 'bibliography') return dispatch({ type: 'addBibliographyEntry' });
    if (type === 'citation' || type === 'reference' || type === 'footnote') {
      alert('请先选中正文段落，再在段落块内插入引用、交叉引用或脚注。');
      return;
    }
    if (type === 'appendix') {
      dispatch({ type: 'insertBlock', sectionId: 'appendix', block: blockFactories.heading() });
      return;
    }
    dispatch({
      type: 'insertBlock',
      sectionId,
      block: blockFactories[type as keyof typeof blockFactories]()
    });
  }

  function createTable() {
    const block = createTableBlock(Math.max(1, rows), Math.max(1, columns), tableCaption);
    if (hasHeader) {
      block.repeatHeaderRows = 1;
      block.rows = block.rows.map((row, index) => ({ ...row, isHeader: index === 0 }));
    }
    dispatch({ type: 'insertBlock', sectionId, block });
    setModal(undefined);
  }

  async function createFigure() {
    const block = blockFactories.figure();
    if (block.type === 'figure') {
      if (figureFile) {
        const asset = await assetApi.uploadImage(figureFile);
        dispatch({ type: 'addAsset', asset });
        dispatch({
          type: 'insertBlock',
          sectionId,
          block: {
            ...block,
            caption: figureCaption,
            altText: figureAlt,
            imagePath: asset.imagePath,
            previewUrl: asset.previewUrl,
            imageContentType: asset.contentType
          }
        });
        setModal(undefined);
        setFigureFile(undefined);
        return;
      }
      dispatch({
        type: 'insertBlock',
        sectionId,
        block: { ...block, caption: figureCaption, altText: figureAlt }
      });
    }
    setModal(undefined);
  }

  return (
    <div className="insert-menu">
      <div className="insert-menu-head">
        <strong>+ 插入内容</strong>
        <span>选择结构块，而不是手工排版。</span>
      </div>
      <div className="insert-groups">
        {GROUPS.map(group => (
          <div className="insert-group" key={group.title}>
            <div className="insert-group-title">{group.title}</div>
            {group.items.map(item => (
              <button
                key={item.type}
                type="button"
                className="insert-option"
                aria-label={`插入${item.title}`}
                onClick={() => insert(item.type)}
              >
                <strong>{item.title}</strong>
                <span>{item.description}</span>
              </button>
            ))}
          </div>
        ))}
      </div>

      {modal === 'table' ? (
        <Modal
          title="插入表格"
          description="只设置结构信息，表格边框和题注格式由模板控制。"
          onClose={() => setModal(undefined)}
        >
          <div className="stack">
            <Field label="表名">
              <Input
                value={tableCaption}
                onChange={event => setTableCaption(event.target.value)}
              />
            </Field>
            <div className="row">
              <Field label="行数">
                <Input
                  type="number"
                  min={1}
                  max={30}
                  value={rows}
                  onChange={event => setRows(clampNumber(event.target.value, 1, 30))}
                />
              </Field>
              <Field label="列数">
                <Input
                  type="number"
                  min={1}
                  max={12}
                  value={columns}
                  onChange={event => setColumns(clampNumber(event.target.value, 1, 12))}
                />
              </Field>
              <Checkbox
                label="第一行为表头"
                checked={hasHeader}
                onChange={event => setHasHeader(event.target.checked)}
              />
            </div>
            <div className="row" style={{ justifyContent: 'flex-end' }}>
              <Button type="button" onClick={() => { setRows(3); setColumns(3); setHasHeader(true); }}>
                常用 3x3
              </Button>
              <Button onClick={() => setModal(undefined)}>取消</Button>
              <Button variant="primary" onClick={createTable}>
                创建表格
              </Button>
            </div>
          </div>
        </Modal>
      ) : null}

      {modal === 'figure' ? (
        <Modal
          title="插入图片"
          description="先创建图片结构块，进入块内上传图片和维护资产。"
          onClose={() => setModal(undefined)}
        >
          <div className="stack">
            <InlineAlert tone="muted" title="图片资产策略">
              前端模式会保留本地预览和资产元数据；真正写入 DOCX 需要后端资产服务。
            </InlineAlert>
            <div className="figure-insert-preview">
              {figurePreview ? <img src={figurePreview} alt={figureAlt || figureCaption} /> : <span>可先选择图片，也可以只创建图片占位块。</span>}
            </div>
            <Field label="选择图片">
              <Input
                type="file"
                accept="image/png,image/jpeg,image/webp,image/gif"
                onChange={event => setFigureFile(event.target.files?.[0])}
              />
            </Field>
            <Field label="图名">
              <Input
                value={figureCaption}
                onChange={event => setFigureCaption(event.target.value)}
              />
            </Field>
            <Field label="替代文本">
              <Input
                value={figureAlt}
                placeholder="用于无障碍说明和资产追踪"
                onChange={event => setFigureAlt(event.target.value)}
              />
            </Field>
            <div className="row" style={{ justifyContent: 'flex-end' }}>
              <Button onClick={() => setModal(undefined)}>取消</Button>
              <Button variant="primary" onClick={() => void createFigure()}>
                {figureFile ? '上传并插入图片块' : '插入图片块'}
              </Button>
            </div>
          </div>
        </Modal>
      ) : null}
    </div>
  );
}

function clampNumber(value: string, min: number, max: number) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return min;
  return Math.min(max, Math.max(min, Math.round(parsed)));
}
