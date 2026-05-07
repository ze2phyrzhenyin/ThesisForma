import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=',
  'base64'
);

test('E2E_UserCanCreateStructuredThesisAndExportJson', async ({ page }, testInfo) => {
  const consoleErrors: string[] = [];
  page.on('console', message => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });

  await page.goto('/');
  await expect(page.getByText('论文结构化编辑器')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('home-page.png'), fullPage: true });

  await page.getByRole('button', { name: '新建论文' }).click();
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

  await page.getByRole('tab', { name: /引用/ }).click();
  await page.getByRole('button', { name: '添加文献' }).click();
  await page.getByLabel(/参考文献文本 ref-1/).fill('王五. 结构化论文写作研究[J]. 示例学刊, 2026.');

  const body = page.getByTestId('section-body');
  await body.getByRole('button', { name: '插入正文段落' }).click();
  const paragraphBlock = page.getByTestId('block-paragraph').last();
  await paragraphBlock.getByLabel('正文段落').fill('本文通过结构化编辑器录入论文内容，格式规则由模板统一控制。');
  await paragraphBlock.getByLabel('插入引用').selectOption('ref-1');
  await paragraphBlock.getByLabel('交叉引用').selectOption('heading-1');

  await body.getByRole('button', { name: '插入表格' }).click();
  await page.getByLabel('表名').fill('样例数据表');
  await page.getByRole('button', { name: '创建表格' }).click();
  const tableBlock = page.getByTestId('block-table').last();
  await tableBlock.getByLabel(/单元格 .* 1/).first().fill('指标');
  await tableBlock.getByLabel(/单元格 .* 2/).first().fill('数值');
  await tableBlock.getByLabel('第一行为表头').check();
  await tableBlock.getByRole('button', { name: '添加行' }).click();
  await tableBlock.getByRole('button', { name: '添加列' }).click();

  await body.getByRole('button', { name: '插入图片' }).click();
  await page.getByLabel('图名').fill('样例图片');
  await page.getByLabel('替代文本').fill('结构化编辑器图片');
  await page.getByRole('button', { name: '插入图片块' }).click();
  await page.locator('input[type="file"]').last().setInputFiles({ name: 'figure.png', mimeType: 'image/png', buffer: png });
  await expect(page.getByAltText('结构化编辑器图片')).toBeVisible();

  await page.screenshot({ path: testInfo.outputPath('editor-with-blocks.png'), fullPage: true });

  await page.getByRole('button', { name: '校验' }).click();
  await expect(page.getByText(/校验通过|校验项/).first()).toBeVisible();

  await expect(page.getByRole('button', { name: '生成 DOCX' }).first()).toBeDisabled();
  await page.getByRole('tab', { name: /属性/ }).click();
  await expect(page.getByText(/生成 DOCX 需要后端服务/)).toBeVisible();

  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: '导出 JSON' }).click();
  const exportDialog = page.getByRole('dialog');
  await expect(exportDialog.getByText('导出 ThesisDocument JSON', { exact: true })).toBeVisible();
  await exportDialog.getByRole('button', { name: /导出 JSON|仍然导出草稿 JSON/ }).click();
  const download = await downloadPromise;
  const downloadPath = testInfo.outputPath('structured-editor-e2e.thesis-document.json');
  await download.saveAs(downloadPath);

  const parsed = JSON.parse(await readFile(downloadPath, 'utf8'));
  expect(parsed.metadata.title).toBe('结构化编辑器端到端论文');
  expect(JSON.stringify(parsed.sections)).toContain('"type":"table"');
  expect(JSON.stringify(parsed.sections)).toContain('"type":"figure"');
  expect(JSON.stringify(parsed.sections)).toContain('"type":"citation"');
  expect(JSON.stringify(parsed.sections)).toContain('"type":"reference"');
  expect(consoleErrors).toEqual([]);
});

test('E2E_FrontendOnlyModeDoesNotCallRenderApi', async ({ page }) => {
  let renderCalls = 0;
  await page.route('**/api/documents/**/render', route => {
    renderCalls += 1;
    return route.fulfill({ status: 500, body: 'render should not be called in frontend-only mode' });
  });

  await page.goto('/editor/draft');
  await expect(page.getByRole('button', { name: '生成 DOCX' }).first()).toBeDisabled();
  await expect(page.getByText(/当前部署仅支持结构化编辑与 JSON 导出/)).toBeVisible();
  expect(renderCalls).toBe(0);
});
