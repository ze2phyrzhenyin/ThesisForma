import { useEffect, useState } from 'react';
import { ThesisEditorPage } from '../components/thesis-editor/ThesisEditorPage';
import { createInitialState, deserializeFromThesisDocument } from '../components/thesis-editor/serialization';
import type { RenderRun, ThesisEditorState } from '../components/thesis-editor/types';
import { HomePage } from './HomePage';
import { RunPage } from './RunPage';
import { TemplatesPage } from './TemplatesPage';

type Route = 'home' | 'templates' | 'editor' | 'run';

export function App() {
  const [route, setRoute] = useState<Route>(routeFromPath());
  const [editorState, setEditorState] = useState<ThesisEditorState>(createInitialState());
  const [lastRun, setLastRun] = useState<RenderRun | undefined>();

  useEffect(() => {
    const onPopState = () => setRoute(routeFromPath());
    window.addEventListener('popstate', onPopState);
    return () => window.removeEventListener('popstate', onPopState);
  }, []);

  function navigate(next: Route) {
    const path = next === 'home' ? '/' : next === 'templates' ? '/templates' : next === 'run' ? '/runs/latest' : '/editor/draft';
    window.history.pushState({}, '', path);
    setRoute(next);
  }

  async function handleImportJson(file: File) {
    try {
      const text = await file.text();
      const document = JSON.parse(text) as unknown;
      const state = deserializeFromThesisDocument(document, editorState.templateId);
      setEditorState(state);
      navigate('editor');
    } catch {
      // Silently ignore parse errors — user will see the editor with default state
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
      // Ignore malformed draft data
    }
  }

  if (route === 'templates') {
    return <TemplatesPage onSelect={(templateId) => {
      setEditorState(createInitialState(templateId));
      navigate('editor');
    }} onBack={() => navigate('home')} />;
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
    return <RunPage run={lastRun} onBack={() => navigate('editor')} />;
  }

  return (
    <HomePage
      onNew={() => navigate('editor')}
      onTemplates={() => navigate('templates')}
      onImportJson={handleImportJson}
      onOpenDraft={handleOpenDraft}
    />
  );
}

function routeFromPath(): Route {
  if (window.location.pathname.startsWith('/templates')) return 'templates';
  if (window.location.pathname.startsWith('/editor')) return 'editor';
  if (window.location.pathname.startsWith('/runs')) return 'run';
  return 'home';
}
