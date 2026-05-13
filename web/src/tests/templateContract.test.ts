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

  it('rejects unsupported template schema versions instead of normalizing them away', () => {
    const result = parseTemplatePackageJson(
      JSON.stringify({
        templateSchemaVersion: '9.9.9',
        id: 'template-a',
        name: 'Template A',
        version: '1.0.0',
        locale: 'zh-CN',
        formatSpecRef: 'format-spec.json'
      })
    );

    expect(result.ok).toBe(false);
    expect(result.template?.templateSchemaVersion).toBe('9.9.9');
    expect(result.issues.some((issue) => issue.code === 'template.schemaVersion.unsupported')).toBe(true);
  });

  it('cleans empty optional arrays without removing required fields', () => {
    const template = cleanTemplatePackage(createBlankTemplatePackage());
    expect(template.templateSchemaVersion).toBe('1.0.0');
    expect(template.id).toBeTruthy();
  });

  it('parses and cleans schema-backed page template element types', () => {
    const result = parseTemplatePackageJson(
      JSON.stringify({
        templateSchemaVersion: '1.0.0',
        id: 'template-layout',
        name: 'Template Layout',
        version: '1.0.0',
        locale: 'zh-CN',
        formatSpecRef: 'format-spec.json',
        variables: [{ name: 'defenseDate', label: 'Defense Date', type: 'date', defaultValue: '2026-06-01' }],
        assets: [{ id: 'collegeLogo', type: 'image', path: 'assets/logo.png', contentType: 'image/png' }],
        pageTemplates: [
          {
            id: 'cover',
            targetSectionType: 'cover',
            insertPosition: 'replaceSectionContent',
            blocks: [
              { type: 'spacer', heightCm: 1.2 },
              { type: 'image', assetId: 'collegeLogo', widthCm: 2.2, heightCm: 2.2, alignment: 'center' },
              { type: 'text', value: '{{variables.defenseDate}}', alignment: 'center', fontOverride: { sizePt: 16 } },
              {
                type: 'fieldTable',
                columns: 2,
                borderMode: 'bottomLine',
                labelColumnWidthCm: 3,
                valueColumnWidthCm: 9,
                rows: [[{ type: 'metadataField', label: '论文题目', sourcePath: 'metadata.title', layout: 'tableRow' }]]
              },
              {
                type: 'declarationText',
                paragraphs: ['声明 {{variables.defenseDate}}'],
                signatureFields: [{ type: 'metadataField', label: '日期', variableName: 'defenseDate' }]
              },
              { type: 'rule', thicknessPt: 1, color: '333333', alignment: 'center', spacingAfterPt: 6 },
              { type: 'pageBreak' }
            ]
          }
        ]
      })
    );

    expect(result.ok).toBe(true);
    const cleaned = cleanTemplatePackage(result.template!);
    expect(cleaned.pageTemplates?.[0].blocks.map((block) => block.type)).toEqual([
      'spacer',
      'image',
      'text',
      'fieldTable',
      'declarationText',
      'rule',
      'pageBreak'
    ]);
    expect(cleaned.pageTemplates?.[0].blocks[2]).toMatchObject({
      type: 'text',
      fontOverride: { sizePt: 16 }
    });
  });

  it('reports missing variables referenced by page template elements', () => {
    const template = createBlankTemplatePackage();
    template.pageTemplates = [
      {
        id: 'cover',
        targetSectionType: 'cover',
        insertPosition: 'replaceSectionContent',
        blocks: [{ type: 'metadataField', label: '日期', variableName: 'missingDate' }]
      }
    ];

    expect(validateTemplatePackage(template).some((issue) => issue.code === 'template.pageTemplate.variable.missing')).toBe(true);
  });

  it('reports missing image assets and illegal layout numbers', () => {
    const template = createBlankTemplatePackage();
    template.pageTemplates = [
      {
        id: 'cover',
        targetSectionType: 'cover',
        insertPosition: 'replaceSectionContent',
        blocks: [
          { type: 'image', assetId: 'missingLogo', widthCm: -1 },
          { type: 'spacer', heightCm: 24 },
          { type: 'text', value: 'Title', fontOverride: { sizePt: 0 }, spacingBeforePt: -1 },
          { type: 'fieldTable', columns: 8, rows: [] },
          { type: 'rule', thicknessPt: 0, color: 'bad' }
        ]
      }
    ];

    const issues = validateTemplatePackage(template);
    expect(issues.some((issue) => issue.code === 'template.pageTemplate.image.asset.missing')).toBe(true);
    expect(issues.some((issue) => issue.code === 'template.pageTemplate.spacer.height.invalid')).toBe(true);
    expect(issues.some((issue) => issue.code === 'template.pageTemplate.fontSize.invalid')).toBe(true);
    expect(issues.some((issue) => issue.code === 'template.pageTemplate.fieldTable.columns.invalid')).toBe(true);
    expect(issues.some((issue) => issue.code === 'template.pageTemplate.rule.thickness.invalid')).toBe(true);
    expect(issues.some((issue) => issue.code === 'template.pageTemplate.rule.color.invalid')).toBe(true);
  });

  it('cleans UI-only fields without dropping valid page template structure', () => {
    const template = createBlankTemplatePackage();
    template.assets = [{ id: 'logo', type: 'image', path: 'assets/logo.png', contentType: 'image/png' }];
    template.pageTemplates = [
      {
        id: 'cover',
        targetSectionType: 'cover',
        insertPosition: 'replaceSectionContent',
        blocks: [
          {
            type: 'image',
            assetId: 'logo',
            widthCm: 2,
            alignment: 'center',
            uiExpanded: true
          } as never
        ]
      }
    ];

    const cleaned = cleanTemplatePackage(template);
    const block = cleaned.pageTemplates?.[0].blocks[0];
    expect(block).toMatchObject({ type: 'image', assetId: 'logo', widthCm: 2, alignment: 'center' });
    expect('uiExpanded' in block!).toBe(false);
  });
});
