import { Badge, Button, EmptyState, Field, Input, Select, Textarea } from '../ui/Primitives';
import type { EditorAction } from './editorReducer';
import { newId } from './serialization';
import type { BibliographyEntry, ThesisEditorState } from './types';

const ENTRY_LABEL: Record<BibliographyEntry['entryType'], string> = {
  book: '专著',
  journal: '期刊',
  web: '网页',
  other: '其他'
};

const ENTRY_TONE: Record<BibliographyEntry['entryType'], 'info' | 'success' | 'neutral' | 'warning'> =
  {
    book: 'info',
    journal: 'success',
    web: 'neutral',
    other: 'neutral'
  };

export function BibliographyManager({
  entries,
  citedKeys,
  dispatch
}: {
  entries: BibliographyEntry[];
  citedKeys: Set<string>;
  dispatch: React.Dispatch<EditorAction>;
}) {
  return (
    <div className="stack" data-testid="bibliography-manager">
      <div className="biblio-toolbar">
        <div>
          <strong>参考文献</strong>
          <small>正文 citation 只能引用这里存在的 key。</small>
        </div>
        <Button
          type="button"
          variant="primary"
          size="sm"
          onClick={() => dispatch({ type: 'addBibliographyEntry' })}
        >
          添加文献
        </Button>
      </div>

      <div className="biblio-quick-actions" aria-label="参考文献快速添加">
        <Button
          type="button"
          size="sm"
          onClick={() => dispatch({ type: 'addBibliographyEntry', entry: createEntry(entries.length, 'journal') })}
        >
          添加期刊
        </Button>
        <Button
          type="button"
          size="sm"
          onClick={() => dispatch({ type: 'addBibliographyEntry', entry: createEntry(entries.length, 'book') })}
        >
          添加专著
        </Button>
        <Button
          type="button"
          size="sm"
          onClick={() => dispatch({ type: 'addBibliographyEntry', entry: createEntry(entries.length, 'web') })}
        >
          添加网页
        </Button>
      </div>

      {entries.length === 0 ? (
        <EmptyState
          title="还没有参考文献"
          description="添加条目后，可在正文段落中插入引用 marker。"
        />
      ) : null}

      {entries.map(entry => (
        <div key={entry.key} className="biblio-item">
          <div className="biblio-head">
            <div className="biblio-tags">
              <Badge tone="info">{entry.key || '未命名 key'}</Badge>
              <Badge tone={ENTRY_TONE[entry.entryType]}>{ENTRY_LABEL[entry.entryType]}</Badge>
              <Badge tone={citedKeys.has(entry.key) ? 'success' : 'neutral'}>
                {citedKeys.has(entry.key) ? '已引用' : '未引用'}
              </Badge>
            </div>
            <Button
              type="button"
              variant="danger"
              size="sm"
              onClick={() =>
                window.confirm('删除该参考文献？') &&
                dispatch({ type: 'deleteBibliographyEntry', key: entry.key })
              }
              aria-label={`删除参考文献 ${entry.key}`}
            >
              删除
            </Button>
          </div>

          <Field label="引用 Key">
            <Input
              value={entry.key}
              aria-label={`参考文献 key ${entry.key}`}
              onChange={event =>
                dispatch({
                  type: 'updateBibliographyEntry',
                  key: entry.key,
                  patch: { key: event.target.value }
                })
              }
            />
          </Field>
          <Field label="文献类型">
            <Select
              value={entry.entryType}
              aria-label={`参考文献类型 ${entry.key}`}
              onChange={event =>
                dispatch({
                  type: 'updateBibliographyEntry',
                  key: entry.key,
                  patch: {
                    entryType: event.target
                      .value as ThesisEditorState['bibliography'][number]['entryType']
                  }
                })
              }
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
              onChange={event =>
                dispatch({
                  type: 'updateBibliographyEntry',
                  key: entry.key,
                  patch: { text: event.target.value }
                })
              }
            />
          </Field>
        </div>
      ))}
    </div>
  );
}

function createEntry(index: number, entryType: BibliographyEntry['entryType']): BibliographyEntry {
  const key = `ref-${index + 1}`;
  const exampleText = {
    journal: '[序号] 作者. 题名[J]. 刊名, 年份, 卷(期): 页码.',
    book: '[序号] 作者. 书名[M]. 出版地: 出版社, 年份.',
    web: '[序号] 作者. 题名[EB/OL]. URL, 访问日期.',
    other: ''
  }[entryType];
  return {
    id: newId('ref'),
    key,
    text: exampleText,
    entryType
  };
}
