import { expect, test } from '@playwright/test';
import path from 'node:path';

const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=',
  'base64'
);

test('E2E_UserCanCreateThesisWithHeadingParagraphTableFigureBibliographyAndRenderDocx', async ({ page }, testInfo) => {
  let savedDocument: any;
  let validateCalled = false;
  let renderCalled = false;

  await page.route('**/api/templates', async route => {
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        templates: [{
          id: 'example-university-engineering',
          name: 'Example University Engineering Thesis',
          school: 'Example University',
          college: 'Example Engineering College',
          version: '1.0.0',
          status: 'ready',
          coverage: 0.875,
          readiness: 'ready',
          tags: ['example']
        }]
      })
    });
  });

  await page.route('**/api/documents', async route => {
    if (route.request().method() === 'POST') {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ id: 'doc-e2e', templateId: 'example-university-engineering', document: {}, updatedAt: '2026-05-06T00:00:00Z' })
      });
      return;
    }
    await route.continue();
  });

  await page.route('**/api/documents/doc-e2e', async route => {
    const payload = route.request().postDataJSON();
    savedDocument = payload.document;
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ id: 'doc-e2e', templateId: payload.templateId, document: payload.document, updatedAt: '2026-05-06T00:00:00Z' })
    });
  });

  await page.route('**/api/documents/doc-e2e/validate', async route => {
    validateCalled = true;
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ isValid: true, issues: [] }) });
  });

  await page.route('**/api/documents/doc-e2e/render', async route => {
    renderCalled = true;
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        runId: 'run-e2e',
        documentId: 'doc-e2e',
        templateId: 'example-university-engineering',
        status: 'valid',
        openXmlValid: true,
        formatValid: true,
        openXmlErrorCount: 0,
        formatErrorCount: 0,
        downloadUrl: '/api/runs/run-e2e/download',
        issues: []
      })
    });
  });

  await page.route('**/api/assets/images', async route => {
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        assetId: 'asset-e2e',
        fileName: 'asset-e2e.png',
        contentType: 'image/png',
        size: png.length,
        imagePath: '../assets/asset-e2e.png',
        previewUrl: '/api/assets/asset-e2e'
      })
    });
  });

  await page.route('**/api/assets/asset-e2e', async route => {
    await route.fulfill({ contentType: 'image/png', body: png });
  });

  await page.route('**/api/runs/run-e2e/download', async route => {
    await route.fulfill({
      contentType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      headers: { 'content-disposition': 'attachment; filename="structured-editor-e2e.docx"' },
      body: Buffer.concat([Buffer.from('PK\u0003\u0004DOCX-E2E-'), png])
    });
  });

  await page.goto('/editor/draft');
  await expect(page.getByTestId('three-column-layout')).toBeVisible();

  await page.getByLabel('论文题目').fill('结构化编辑器端到端论文');
  await page.getByLabel('作者').fill('示例作者');
  await page.getByLabel('学院').fill('Example Engineering College');
  await page.getByLabel('专业').fill('戏剧影视表演');
  await page.getByLabel('学号').fill('20260001');
  await page.getByLabel('指导教师').fill('示例导师');
  await page.getByLabel('日期').fill('2026-05-06');

  await page.getByLabel('标题文本').first().fill('第一章 绪论');
  await expect(page.getByTestId('toc-preview')).toContainText('第一章 绪论');

  await page.getByRole('button', { name: '添加文献' }).click();
  await page.getByLabel(/参考文献文本 ref-1/).fill('王五. 结构化论文写作研究[J]. 示例学刊, 2026.');

  const body = page.getByTestId('section-body');
  await body.getByRole('button', { name: /正文段落/ }).click();
  await page.getByLabel('正文段落').last().fill('本文通过结构化编辑器录入论文内容，格式规则由模板统一控制。');
  await page.getByLabel('插入引用').last().selectOption('ref-1');
  await page.getByLabel('交叉引用').last().selectOption('heading-1');

  await body.getByRole('button', { name: /标题/ }).click();
  await page.getByLabel('标题文本').last().fill('第二章 方法');

  await body.getByRole('button', { name: /表格/ }).click();
  await page.getByTestId('block-table').last().getByLabel('表名').fill('样例数据表');
  await page.getByTestId('block-table').last().getByLabel(/单元格 .* 1/).first().fill('指标');
  await page.getByTestId('block-table').last().getByLabel(/单元格 .* 2/).first().fill('数值');
  await page.getByLabel('第一行为表头').check();
  await page.getByRole('button', { name: '添加行' }).click();
  await page.getByRole('button', { name: '添加列' }).click();

  await body.getByRole('button', { name: /图片/ }).click();
  const chooser = page.locator('input[type="file"]').last();
  await chooser.setInputFiles({ name: 'figure.png', mimeType: 'image/png', buffer: png });
  await page.getByTestId('block-figure').last().getByLabel('图名').fill('样例图片');
  await page.getByTestId('block-figure').last().getByLabel('替代文本').fill('结构化编辑器图片');
  await expect(page.getByAltText('结构化编辑器图片')).toBeVisible();

  await page.screenshot({ path: testInfo.outputPath('structured-editor-filled.png'), fullPage: true });

  await page.getByRole('button', { name: '校验' }).click();
  await expect(page.getByText('校验通过').first()).toBeVisible();

  await page.getByRole('button', { name: '生成 DOCX' }).first().click();
  await expect(page.getByText('OpenXML：通过')).toBeVisible();
  await expect(page.getByText('格式验证：通过')).toBeVisible();

  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('link', { name: '下载 DOCX' }).click();
  const download = await downloadPromise;
  const downloadPath = testInfo.outputPath('structured-editor-e2e.docx');
  await download.saveAs(downloadPath);

  expect(download.suggestedFilename()).toBe('structured-editor-e2e.docx');
  expect(renderCalled).toBe(true);
  expect(validateCalled || renderCalled).toBe(true);
  expect(savedDocument.metadata.title).toBe('结构化编辑器端到端论文');

  const bodySection = savedDocument.sections.find((section: any) => section.kind === 'body');
  const bibliographySection = savedDocument.sections.find((section: any) => section.kind === 'bibliography');
  expect(JSON.stringify(bodySection.blocks)).toContain('"type":"heading"');
  expect(JSON.stringify(bodySection.blocks)).toContain('"type":"paragraph"');
  expect(JSON.stringify(bodySection.blocks)).toContain('"type":"table"');
  expect(JSON.stringify(bodySection.blocks)).toContain('"type":"figure"');
  expect(JSON.stringify(bodySection.blocks)).toContain('"type":"citation"');
  expect(JSON.stringify(bodySection.blocks)).toContain('"type":"reference"');
  expect(JSON.stringify(bibliographySection.blocks)).toContain('结构化论文写作研究');

  const downloaded = await import('node:fs/promises').then(fs => fs.readFile(downloadPath));
  expect(downloaded.length).toBeGreaterThan(20);
  expect(path.basename(downloadPath)).toBe('structured-editor-e2e.docx');
});
