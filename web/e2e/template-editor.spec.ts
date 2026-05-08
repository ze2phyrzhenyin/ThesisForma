import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

test('template editor imports page templates, edits a variable binding, validates, and exports JSON', async ({ page }) => {
  await page.goto('/templates/editor');
  await expect(page.getByRole('heading', { name: '模板包编辑器' })).toBeVisible();

  const template = {
    templateSchemaVersion: '1.0.0',
    id: 'e2e-template',
    name: 'E2E Template',
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
          { type: 'image', assetId: 'collegeLogo', widthCm: 2.2, heightCm: 2.2, alignment: 'center' },
          { type: 'text', value: '{{metadata.title}}', alignment: 'center' }
        ]
      }
    ]
  };

  await page.locator('input[type="file"]').setInputFiles({
    name: 'template.json',
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(template))
  });
  await expect(page.getByText('已导入模板包。')).toBeVisible();

  await page.getByLabel('新增页面模板元素类型').selectOption('metadataField');
  await page.getByRole('button', { name: '＋ 新增元素' }).click();
  await page.getByRole('button', { name: /metadataField/ }).last().click();
  await page.getByLabel('variableName').last().fill('missingDate');
  await expect(page.getByText(/不存在的变量 missingDate/)).toBeVisible();

  await page.getByLabel('variableName').last().fill('defenseDate');
  await expect(page.getByText(/不存在的变量 missingDate/)).toHaveCount(0);

  const download = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('button', { name: '导出模板 JSON' }).click()
  ]).then(([item]) => item);
  const path = await download.path();
  expect(path).toBeTruthy();
  const exported = JSON.parse(await readFile(path!, 'utf8')) as typeof template;

  expect(exported.pageTemplates[0].blocks.map((block) => block.type)).toContain('metadataField');
  expect(exported.pageTemplates[0].blocks.at(-1)).toMatchObject({
    type: 'metadataField',
    variableName: 'defenseDate'
  });
  expect('uiExpanded' in exported.pageTemplates[0].blocks.at(-1)!).toBe(false);
});
