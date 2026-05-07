import { useEffect, useState } from 'react';
import { ThesisEditorPage } from '../components/thesis-editor/ThesisEditorPage';
import {
  createInitialState,
  deserializeFromThesisDocument
} from '../components/thesis-editor/serialization';
import type { RenderRun, ThesisEditorState } from '../components/thesis-editor/types';
import { HomePage } from './HomePage';
import { RunPage } from './RunPage';
import { TemplatesPage } from './TemplatesPage';

type Route = 'home' | 'templates' | 'editor' | 'run';

export function App() {
  const [route, setRoute] = useState<Route>(routeFromPath());
  const [editorState, setEditorState] = useState<ThesisEditorState>(createInitialState());
  const [lastRun] = useState<RenderRun | undefined>();
  const [homeNotice, setHomeNotice] = useState<{ tone: 'success' | 'danger'; title: string; message: string }>();

  useEffect(() => {
    const onPopState = () => setRoute(routeFromPath());
    window.addEventListener('popstate', onPopState);
    return () => window.removeEventListener('popstate', onPopState);
  }, []);

  function navigate(next: Route) {
    const path =
      next === 'home'
        ? '/'
        : next === 'templates'
          ? '/templates'
          : next === 'run'
            ? '/runs/latest'
            : '/editor/draft';
    window.history.pushState({}, '', path);
    setRoute(next);
  }

  async function handleImportJson(file: File) {
    try {
      const text = await readFileText(file);
      const document = JSON.parse(text) as unknown;
      const state = deserializeFromThesisDocument(document, editorState.templateId);
      setEditorState(state);
      setHomeNotice(undefined);
      navigate('editor');
    } catch (error) {
      setHomeNotice({
        tone: 'danger',
        title: '导入 JSON 失败',
        message: error instanceof SyntaxError
          ? '文件不是有效 JSON，请检查文件内容后重新导入。'
          : '文件无法转换为 ThesisDocument 草稿，请确认导出来源。'
      });
    }
  }

  function handleOpenDraft(draftId: string) {
    const item = localStorage.getItem(`thesisforma.document.${draftId}`);
    if (!item) return;
    try {
      const envelope = JSON.parse(item) as { document: unknown; templateId?: string };
      const state = deserializeFromThesisDocument(envelope.document, envelope.templateId);
      setEditorState(state);
      navigate('editor');
    } catch {
      setHomeNotice({
        tone: 'danger',
        title: '打开草稿失败',
        message: '这个本地草稿数据已损坏，可以删除后重新导入 JSON。'
      });
    }
  }

  if (route === 'templates') {
    return (
      <TemplatesPage
        onSelect={templateId => {
          setEditorState(createInitialState(templateId));
          navigate('editor');
        }}
        onBack={() => navigate('home')}
      />
    );
  }

  if (route === 'editor') {
    return (
      <ThesisEditorPage
        initialState={editorState}
        onStateChange={setEditorState}
        onHome={() => navigate('home')}
        onTemplates={() => navigate('templates')}
        onBack={() => window.history.back()}
      />
    );
  }

  if (route === 'run') {
    return <RunPage run={lastRun} onBack={() => navigate('editor')} onHome={() => navigate('home')} />;
  }

  return (
    <HomePage
      onNew={() => navigate('editor')}
      onTemplates={() => navigate('templates')}
      onImportJson={handleImportJson}
      onOpenDraft={handleOpenDraft}
      notice={homeNotice}
    />
  );
}

function routeFromPath(): Route {
  if (window.location.pathname.startsWith('/templates')) return 'templates';
  if (window.location.pathname.startsWith('/editor')) return 'editor';
  if (window.location.pathname.startsWith('/runs')) return 'run';
  return 'home';
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
