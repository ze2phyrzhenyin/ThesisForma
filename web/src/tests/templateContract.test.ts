import { describe, expect, it } from 'vitest';
import {
  cleanTemplatePackage,
  createBlankTemplatePackage,
  parseTemplatePackageJson,
  validateTemplatePackage
} from '@/templates/templateContract';

describe('TemplatePackage contract helpers', () => {
  it('imports a valid minimal template package', () => {
    const result = parseTemplatePackageJson(
      JSON.stringify({
        templateSchemaVersion: '1.0.0',
        id: 'template-a',
        name: 'Template A',
        version: '1.0.0',
        locale: 'zh-CN',
        formatSpecRef: 'format-spec.json',
        variables: [{ name: 'defenseDate', label: 'Defense Date', type: 'date' }]
      })
    );
    expect(result.ok).toBe(true);
    expect(result.template?.variables?.[0].name).toBe('defenseDate');
  });

  it('reports duplicate variable names', () => {
    const template = createBlankTemplatePackage();
    template.variables = [
      { name: 'title', label: 'Title', type: 'string' },
      { name: 'title', label: 'Title Copy', type: 'string' }
    ];
    expect(validateTemplatePackage(template).some((issue) => issue.code === 'template.variable.duplicate')).toBe(true);
  });

  it('reports invalid margins', () => {
    const template = createBlankTemplatePackage();
    template.formatSpec!.pageSetup!.topMarginCm = -1;
    expect(validateTemplatePackage(template).some((issue) => issue.code === 'template.formatSpec.margin.invalid')).toBe(true);
  });

  it('cleans empty optional arrays without removing required fields', () => {
    const template = cleanTemplatePackage(createBlankTemplatePackage());
    expect(template.templateSchemaVersion).toBe('1.0.0');
    expect(template.id).toBeTruthy();
  });
});
