import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: 'e2e',
  timeout: 60000,
  reporter: [['list'], ['json', { outputFile: 'reports/e2e.json' }]],
  use: {
    baseURL: 'http://127.0.0.1:4173',
    viewport: { width: 390, height: 844 },
    deviceScaleFactor: 2,
    isMobile: true,
    hasTouch: true,
    launchOptions: process.env.PW_CHROMIUM ? { executablePath: process.env.PW_CHROMIUM } : {},
  },
  projects: [
    { name: 'iphone-14', use: { viewport: { width: 390, height: 844 } } },
    { name: 'small-android', use: { viewport: { width: 360, height: 740 } } },
  ],
  webServer: { command: 'npx vite preview --port 4173 --strictPort --host 127.0.0.1', url: 'http://127.0.0.1:4173', reuseExistingServer: true, timeout: 60000 },
});
