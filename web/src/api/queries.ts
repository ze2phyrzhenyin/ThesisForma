import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from './client';
import type { CreateDocumentRequest, DocumentOverrides, ThesisDocument } from '@/types';

export interface SaveDocumentInput {
  id: string;
  document: ThesisDocument;
  templateId?: string | null;
  overrides?: DocumentOverrides | null;
}

export interface ValidateInput {
  id: string;
  templateId?: string | null;
  overrides?: DocumentOverrides | null;
}

export interface FormatPreviewInput {
  id: string;
  templateId?: string | null;
  overrides?: DocumentOverrides | null;
}

export interface RenderInput {
  id: string;
  templateId?: string | null;
  overrides?: DocumentOverrides | null;
}

export const queryKeys = {
  templates: ['templates'] as const,
  template: (id: string) => ['template', id] as const,
  document: (id: string) => ['document', id] as const,
  run: (id: string) => ['run', id] as const
};

export function useTemplates() {
  return useQuery({
    queryKey: queryKeys.templates,
    queryFn: api.listTemplates,
    staleTime: 60_000
  });
}

export function useTemplate(id: string | undefined) {
  return useQuery({
    queryKey: id ? queryKeys.template(id) : ['template', 'none'],
    queryFn: () => api.getTemplate(id as string),
    enabled: Boolean(id),
    staleTime: 60_000
  });
}

export function useDocument(id: string | undefined) {
  return useQuery({
    queryKey: id ? queryKeys.document(id) : ['document', 'none'],
    queryFn: () => api.getDocument(id as string),
    enabled: Boolean(id)
  });
}

export function useCreateDocument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateDocumentRequest) => api.createDocument(req),
    onSuccess: (env) => {
      qc.setQueryData(queryKeys.document(env.id), env);
    }
  });
}

export function useImportDocument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: { document: ThesisDocument; templateId?: string | null; overrides?: DocumentOverrides | null }) =>
      api.importDocumentJson(input.document, input.templateId ?? null, input.overrides ?? null),
    onSuccess: (env) => {
      qc.setQueryData(queryKeys.document(env.id), env);
    }
  });
}

export function useSaveDocument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: SaveDocumentInput) =>
      api.saveDocument(input.id, input.document, input.templateId ?? null, input.overrides ?? null),
    onSuccess: (env) => {
      qc.setQueryData(queryKeys.document(env.id), env);
    }
  });
}

export function useValidateDocument() {
  return useMutation({
    mutationFn: (input: ValidateInput) => api.validateDocument(input.id, input.templateId ?? null, input.overrides ?? null)
  });
}

export function useFormatPreview() {
  return useMutation({
    mutationFn: (input: FormatPreviewInput) =>
      api.previewDocumentFormat(input.id, input.templateId ?? null, input.overrides ?? null)
  });
}

export function useRenderDocument() {
  return useMutation({
    mutationFn: (input: RenderInput) => api.renderDocument(input.id, input.templateId ?? null, input.overrides ?? null)
  });
}
