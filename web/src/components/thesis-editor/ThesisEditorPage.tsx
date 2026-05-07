import { useEffect, useMemo, useReducer, useRef, useState } from 'react';
import { appConfig, documentApi, renderApi, templateApi } from '../../api/client';
import { Card, EmptyState, InlineAlert, Panel, Select, Tabs } from '../ui/Primitives';
import { BibliographyManager } from './BibliographyManager';
import { BlockEditor } from './BlockEditor';
import { EditorToolbar } from './EditorToolbar';
import { InsertBlockMenu } from './InsertBlockMenu';
import { MetadataForm } from './MetadataForm';
import { OutlinePanel } from './OutlinePanel';
import { RenderPanel } from './RenderPanel';
import { TemplateStatusPanel } from './TemplateStatusPanel';
import { TocPreview } from './TocPreview';
import { ValidationPanel } from './ValidationPanel';
import { editorReducer, localValidate, type EditorAction } from './editorReducer';
import {
  collectReferenceTargets,
  createInitialState,
  deserializeFromThesisDocument,
  serializeToThesisDocument
} from './serialization';
import type { TemplateSummary, ThesisEditorState } from './types';

type ThesisEditorPageProps = {
  initialState?: ThesisEditorState;
  onStateChange?: (state: ThesisEditorState) => void;
  onHome?: () => void;
  onTemplates?: () => void;
  onBack?: () => void;
};

const SECTION_KIND_LABEL: Record<string, string> = {
  cover: '封面',
  originalityStatement: '原创性声明',
  abstract: '摘要',
  toc: '目录',
  body: '正文',
  acknowledgements: '致谢',
  bibliography: '参考文献',
  appendix: '附录'
};

const BLOCK_LABEL: Record<string, string> = {
  heading: '标题',
  paragraph: '正文段落',
  abstract: '摘要',
  table: '表格',
  figure: '图片',
  equation: '公式',
  pageBreak: '分页'
};

export function ThesisEditorPage({
  initialState,
  onStateChange,
  onHome,
  onTemplates,
  onBack
}: ThesisEditorPageProps) {
  const [historyState, dispatchWithHistory] = useReducer(
    editorHistoryReducer,
    initialState ?? createInitialState(),
    state => ({ past: [], present: state, future: [] })
  );
  const state = historyState.present;
  const dispatch = dispatchWithHistory as React.Dispatch<EditorAction>;

  const [templates, setTemplates] = useState<TemplateSummary[]>([]);
  const [activeTab, setActiveTab] = useState('properties');
  const [toast, setToast] = useState<string>();
  const importInputRef = useRef<HTMLInputElement>(null);

  const referenceTargets = useMemo(() => collectReferenceTargets(state), [state]);
  const citedKeys = useMemo(() => collectCitations(state), [state]);
  const selectedBlock = useMemo(
    () => state.sections.flatMap(s => s.blocks).find(b => b.id === state.selectedBlockId),
    [state]
  );

  useEffect(() => {
    onStateChange?.(state);
  }, [onStateChange, state]);

  useEffect(() => {
    void templateApi
      .list()
      .then(items => {
        setTemplates(items);
        const selected = items.find(item => item.id === state.templateId);
        if (selected)
          dispatch({ type: 'setTemplate', templateId: selected.id, template: selected });
      })
      .catch(() => undefined);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
        event.preventDefault();
        void save();
      }
      if (
        (event.ctrlKey || event.metaKey) &&
        event.key.toLowerCase() === 'z' &&
        !event.shiftKey
      ) {
        event.preventDefault();
        dispatchWithHistory({ type: 'undo' });
      }
      if (
        (event.ctrlKey || event.metaKey) &&
        (event.key.toLowerCase() === 'y' ||
          (event.key.toLowerCase() === 'z' && event.shiftKey))
      ) {
        event.preventDefault();
        dispatchWithHistory({ type: 'redo' });
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  });

  useEffect(() => {
    if (!toast) return;
    const handle = window.setTimeout(() => setToast(undefined), 3200);
    return () => window.clearTimeout(handle);
  }, [toast]);

  async function save() {
    dispatch({ type: 'setAutosaveStatus', status: 'saving' });
    try {
      const document = serializeToThesisDocument(state);
      if (state.documentId) {
        await documentApi.save(state.documentId, document, state.templateId);
        dispatch({ type: 'setAutosaveStatus', status: 'saved' });
        return state.documentId;
      }
      const created = await documentApi.create({
        templateId: state.templateId,
        title: state.metadata.title
      });
      dispatch({ type: 'setDocumentId', id: created.id });
      await documentApi.save(created.id, document, state.templateId);
      dispatch({ type: 'setAutosaveStatus', status: 'saved' });
      return created.id;
    } catch {
      dispatch({ type: 'setAutosaveStatus', status: 'failed' });
      return undefined;
    }
  }

  async function validate() {
    const localIssues = localValidate(state);
    dispatch({ type: 'setValidationIssues', issues: localIssues });
    if (!state.documentId || localIssues.some(i => i.severity === 'error')) return;
    try {
      const response = await documentApi.validate(state.documentId, state.templateId);
      dispatch({ type: 'setValidationIssues', issues: response.issues });
    } catch (error) {
      dispatch({
        type: 'setValidationIssues',
        issues: [
          {
            code: 'api.validate.failed',
            severity: 'error',
            message: error instanceof Error ? error.message : '服务端校验失败。'
          }
        ]
      });
    }
  }

  async function render() {
    if (!appConfig.docxRenderEnabled) {
      dispatch({
        type: 'setRenderRun',
        run: await renderApi.render(state.documentId ?? 'frontend-only', state.templateId)
      });
      return;
    }
    const documentId = await save();
    if (!documentId) return;
    await validate();
    try {
      const run = await renderApi.render(documentId, state.templateId);
      dispatch({ type: 'setRenderRun', run });
    } catch (error) {
      dispatch({
        type: 'setValidationIssues',
        issues: [
          {
            code: 'api.render.failed',
            severity: 'error',
            message: error instanceof Error ? error.message : '生成 DOCX 失败。'
          }
        ]
      });
    }
  }

  function exportJson() {
    const issues = localValidate(state);
    dispatch({ type: 'setValidationIssues', issues });
    const document = serializeToThesisDocument(state);
    const blob = new Blob([JSON.stringify(document, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const anchor = window.document.createElement('a');
    anchor.href = url;
    anchor.download = `${safeDownloadName(state.metadata.title || 'thesis-document')}.json`;
    window.document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
    setToast(
      issues.some(issue => issue.severity === 'error')
        ? '已导出草稿 JSON；仍建议修复校验错误。'
        : 'ThesisDocument JSON 已导出。'
    );
  }

  async function importJson(file?: File) {
    if (!file) return;
    const text = await readFileText(file);
    const document = JSON.parse(text);
    dispatch({
      type: 'replaceState',
      state: deserializeFromThesisDocument(document, state.templateId)
    });
  }

  function jumpToBlock(blockId?: string) {
    dispatch({ type: 'selectBlock', blockId });
    if (blockId) {
      window.document
        .querySelector(`[data-block-id="${blockId}"]`)
        ?.scrollIntoView({ block: 'center' });
    }
  }

  return (
    <div className="app">
      <EditorToolbar
        state={state}
        onSave={() => void save()}
        onValidate={() => void validate()}
        onRender={() => void render()}
        onExportJson={exportJson}
        onImportJson={() => importInputRef.current?.click()}
        onHome={onHome}
        onTemplates={onTemplates}
        onBack={onBack}
        onUndo={() => dispatchWithHistory({ type: 'undo' })}
        onRedo={() => dispatchWithHistory({ type: 'redo' })}
        canUndo={historyState.past.length > 0}
        canRedo={historyState.future.length > 0}
        docxRenderEnabled={appConfig.docxRenderEnabled}
      />

      <input
        ref={importInputRef}
        type="file"
        accept="application/json,.json"
        hidden
        aria-label="导入 ThesisDocument JSON"
        onChange={event => void importJson(event.target.files?.[0])}
      />

      <main className="editor" data-testid="three-column-layout">
        <div className="col col-outline">
          <OutlinePanel state={state} onSelect={jumpToBlock} />
        </div>

        <div className="col col-canvas">
          <div className="canvas-wrap">
            <article className="canvas">
              <header className="canvas-head">
                <span className="eyebrow">{state.template?.name ?? state.templateId}</span>
                <h1>{state.metadata.title || '未命名论文'}</h1>
                <p className="subline">
                  结构化内容画布。最终格式由模板渲染，不在这里手工排版。
                </p>
                <div className="canvas-meta-bar">
                  <span>{state.sections.length} sections</span>
                  <span>{state.sections.flatMap(s => s.blocks).length} blocks</span>
                  <span>{state.bibliography.length} 参考文献</span>
                </div>
              </header>

              <Card title="论文元信息" description="封面字段和模板变量来自这些结构化元信息。">
                <MetadataForm metadata={state.metadata} dispatch={dispatch} />
              </Card>

              {state.sections.map(section => (
                <section
                  key={section.id}
                  className="section"
                  data-testid={`section-${section.id}`}
                >
                  <div className="section-head">
                    <span className="section-eyebrow">{section.title}</span>
                    <span className="section-meta">
                      <span>{SECTION_KIND_LABEL[section.kind] ?? section.kind}</span>
                      <span>{section.blocks.length} blocks</span>
                    </span>
                  </div>

                  {section.kind === 'toc' ? <TocPreview state={state} /> : null}

                  {section.blocks.length === 0 && section.kind !== 'toc' ? (
                    <EmptyState
                      title="这个 section 还没有内容"
                      description="使用下方插入菜单添加标题、段落、表格、图片或学术元素。"
                    />
                  ) : null}

                  {section.blocks.map(block => (
                    <BlockEditor
                      key={block.id}
                      block={block}
                      active={state.selectedBlockId === block.id}
                      dispatch={dispatch}
                      bibliographyKeys={state.bibliography.map(entry => entry.key)}
                      referenceTargets={referenceTargets}
                      issues={state.validationIssues.filter(issue => issue.blockId === block.id)}
                    />
                  ))}

                  {section.kind !== 'toc' &&
                  section.kind !== 'cover' &&
                  section.kind !== 'originalityStatement' ? (
                    <InsertBlockMenu sectionId={section.id} dispatch={dispatch} />
                  ) : null}
                </section>
              ))}
            </article>
          </div>
        </div>

        <div className="col col-side">
          <Panel
            title="编辑辅助"
            description="属性、校验、引用和模板提示。"
            contentClassName=""
          >
            <div className="side-tabs">
              <Tabs
                active={activeTab}
                onChange={setActiveTab}
                tabs={[
                  { id: 'properties', label: '属性' },
                  { id: 'validation', label: '校验', badge: state.validationIssues.length },
                  { id: 'references', label: '引用' },
                  { id: 'template', label: '模板' }
                ]}
              />
            </div>
            <div className="side-content">
              {activeTab === 'properties' ? (
                <>
                  <InlineAlert title="当前内容块">
                    {selectedBlock
                      ? `${BLOCK_LABEL[selectedBlock.type] ?? selectedBlock.type}：${selectedBlock.id}`
                      : '尚未选中内容块。点击正文、表格或图片块可查看属性。'}
                  </InlineAlert>
                  <RenderPanel
                    run={state.renderRun}
                    onRender={() => void render()}
                    docxRenderEnabled={appConfig.docxRenderEnabled}
                  />
                </>
              ) : null}
              {activeTab === 'validation' ? (
                <ValidationPanel issues={state.validationIssues} onJump={jumpToBlock} />
              ) : null}
              {activeTab === 'references' ? (
                <BibliographyManager
                  entries={state.bibliography}
                  citedKeys={citedKeys}
                  dispatch={dispatch}
                />
              ) : null}
              {activeTab === 'template' ? (
                <>
                  <Select
                    aria-label="选择模板"
                    value={state.templateId}
                    onChange={event => {
                      const template = templates.find(item => item.id === event.target.value);
                      dispatch({
                        type: 'setTemplate',
                        templateId: event.target.value,
                        template
                      });
                    }}
                  >
                    {templates.map(template => (
                      <option key={template.id} value={template.id}>
                        {template.name}
                      </option>
                    ))}
                  </Select>
                  <TemplateStatusPanel template={state.template} />
                </>
              ) : null}
            </div>
          </Panel>
        </div>
      </main>

      {toast ? (
        <div className="toast" role="status">
          {toast}
        </div>
      ) : null}
    </div>
  );
}

function safeDownloadName(value: string) {
  return (
    value
      .trim()
      .replace(/[^\p{L}\p{N}._-]+/gu, '-')
      .replace(/^-|-$/g, '') || 'thesis-document'
  );
}

function readFileText(file: File) {
  if (typeof file.text === 'function') return file.text();
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error);
    reader.onload = () => resolve(String(reader.result ?? ''));
    reader.readAsText(file);
  });
}

function collectCitations(state: ThesisEditorState) {
  const keys = new Set<string>();
  for (const block of state.sections.flatMap(section => section.blocks)) {
    if (block.type === 'paragraph') {
      for (const inline of block.inlines) {
        if (inline.type === 'citation') keys.add(inline.targetId);
      }
    }
  }
  return keys;
}

type EditorHistoryState = {
  past: ThesisEditorState[];
  present: ThesisEditorState;
  future: ThesisEditorState[];
};

type EditorHistoryAction = EditorAction | { type: 'undo' } | { type: 'redo' };

function editorHistoryReducer(
  history: EditorHistoryState,
  action: EditorHistoryAction
): EditorHistoryState {
  if (action.type === 'undo') {
    const previous = history.past.at(-1);
    if (!previous) return history;
    return {
      past: history.past.slice(0, -1),
      present: previous,
      future: [history.present, ...history.future].slice(0, 50)
    };
  }
  if (action.type === 'redo') {
    const next = history.future[0];
    if (!next) return history;
    return {
      past: [...history.past, history.present].slice(-50),
      present: next,
      future: history.future.slice(1)
    };
  }
  const next = editorReducer(history.present, action);
  if (Object.is(next, history.present)) return history;
  if (!isUndoableAction(action)) return { ...history, present: next };
  return {
    past: [...history.past, history.present].slice(-50),
    present: next,
    future: []
  };
}

function isUndoableAction(action: EditorAction) {
  return ![
    'setDocumentId',
    'setTemplate',
    'setAutosaveStatus',
    'selectBlock',
    'setValidationIssues',
    'setRenderRun'
  ].includes(action.type);
}
