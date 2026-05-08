import { Suspense, lazy } from 'react';
import type { ReactNode } from 'react';
import { createBrowserRouter, Navigate } from 'react-router-dom';
import { HomePage } from '@/pages/HomePage';
import { TemplatesPage } from '@/pages/TemplatesPage';
import { AppShell } from './AppShell';
import { ErrorPage } from './ErrorPage';

const EditorPage = lazy(() => import('@/pages/EditorPage').then((module) => ({ default: module.EditorPage })));
const TemplateEditorPage = lazy(() =>
  import('@/pages/TemplateEditorPage').then((module) => ({ default: module.TemplateEditorPage }))
);

function lazyPage(element: ReactNode) {
  return <Suspense fallback={<main style={{ padding: '24px' }}>正在加载…</main>}>{element}</Suspense>;
}

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    errorElement: <ErrorPage />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'templates', element: <TemplatesPage /> },
      { path: 'templates/editor', element: lazyPage(<TemplateEditorPage />) },
      { path: 'd/:docId', element: lazyPage(<EditorPage />) },
      { path: '*', element: <Navigate to="/" replace /> }
    ]
  }
]);
