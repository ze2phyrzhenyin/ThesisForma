import type { DocumentOverridesSchema } from '@/editor/overrides';
import type { ThesisDocumentSchema } from '@/types/document';
import type { TemplatePackageSchema, ThesisFormatSpecSchema } from '@/types/template';
import { describe, expect, it } from 'vitest';

describe('generated schema types', () => {
  it('are exposed through handwritten editor adapter modules', () => {
    const document: ThesisDocumentSchema | null = null;
    const template: TemplatePackageSchema | null = null;
    const format: ThesisFormatSpecSchema | null = null;
    const overrides: DocumentOverridesSchema | null = null;

    expect([document, template, format, overrides]).toEqual([null, null, null, null]);
  });
});
