import { test, expect } from '@playwright/test';

test('editor hydrates from a local draft created on the home page', async ({ page }) => {
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());
  await page.getByRole('button', { name: '新建论文' }).click();
  await expect(page).toHaveURL(/\/d\/local-doc-/);

  // Topbar shows the title
  await expect(page.locator('input[aria-label="论文题目"]')).toHaveValue('未命名论文');

  // Section nav has "正文" and the heading shows in outline
  await expect(page.getByRole('button', { name: /正文/ })).toBeVisible();
  await expect(page.getByText('绪论').first()).toBeVisible();

  // Switch to metadata view
  await page.getByRole('button', { name: '元数据' }).first().click();
  await expect(page.getByRole('heading', { name: '论文元数据' })).toBeVisible();

  await page.getByRole('button', { name: '格式覆盖' }).click();
  await expect(page.getByRole('heading', { name: '格式覆盖' })).toBeVisible();
  await page.getByRole('button', { name: '刷新生效证据' }).click();
  await expect(page.getByText(/Static local preview unavailable|Resolved ThesisFormatSpec/)).toBeVisible();
});

test('lazy right rail drawers render after editor load', async ({ page }) => {
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());
  await page.getByRole('button', { name: '新建论文' }).click();
  await expect(page).toHaveURL(/\/d\/local-doc-/);

  await page.locator('button[aria-label="参考文献"]').click();
  await expect(page.getByRole('heading', { name: '参考文献库' })).toBeVisible();

  await page.locator('button[aria-label="脚注尾注"]').click();
  await expect(page.getByRole('heading', { name: '脚注 / 尾注' })).toBeVisible();

  await page.locator('button[aria-label="校验问题"]').click();
  await expect(page.getByRole('heading', { name: '校验问题' })).toBeVisible();
});

test('mobile editor and template routes render nonblank lazy chunks', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());
  await page.getByRole('button', { name: '新建论文' }).click();
  await expect(page).toHaveURL(/\/d\/local-doc-/);
  await expect(page.locator('input[aria-label="论文题目"]')).toBeVisible();
  await expect(page.getByRole('main')).toBeVisible();

  const editorOverflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  expect(editorOverflow).toBeLessThanOrEqual(2);

  await page.goto('/templates/editor');
  await expect(page.getByRole('heading', { name: '模板包编辑器' })).toBeVisible();
  await expect(page.getByText('Page Templates')).toBeVisible();

  const templateOverflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  expect(templateOverflow).toBeLessThanOrEqual(2);
});

test('template editor opens and validates a blank package', async ({ page }) => {
  await page.goto('/templates/editor');
  await expect(page.getByRole('heading', { name: '模板包编辑器' })).toBeVisible();
  await expect(page.locator('input[value="local-template"]')).toBeVisible();
  await expect(page.getByRole('heading', { name: '模板校验' })).toBeVisible();
});
