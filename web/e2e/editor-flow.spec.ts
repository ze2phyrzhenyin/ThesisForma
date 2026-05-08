import { test, expect } from '@playwright/test';

/**
 * Walks a user through opening a stub document and confirming the section
 * navigation, metadata panel, and canvas all hydrate. Uses route mocking
 * so the test does not depend on a live .NET API.
 */
test('editor hydrates with stubbed document', async ({ page }) => {
  await page.route('**/api/documents/doc-test', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'doc-test',
        templateId: null,
        updatedAt: '2026-05-08T00:00:00Z',
        document: {
          schemaVersion: '1.1.0',
          metadata: {
            title: '示例论文',
            author: '张三',
            college: '信息学院',
            major: '软件工程',
            studentId: '20260001',
            advisor: '李四',
            date: '2026-05',
            language: 'zh-CN'
          },
          sections: [
            {
              id: 'body',
              kind: 'body',
              blocks: [
                { type: 'heading', id: 'h1', level: 1, inlines: [{ type: 'text', text: '绪论' }] },
                {
                  type: 'paragraph',
                  id: 'p1',
                  inlines: [{ type: 'text', text: '论文起步段落。' }]
                }
              ]
            }
          ]
        }
      })
    });
  });

  await page.goto('/d/doc-test');

  // Topbar shows the title
  await expect(page.locator('input[aria-label="论文题目"]')).toHaveValue('示例论文');

  // Section nav has "正文" and the heading shows in outline
  await expect(page.getByRole('button', { name: /正文/ })).toBeVisible();
  await expect(page.getByText('绪论').first()).toBeVisible();

  // Switch to metadata view
  await page.getByRole('button', { name: '元数据' }).first().click();
  await expect(page.getByRole('heading', { name: '论文元数据' })).toBeVisible();
});
