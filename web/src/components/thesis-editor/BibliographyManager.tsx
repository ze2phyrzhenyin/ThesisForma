import { Badge, Button, EmptyState, Field, Input, Select, Textarea } from '../design-system/Primitives';
import type { EditorAction } from './editorReducer';
import type { BibliographyEntry, ThesisEditorState } from './types';

const entryTypeLabels: Record<BibliographyEntry['entryType'], string> = {
  book: '专著',
  journal: '期刊',
  web: '网页',
  other: '其他'
};

const entryTypeTones: Record<BibliographyEntry['entryType'], 'info' | 'success' | 'neutral' | 'warning'> = {
  book: 'info',
  journal: 'success',
  web: 'neutral',
  other: 'neutral'
};

export function BibliographyManager({ entries, citedKeys, dispatch }: {
  entries: BibliographyEntry[];
  citedKeys: Set<string>;
  dispatch: React.Dispatch<EditorAction>;
}) {
  return (
    <div className="stack" data-testid="bibliography-manager">
      <div className="bibliography-toolbar">
        <div>
          <strong>参考文献</strong>
          <p className="muted">正文 citation 只能引用这里存在的 key。</p>
        </div>
        <Button type="button" onClick={() => dispatch({ type: 'addBibliographyEntry' })}>添加文献</Button>
      </div>
      {entries.length === 0 ? (
        <EmptyState
          title="还没有参考文献"
          description="添加条目后，可在正文段落中插入引用 marker。"
        />
      ) : null}
      {entries.map(entry => (
        <div key={entry.key} className="bibliography-item">
          <div className="bibliography-item-header">
            <div className="bibliography-item-badges">
              <Badge tone="info">{entry.key || '未命名 key'}</Badge>
              <Badge tone={entryTypeTones[entry.entryType]}>{entryTypeLabels[entry.entryType]}</Badge>
              <Badge tone={citedKeys.has(entry.key) ? 'success' : 'neutral'}>
                {citedKeys.has(entry.key) ? '已引用' : '未引用'}
              </Badge>
            </div>
            <Button
              type="button"
              variant="danger"
              onClick={() => window.confirm('删除该参考文献？') && dispatch({ type: 'deleteBibliographyEntry', key: entry.key })}
              aria-label={`删除参考文献 ${entry.key}`}
            >
              删除
            </Button>
          </div>
          <div className="bibliography-item-body stack">
            <Field label="引用 Key">
              <Input
                value={entry.key}
                aria-label={`参考文献 key ${entry.key}`}
                onChange={event => dispatch({ type: 'updateBibliographyEntry', key: entry.key, patch: { key: event.target.value } })}
              />
            </Field>
            <Field label="文献类型">
              <Select
                value={entry.entryType}
                aria-label={`参考文献类型 ${entry.key}`}
                onChange={event => dispatch({
                  type: 'updateBibliographyEntry',
                  key: entry.key,
                  patch: { entryType: event.target.value as ThesisEditorState['bibliography'][number]['entryType'] }
                })}
              >
                <option value="book">专著 (book)</option>
                <option value="journal">期刊 (journal)</option>
                <option value="web">网页 (web)</option>
                <option value="other">其他 (other)</option>
              </Select>
            </Field>
            <Field label="显示文本" hint="完整参考文献格式，按模板要求填写。">
              <Textarea
                value={entry.text}
                aria-label={`参考文献文本 ${entry.key}`}
                onChange={event => dispatch({ type: 'updateBibliographyEntry', key: entry.key, patch: { text: event.target.value } })}
              />
            </Field>
          </div>
        </div>
      ))}
    </div>
  );
}
