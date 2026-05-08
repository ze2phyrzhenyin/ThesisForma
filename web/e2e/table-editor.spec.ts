import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import type { TableBlock } from '../src/types';

test('table merge and split flow exports clean ThesisDocument JSON', async ({ page }) => {
  page.on('dialog', (dialog) => dialog.accept());

  await page.goto('/');
  await page.evaluate(() => localStorage.clear());
  await page.getByRole('button', { name: '新建论文' }).click();
  await expect(page).toHaveURL(/\/d\/local-doc-/);

  const main = page.getByRole('main');
  const largeAdd = main.getByRole('button', { name: '＋ 添加内容…' });
  if ((await largeAdd.count()) > 0) {
    await largeAdd.click();
  } else {
    await main.getByRole('button', { name: '＋' }).first().click();
  }
  await page.getByRole('menuitem', { name: /表 行列网格/ }).click();

  const table = page.locator('table').first();
  await expect(table).toBeVisible();
  await table.locator('td textarea').nth(0).click();
  await table.locator('td textarea').nth(1).click();
  await expect(page.getByText(/已选 1 × 2/)).toBeVisible();

  await page.getByRole('button', { name: '合并所选' }).click();
  await expect(page.getByText('×2')).toBeVisible();

  await table.locator('td textarea').first().fill('merged header');
  await page.getByRole('button', { name: '拆分单元格' }).click();
  await expect(page.getByText('×2')).toHaveCount(0);

  await page.getByRole('button', { name: '导出 ▾' }).click();
  const download = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('menuitem', { name: /JSON/ }).click()
  ]).then(([item]) => item);
  const path = await download.path();
  expect(path).toBeTruthy();

  const exported = JSON.parse(await readFile(path!, 'utf8')) as {
    sections: Array<{ blocks: unknown[] }>;
  };
  const exportedTable = exported.sections.flatMap((section) => section.blocks).find((block) => {
    return typeof block === 'object' && block !== null && (block as { type?: string }).type === 'table';
  }) as TableBlock | undefined;

  expect(exportedTable).toBeTruthy();
  expect('selection' in exportedTable!).toBe(false);
  expect(exportedTable!.rows[0].cells).toHaveLength(3);
  expect(exportedTable!.rows[0].cells.some((cell) => cell.gridSpan || cell.verticalMerge)).toBe(false);
  expect(exportedTable!.rows[0].cells[0].text).toBe('merged header');
});
