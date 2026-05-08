import { test, expect } from '@playwright/test';

/**
 * Smoke test for the home page. Doesn't require the .NET API to be running:
 * the page loads, brand renders, and primary CTAs are visible.
 */
test('home page renders core CTAs', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: '把论文写进结构里' })).toBeVisible();
  await expect(page.getByRole('button', { name: /新建论文|创建中…/ })).toBeVisible();
  await expect(page.getByText('导入 JSON')).toBeVisible();
});

test('navigates to templates page', async ({ page }) => {
  await page.goto('/templates');
  await expect(page.getByRole('heading', { name: '模板库' })).toBeVisible();
});
