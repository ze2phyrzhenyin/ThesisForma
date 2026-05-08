import { defineConfig, devices } from '@playwright/test';

const e2ePort = Number(process.env.PLAYWRIGHT_PORT ?? 53173);
const e2eHost = '127.0.0.1';
const e2eBaseUrl = `http://${e2eHost}:${e2ePort}`;

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  fullyParallel: true,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: e2eBaseUrl,
    trace: 'on-first-retry'
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: process.env.CI
    ? {
        command: `npm run preview -- --host ${e2eHost} --port ${e2ePort}`,
        port: e2ePort,
        timeout: 60_000,
        reuseExistingServer: false
      }
    : {
        command: `npm run dev -- --host ${e2eHost} --port ${e2ePort}`,
        port: e2ePort,
        timeout: 60_000,
        reuseExistingServer: false
      }
});
