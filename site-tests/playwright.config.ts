import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: '.', timeout: 60000,
  reporter: [['list'], ['json', { outputFile: 'site-e2e.json' }]],
  use: { baseURL: 'http://127.0.0.1:4180', launchOptions: process.env.PW_CHROMIUM ? { executablePath: process.env.PW_CHROMIUM } : {} },
  projects: [
    { name: 'mobile', use: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 2 } },
    { name: 'desktop', use: { viewport: { width: 1440, height: 900 } } },
  ],
  webServer: { command: 'python3 -m http.server 4180 --bind 127.0.0.1 --directory ../site', url: 'http://127.0.0.1:4180', reuseExistingServer: true },
});
