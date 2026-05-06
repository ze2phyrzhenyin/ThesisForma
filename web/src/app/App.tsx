import { useState } from 'react';
import { ThesisEditorPage } from '../components/thesis-editor/ThesisEditorPage';
import { createInitialState } from '../components/thesis-editor/serialization';
import type { RenderRun, ThesisEditorState } from '../components/thesis-editor/types';
import { HomePage } from './HomePage';
import { RunPage } from './RunPage';
import { TemplatesPage } from './TemplatesPage';

type Route = 'home' | 'templates' | 'editor' | 'run';

export function App() {
  const [route, setRoute] = useState<Route>(routeFromPath());
  const [editorState, setEditorState] = useState<ThesisEditorState>(createInitialState());
  const [lastRun, setLastRun] = useState<RenderRun | undefined>();

  function navigate(next: Route) {
    const path = next === 'home' ? '/' : next === 'templates' ? '/templates' : next === 'run' ? '/runs/latest' : '/editor/draft';
    window.history.pushState({}, '', path);
    setRoute(next);
  }

  if (route === 'templates') {
    return <TemplatesPage onSelect={(templateId) => {
      setEditorState(createInitialState(templateId));
      navigate('editor');
    }} />;
  }

  if (route === 'editor') {
    return <ThesisEditorPage initialState={editorState} />;
  }

  if (route === 'run') {
    return <RunPage run={lastRun} onBack={() => navigate('editor')} />;
  }

  return <HomePage onNew={() => navigate('editor')} onTemplates={() => navigate('templates')} />;
}

function routeFromPath(): Route {
  if (window.location.pathname.startsWith('/templates')) return 'templates';
  if (window.location.pathname.startsWith('/editor')) return 'editor';
  if (window.location.pathname.startsWith('/runs')) return 'run';
  return 'home';
}
