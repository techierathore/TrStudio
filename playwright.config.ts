import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests/verify',
  timeout: 120000,
  fullyParallel: false,
  workers: 1,
  reporter: 'line',
  use: {
    headless: true,
    screenshot: 'only-on-failure',
    baseURL: 'http://localhost:5099',
  },
});
