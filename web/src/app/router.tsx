import { createBrowserRouter, Navigate } from 'react-router-dom';
import { HomePage } from '@/pages/HomePage';
import { TemplatesPage } from '@/pages/TemplatesPage';
import { EditorPage } from '@/pages/EditorPage';
import { AppShell } from './AppShell';
import { ErrorPage } from './ErrorPage';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    errorElement: <ErrorPage />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'templates', element: <TemplatesPage /> },
      { path: 'd/:docId', element: <EditorPage /> },
      { path: '*', element: <Navigate to="/" replace /> }
    ]
  }
]);
