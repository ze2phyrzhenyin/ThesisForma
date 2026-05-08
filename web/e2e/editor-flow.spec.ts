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
});

test('template editor opens and validates a blank package', async ({ page }) => {
  await page.goto('/templates/editor');
  await expect(page.getByRole('heading', { name: '模板包编辑器' })).toBeVisible();
  await expect(page.locator('input[value="local-template"]')).toBeVisible();
  await expect(page.getByRole('heading', { name: '模板校验' })).toBeVisible();
});
