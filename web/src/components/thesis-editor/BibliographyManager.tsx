import { Badge, Button, Field, Input, Select, Textarea } from '../design-system/Primitives';
import type { EditorAction } from './editorReducer';
import type { BibliographyEntry, ThesisEditorState } from './types';

export function BibliographyManager({ entries, citedKeys, dispatch }: {
  entries: BibliographyEntry[];
  citedKeys: Set<string>;
  dispatch: React.Dispatch<EditorAction>;
}) {
  return (
    <div className="stack" data-testid="bibliography-manager">
      <div className="inline-row" style={{ justifyContent: 'space-between' }}>
        <strong>参考文献</strong>
        <Button type="button" onClick={() => dispatch({ type: 'addBibliographyEntry' })}>添加文献</Button>
      </div>
      {entries.length === 0 ? <p className="muted">还没有参考文献。添加后可在正文插入 citation marker。</p> : null}
      {entries.map(entry => (
        <div key={entry.key} className="section-card" style={{ margin: 0 }}>
          <div className="section-header">
            <Badge tone={citedKeys.has(entry.key) ? 'success' : 'neutral'}>{citedKeys.has(entry.key) ? '已引用' : '未引用'}</Badge>
            <Button type="button" variant="danger" onClick={() => window.confirm('删除该参考文献？') && dispatch({ type: 'deleteBibliographyEntry', key: entry.key })}>删除</Button>
          </div>
          <div className="block-body stack">
            <Field label="Key">
              <Input value={entry.key} aria-label={`参考文献 key ${entry.key}`} onChange={event => dispatch({ type: 'updateBibliographyEntry', key: entry.key, patch: { key: event.target.value } })} />
            </Field>
            <Field label="类型">
              <Select value={entry.entryType} aria-label={`参考文献类型 ${entry.key}`} onChange={event => dispatch({ type: 'updateBibliographyEntry', key: entry.key, patch: { entryType: event.target.value as ThesisEditorState['bibliography'][number]['entryType'] } })}>
                <option value="book">book</option>
                <option value="journal">journal</option>
                <option value="web">web</option>
                <option value="other">other</option>
              </Select>
            </Field>
            <Field label="显示文本">
              <Textarea value={entry.text} aria-label={`参考文献文本 ${entry.key}`} onChange={event => dispatch({ type: 'updateBibliographyEntry', key: entry.key, patch: { text: event.target.value } })} />
            </Field>
          </div>
        </div>
      ))}
    </div>
  );
}
