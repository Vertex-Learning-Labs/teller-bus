import { defineConfig } from '@playwright/test';

export const urls = {
  api: process.env.TELLER_API_URL ?? 'http://localhost:5112',
  mockHost: process.env.MOCK_HOST_URL ?? 'http://localhost:5081',
  workerReads: process.env.WORKER_READS_URL ?? 'http://localhost:5082',
  workerPosts: process.env.WORKER_POSTS_URL ?? 'http://localhost:5083',
};

export default defineConfig({
  testDir: './specs',
  globalSetup: './global-setup.ts',
  // Tests share the seed accounts and assert on balances, so they run one at a time.
  workers: 1,
  fullyParallel: false,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: urls.api,
    extraHTTPHeaders: { Accept: 'application/json' },
  },
});
