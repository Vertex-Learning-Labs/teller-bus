import { expect, test } from '@playwright/test';
import { urls } from '../playwright.config';

test.describe('stack health', () => {
  test('teller api is ready', async ({ request }) => {
    const response = await request.get('/health');
    expect(response.ok()).toBeTruthy();
    expect(await response.json()).toMatchObject({ service: 'teller-api', status: 'ready' });
  });

  test('mock host is ready', async ({ request }) => {
    const response = await request.get(`${urls.mockHost}/health`);
    expect(await response.json()).toMatchObject({ service: 'mock-host', status: 'ready' });
  });

  for (const [lane, url] of [['reads', urls.workerReads], ['posts', urls.workerPosts]]) {
    test(`${lane} worker is connected to the bus`, async ({ request }) => {
      const response = await request.get(`${url}/health/ready`);
      expect(response.ok()).toBeTruthy();
    });
  }
});
