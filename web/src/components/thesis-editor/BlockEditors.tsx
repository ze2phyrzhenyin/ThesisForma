import { useRef } from 'react';
import { assetApi } from '../../api/client';
import { Badge, Button, Checkbox, Field, InlineAlert, Input, Select, Textarea } from '../design-system/Primitives';
import type { EditorAction } from './editorReducer';
import { setParagraphText } from './editorReducer';
import type { BlockNode } from './types';

export function HeadingBlockEditor({ block, dispatch }: { block: Extract<BlockNode, { type: 'heading' }>; dispatch: React.Dispatch<EditorAction> }) {
  return (
    <div className="stack">
      <div className="inline-row">
        <Field label="标题级别">
          <Select
            aria-label="标题级别"
            value={block.level}
            onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, level: Number(event.target.value) } })}
          >
            {[1, 2, 3, 4, 5, 6].map(level => <option key={level} value={level}>{level} 级标题</option>)}
          </Select>
        </Field>
        <Field label="标题文本">
          <Input
            aria-label="标题文本"
            value={block.text}
            onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, text: event.target.value } })}
          />
        </Field>
      </div>
      <InlineAlert title="标题格式由模板控制">这里只维护标题层级和标题文本，不手动设置字体字号。</InlineAlert>
    </div>
  );
}

export function ParagraphBlockEditor({ block, dispatch, bibliographyKeys, referenceTargets }: {
  block: Extract<BlockNode, { type: 'paragraph' }>;
  dispatch: React.Dispatch<EditorAction>;
  bibliographyKeys: string[];
  referenceTargets: Array<{ id: string; label: string; type: string }>;
}) {
  const text = block.inlines.filter(inline => inline.type === 'text').map(inline => inline.text).join('');
  return (
    <div className="stack">
      <Field label="正文段落">
        <Textarea
          aria-label="正文段落"
          value={text}
          onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: setParagraphText(block, event.target.value) })}
        />
      </Field>
      <div className="inline-row">
        <CitationPicker blockId={block.id} bibliographyKeys={bibliographyKeys} dispatch={dispatch} />
        <CrossReferencePicker blockId={block.id} referenceTargets={referenceTargets} dispatch={dispatch} />
        <Button
          type="button"
          onClick={() => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, inlines: [...block.inlines, { type: 'footnote', noteId: `fn-${Date.now()}`, inlines: [{ type: 'text', text: '脚注内容' }] }] } })}
        >
          插入脚注
        </Button>
      </div>
    </div>
  );
}

export function TableBlockEditor({ block, dispatch }: { block: Extract<BlockNode, { type: 'table' }>; dispatch: React.Dispatch<EditorAction> }) {
  const rowCount = block.rows.length;
  const columnCount = block.rows[0]?.cells.length ?? 0;
  return (
    <div className="table-shell">
      <div className="table-caption-row">
        <Field label="表名" error={!block.caption.trim() ? '表格需要表名，导出时由模板决定表题位置。' : undefined}>
          <Input
            aria-label="表名"
            placeholder="例如：样本信息统计表"
            value={block.caption}
            onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, caption: event.target.value } })}
          />
        </Field>
        <div className="stack-tight">
          <span className="table-meta">{rowCount} 行 / {columnCount} 列</span>
          <Checkbox
            label="第一行为表头"
            checked={block.repeatHeaderRows === 1}
            onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, repeatHeaderRows: event.target.checked ? 1 : undefined, rows: block.rows.map((row, index) => ({ ...row, isHeader: event.target.checked && index === 0 })) } })}
          />
        </div>
      </div>
      <InlineAlert title="表格边框由模板控制">这里仅编辑表名、行列和单元格文本，不设置三线表或边框粗细。</InlineAlert>
      <div className="table-row-actions">
        <Button type="button" onClick={() => dispatch({ type: 'addTableRow', blockId: block.id })}>添加行</Button>
        <Button type="button" onClick={() => dispatch({ type: 'addTableColumn', blockId: block.id })}>添加列</Button>
      </div>
      <div className="table-scroll">
        <table className="table-editor" aria-label="表格编辑器">
          <tbody>
            {block.rows.map(row => (
              <tr key={row.id} className={row.isHeader ? 'is-header' : undefined}>
                {row.cells.map((cell, columnIndex) => (
                  <td key={cell.id}>
                    <Input
                      aria-label={`单元格 ${row.id} ${columnIndex + 1}`}
                      value={cell.text}
                      onChange={event => dispatch({ type: 'updateTableCell', blockId: block.id, rowId: row.id, cellId: cell.id, text: event.target.value })}
                      onKeyDown={event => {
                        if (event.key === 'Tab') event.currentTarget.blur();
                      }}
                    />
                  </td>
                ))}
                <td>
                  <Button type="button" variant="danger" onClick={() => window.confirm('删除这一行？') && dispatch({ type: 'deleteTableRow', blockId: block.id, rowId: row.id })}>删除行</Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="table-column-actions">
        {block.rows[0]?.cells.map((cell, index) => (
          <Button key={cell.id} type="button" variant="danger" onClick={() => window.confirm(`删除第 ${index + 1} 列？`) && dispatch({ type: 'deleteTableColumn', blockId: block.id, columnIndex: index })}>
            删除第 {index + 1} 列
          </Button>
        ))}
      </div>
    </div>
  );
}

export function FigureBlockEditor({ block, dispatch }: { block: Extract<BlockNode, { type: 'figure' }>; dispatch: React.Dispatch<EditorAction> }) {
  const inputRef = useRef<HTMLInputElement>(null);
  async function upload(file: File) {
    const asset = await assetApi.uploadImage(file);
    dispatch({ type: 'addAsset', asset });
    dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, imagePath: asset.imagePath, previewUrl: asset.previewUrl, imageContentType: asset.contentType } });
  }
  return (
    <div className="stack">
      <div className="figure-upload">
        {block.previewUrl ? <img src={block.previewUrl} alt={block.altText || block.caption} className="thumb" /> : <div className="thumb thumb-placeholder" aria-label="图片占位">等待上传</div>}
        <div className="stack-tight">
          <div className="inline-row">
            <Button type="button" onClick={() => inputRef.current?.click()}>{block.previewUrl ? '更换图片' : '上传图片'}</Button>
            <Badge tone={block.imagePath ? 'success' : 'warning'}>{block.imagePath ? '本地资产已记录' : '缺少图片'}</Badge>
          </div>
          <p className="muted">Vercel 前端模式只保存本地预览与资产元数据；最终 DOCX 插图需要后端资产服务。</p>
        </div>
        <input
          ref={inputRef}
          type="file"
          accept="image/png,image/jpeg,image/gif,image/bmp"
          hidden
          onChange={event => {
            const file = event.target.files?.[0];
            if (file) void upload(file);
          }}
        />
      </div>
      <Field label="图名">
        <Input aria-label="图名" value={block.caption} onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, caption: event.target.value } })} />
      </Field>
      <Field label="替代文本">
        <Input aria-label="替代文本" value={block.altText} onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, altText: event.target.value } })} />
      </Field>
      <InlineAlert title="图片格式由模板控制">图片宽度、居中方式和图题位置由模板统一决定。</InlineAlert>
    </div>
  );
}

export function EquationBlockEditor({ block, dispatch }: { block: Extract<BlockNode, { type: 'equation' }>; dispatch: React.Dispatch<EditorAction> }) {
  return (
    <div className="stack">
      <Field label="公式">
        <Input aria-label="公式" value={block.plainText} onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, plainText: event.target.value } })} />
      </Field>
      <Field label="公式说明">
        <Input aria-label="公式说明" value={block.caption ?? ''} onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, caption: event.target.value } })} />
      </Field>
    </div>
  );
}

export function AbstractBlockEditor({ block, dispatch }: { block: Extract<BlockNode, { type: 'abstract' }>; dispatch: React.Dispatch<EditorAction> }) {
  return (
    <div className="stack">
      <Field label={block.language === 'zh' ? '中文摘要' : 'English Abstract'} hint={`当前约 ${block.text.length} 字符`}>
        <Textarea aria-label="摘要正文" value={block.text} onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, text: event.target.value } })} />
      </Field>
      <Field label={block.language === 'zh' ? '关键词' : 'Key words'} hint="使用分号分隔，格式由模板控制。">
        <Input aria-label="关键词" value={block.keywords.join('; ')} onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, keywords: event.target.value.split(/[;；]/).map(item => item.trim()).filter(Boolean) } })} />
      </Field>
    </div>
  );
}

export function CitationPicker({ blockId, bibliographyKeys, dispatch }: { blockId: string; bibliographyKeys: string[]; dispatch: React.Dispatch<EditorAction> }) {
  return (
    <Field label="插入引用">
      <Select aria-label="插入引用" defaultValue="" onChange={event => event.target.value && dispatch({ type: 'insertCitation', blockId, key: event.target.value })}>
        <option value="">选择参考文献 key</option>
        {bibliographyKeys.map(key => <option key={key} value={key}>{key}</option>)}
      </Select>
    </Field>
  );
}

export function CrossReferencePicker({ blockId, referenceTargets, dispatch }: { blockId: string; referenceTargets: Array<{ id: string; label: string; type: string }>; dispatch: React.Dispatch<EditorAction> }) {
  return (
    <Field label="交叉引用">
      <Select aria-label="交叉引用" defaultValue="" onChange={event => {
        const target = referenceTargets.find(item => item.id === event.target.value);
        if (target) dispatch({ type: 'insertReference', blockId, bookmarkName: target.id, label: target.label });
      }}>
        <option value="">选择标题/图/表/公式</option>
        {referenceTargets.map(target => <option key={target.id} value={target.id}>{target.type}: {target.label}</option>)}
      </Select>
    </Field>
  );
}
