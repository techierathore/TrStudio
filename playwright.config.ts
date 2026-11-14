import { defineConfig } from '@playwright/test';

export default defineConfig({
  outputDir: './tests/.artifacts/test-results',
  testDir: './tests/verify',
  timeout: 120000,
  fullyParallel: false,
  workers: 1,
  reporter: 'line',
  use: {
    headless: true,
    screenshot: 'only-on-failure',
    baseURL: process.env.BASE_URL || 'http://localhost:5099',
  },
});
