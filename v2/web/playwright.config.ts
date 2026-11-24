import { defineConfig, devices } from '@playwright/test'

const port = Number(process.env.E2E_PORT ?? 4173)
const apiBase = process.env.NUXT_API_BASE ?? 'http://127.0.0.1:5080'

// 需要先啟動 API（v2/README）與 PostgreSQL；`npm run test:e2e` 會先 nuxt build。
export default defineConfig({
  testDir: 'tests/e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: [['list']],
  globalSetup: './tests/e2e/global-setup.ts',
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 900 } } },
    { name: 'mobile', use: { ...devices['Pixel 7'], viewport: { width: 360, height: 740 } } },
  ],
  webServer: {
    command: 'node .output/server/index.mjs',
    url: `http://127.0.0.1:${port}`,
    // Reusing whatever already listens on the port can test a foreign server; opt in with E2E_REUSE=1.
    reuseExistingServer: process.env.E2E_REUSE === '1',
    env: {
      PORT: String(port),
      HOST: '127.0.0.1',
      NUXT_API_BASE: apiBase,
    },
  },
})
