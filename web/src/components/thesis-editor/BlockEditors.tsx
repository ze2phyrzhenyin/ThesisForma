import { useRef } from 'react';
import { assetApi } from '../../api/client';
import { Button, Field, Input, Select, Textarea } from '../design-system/Primitives';
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
            {[1, 2, 3, 4, 5, 6].map(level => <option key={level} value={level}>Heading {level}</option>)}
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
      <p className="muted">标题编号和样式由模板自动控制。</p>
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
  return (
    <div className="stack">
      <Field label="表名">
        <Input
          aria-label="表名"
          value={block.caption}
          onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, caption: event.target.value } })}
        />
      </Field>
      <div className="inline-row">
        <Button type="button" onClick={() => dispatch({ type: 'addTableRow', blockId: block.id })}>添加行</Button>
        <Button type="button" onClick={() => dispatch({ type: 'addTableColumn', blockId: block.id })}>添加列</Button>
        <label className="inline-row">
          <input
            type="checkbox"
            checked={block.repeatHeaderRows === 1}
            onChange={event => dispatch({ type: 'updateBlock', blockId: block.id, block: { ...block, repeatHeaderRows: event.target.checked ? 1 : undefined, rows: block.rows.map((row, index) => ({ ...row, isHeader: event.target.checked && index === 0 })) } })}
          />
          第一行为表头
        </label>
      </div>
      <table className="table-editor" aria-label="表格编辑器">
        <tbody>
          {block.rows.map(row => (
            <tr key={row.id}>
              {row.cells.map((cell, columnIndex) => (
                <td key={cell.id}>
                  <Input
                    aria-label={`单元格 ${row.id} ${columnIndex + 1}`}
                    value={cell.text}
                    onChange={event => dispatch({ type: 'updateTableCell', blockId: block.id, rowId: row.id, cellId: cell.id, text: event.target.value })}
                    onKeyDown={event => {
                      if (event.key === 'Tab') {
                        event.currentTarget.blur();
                      }
                    }}
                  />
                </td>
              ))}
              <td style={{ width: 40 }}>
                <Button type="button" variant="danger" onClick={() => dispatch({ type: 'deleteTableRow', blockId: block.id, rowId: row.id })}>删</Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="inline-row">
        {block.rows[0]?.cells.map((cell, index) => (
          <Button key={cell.id} type="button" variant="danger" onClick={() => dispatch({ type: 'deleteTableColumn', blockId: block.id, columnIndex: index })}>
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
      <div className="inline-row">
        {block.previewUrl ? <img src={block.previewUrl} alt={block.altText || block.caption} className="thumb" /> : <div className="thumb" aria-label="图片占位" />}
        <Button type="button" onClick={() => inputRef.current?.click()}>上传图片</Button>
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
      <p className="muted">图片宽度和题注位置由模板控制。</p>
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
        <option value="">选择参考文献</option>
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
