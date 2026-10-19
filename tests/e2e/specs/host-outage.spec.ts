import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { expect, test } from '@playwright/test';
import { urls } from '../playwright.config';
import { accounts, settled, withdraw, Withdrawal } from '../support/teller';

// Drives docker compose directly, so it needs the stack started from this repository's compose file.
const repoRoot = path.resolve(__dirname, '../../..');
const compose = (...args: string[]) => execFileSync('docker', ['compose', ...args], { cwd: repoRoot, stdio: 'pipe' });

test.describe('host outage @chaos', () => {
  test.describe.configure({ mode: 'serial' });

  test.afterAll(async ({ request }) => {
    compose('start', 'mock-host');
    await expect.poll(async () => (await request.get(`${urls.mockHost}/health`).catch(() => null))?.ok() ?? false, {
      timeout: 30_000,
    }).toBe(true);
  });

  test('a withdrawal the host never answers ends in NeedsReview', async ({ request }) => {
    compose('stop', 'mock-host');

    const response = await withdraw(request, { accountNumber: accounts.open, amount: 3 });
    expect(response.status()).toBe(202);
    const accepted = (await response.json()) as Withdrawal;

    // HTTP retries, then bus retries at 1 s and 5 s, then MassTransit publishes Fault<CashWithdrawalRequested>.
    const final = await settled(request, accepted.transactionId, 45_000);
    expect(final).toMatchObject({ status: 'NeedsReview', reason: 'HostOutcomeUnknown' });
  });

  test('withdrawals post again once the host is back', async ({ request }) => {
    compose('start', 'mock-host');
    await expect.poll(async () => (await request.get(`${urls.mockHost}/health`).catch(() => null))?.ok() ?? false, {
      timeout: 30_000,
    }).toBe(true);

    const accepted = (await (await withdraw(request, { accountNumber: accounts.open, amount: 3 })).json()) as Withdrawal;

    // The worker's circuit breaker may still be open for a few seconds after the outage.
    expect((await settled(request, accepted.transactionId, 45_000)).status).toBe('Posted');
  });
});
