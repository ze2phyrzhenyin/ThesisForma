import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  fullyParallel: true,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry'
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: process.env.CI
    ? {
        command: 'npm run preview',
        port: 5173,
        timeout: 60_000,
        reuseExistingServer: false
      }
    : {
        command: 'npm run dev',
        port: 5173,
        timeout: 60_000,
        reuseExistingServer: true
      }
});
