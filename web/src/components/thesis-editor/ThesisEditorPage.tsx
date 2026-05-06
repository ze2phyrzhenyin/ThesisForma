import { useEffect, useMemo, useReducer, useRef, useState } from 'react';
import { appConfig, documentApi, renderApi, templateApi } from '../../api/client';
import { Card, EmptyState, Panel } from '../design-system/Primitives';
import { BlockEditor } from './BlockEditor';
import { BibliographyManager } from './BibliographyManager';
import { EditorToolbar } from './EditorToolbar';
import { InsertBlockMenu } from './InsertBlockMenu';
import { MetadataForm } from './MetadataForm';
import { OutlinePanel } from './OutlinePanel';
import { RenderPanel } from './RenderPanel';
import { TemplateStatusPanel } from './TemplateStatusPanel';
import { TocPreview } from './TocPreview';
import { ValidationPanel } from './ValidationPanel';
import { editorReducer, localValidate } from './editorReducer';
import { collectReferenceTargets, createInitialState, deserializeFromThesisDocument, serializeToThesisDocument } from './serialization';
import type { TemplateSummary, ThesisEditorState } from './types';

export function ThesisEditorPage({ initialState }: { initialState?: ThesisEditorState }) {
  const [state, dispatch] = useReducer(editorReducer, initialState ?? createInitialState());
  const [templates, setTemplates] = useState<TemplateSummary[]>([]);
  const importInputRef = useRef<HTMLInputElement>(null);
  const referenceTargets = useMemo(() => collectReferenceTargets(state), [state]);
  const citedKeys = useMemo(() => collectCitations(state), [state]);

  useEffect(() => {
    void templateApi.list().then(items => {
      setTemplates(items);
      const selected = items.find(item => item.id === state.templateId);
      if (selected) dispatch({ type: 'setTemplate', templateId: selected.id, template: selected });
    }).catch(() => undefined);
  }, []);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
        event.preventDefault();
        void save();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  });

  async function save() {
    dispatch({ type: 'setAutosaveStatus', status: 'saving' });
    try {
      const document = serializeToThesisDocument(state);
      if (state.documentId) {
        await documentApi.save(state.documentId, document, state.templateId);
        dispatch({ type: 'setAutosaveStatus', status: 'saved' });
        return state.documentId;
      } else {
        const created = await documentApi.create({ templateId: state.templateId, title: state.metadata.title });
        dispatch({ type: 'setDocumentId', id: created.id });
        await documentApi.save(created.id, document, state.templateId);
        dispatch({ type: 'setAutosaveStatus', status: 'saved' });
        return created.id;
      }
    } catch {
      dispatch({ type: 'setAutosaveStatus', status: 'failed' });
      return undefined;
    }
  }

  async function validate() {
    const localIssues = localValidate(state);
    dispatch({ type: 'setValidationIssues', issues: localIssues });
    if (!state.documentId || localIssues.some(issue => issue.severity === 'error')) return;
    try {
      const response = await documentApi.validate(state.documentId, state.templateId);
      dispatch({ type: 'setValidationIssues', issues: response.issues });
    } catch (error) {
      dispatch({ type: 'setValidationIssues', issues: [{ code: 'api.validate.failed', severity: 'error', message: error instanceof Error ? error.message : '服务端校验失败。' }] });
    }
  }

  async function render() {
    if (!appConfig.docxRenderEnabled) {
      dispatch({ type: 'setRenderRun', run: await renderApi.render(state.documentId ?? 'frontend-only', state.templateId) });
      return;
    }
    const documentId = await save();
    if (!documentId) return;
    await validate();
    try {
      const run = await renderApi.render(documentId, state.templateId);
      dispatch({ type: 'setRenderRun', run });
    } catch (error) {
      dispatch({ type: 'setValidationIssues', issues: [{ code: 'api.render.failed', severity: 'error', message: error instanceof Error ? error.message : '生成 DOCX 失败。' }] });
    }
  }

  function exportJson() {
    const document = serializeToThesisDocument(state);
    const blob = new Blob([JSON.stringify(document, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const anchor = documentNode().createElement('a');
    anchor.href = url;
    anchor.download = `${safeDownloadName(state.metadata.title || 'thesis-document')}.json`;
    documentNode().body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  }

  async function importJson(file?: File) {
    if (!file) return;
    const text = await readFileText(file);
    const document = JSON.parse(text);
    dispatch({ type: 'replaceState', state: deserializeFromThesisDocument(document, state.templateId) });
  }

  function jumpToBlock(blockId?: string) {
    dispatch({ type: 'selectBlock', blockId });
    if (blockId) {
      document.querySelector(`[data-block-id="${blockId}"]`)?.scrollIntoView({ block: 'center' });
    }
  }

  return (
    <div className="app-shell">
      <EditorToolbar
        state={state}
        onSave={() => void save()}
        onValidate={() => void validate()}
        onRender={() => void render()}
        onExportJson={exportJson}
        onImportJson={() => importInputRef.current?.click()}
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
      <main className="editor-layout" data-testid="three-column-layout">
        <div className="outline-panel">
          <OutlinePanel state={state} onSelect={jumpToBlock} />
        </div>
        <div className="editor-paper">
          <div className="paper-inner">
            <Card title="论文元信息">
              <MetadataForm metadata={state.metadata} dispatch={dispatch} />
            </Card>
            <div style={{ height: 16 }} />
            {state.sections.map(section => (
              <section key={section.id} className="section-card" data-testid={`section-${section.id}`}>
                <div className="section-header">
                  <span className="section-title">{section.title}</span>
                  <span className="muted">{section.kind}</span>
                </div>
                {section.kind === 'toc' ? (
                  <div className="block-body"><TocPreview state={state} /></div>
                ) : null}
                {section.blocks.length === 0 && section.kind !== 'toc' ? (
                  <div className="block-body"><EmptyState title="这个 section 还没有内容。使用下方插入菜单添加结构块。" /></div>
                ) : null}
                {section.blocks.map(block => (
                  <BlockEditor
                    key={block.id}
                    block={block}
                    active={state.selectedBlockId === block.id}
                    dispatch={dispatch}
                    bibliographyKeys={state.bibliography.map(entry => entry.key)}
                    referenceTargets={referenceTargets}
                  />
                ))}
                {section.kind !== 'toc' && section.kind !== 'cover' && section.kind !== 'originalityStatement' ? <InsertBlockMenu sectionId={section.id} dispatch={dispatch} /> : null}
              </section>
            ))}
          </div>
        </div>
        <div className="side-panel">
          <div className="stack">
            <Panel title="模板状态">
              <select
                className="ui-select"
                aria-label="选择模板"
                value={state.templateId}
                onChange={event => {
                  const template = templates.find(item => item.id === event.target.value);
                  dispatch({ type: 'setTemplate', templateId: event.target.value, template });
                }}
              >
                {templates.map(template => <option key={template.id} value={template.id}>{template.name}</option>)}
              </select>
              <div style={{ height: 12 }} />
              <TemplateStatusPanel template={state.template} />
            </Panel>
            <Panel title="校验问题">
              <ValidationPanel issues={state.validationIssues} onJump={jumpToBlock} />
            </Panel>
            <Panel title="引用与参考文献">
              <BibliographyManager entries={state.bibliography} citedKeys={citedKeys} dispatch={dispatch} />
            </Panel>
            <Panel title="生成结果">
              <RenderPanel run={state.renderRun} onRender={() => void render()} docxRenderEnabled={appConfig.docxRenderEnabled} />
            </Panel>
          </div>
        </div>
      </main>
    </div>
  );
}

function safeDownloadName(value: string) {
  return value.trim().replace(/[^\p{L}\p{N}._-]+/gu, '-').replace(/^-|-$/g, '') || 'thesis-document';
}

function documentNode() {
  return window.document;
}

function readFileText(file: File) {
  if (typeof file.text === 'function') {
    return file.text();
  }

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
