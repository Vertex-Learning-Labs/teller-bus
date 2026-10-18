import { expect, test } from '@playwright/test';
import { accounts, inquire, Inquiry, newKey } from '../support/teller';

test.describe('balance inquiry', () => {
  test('answers with the host balance for an open account', async ({ request }) => {
    const response = await inquire(request, accounts.open);

    expect(response.status()).toBe(200);
    const inquiry = (await response.json()) as Inquiry;
    expect(inquiry).toMatchObject({ accountNumber: accounts.open, status: 'Answered', currency: 'USD', duplicate: false });
    expect(inquiry.availableBalance).toBeGreaterThanOrEqual(0);
  });

  test('reports an account the host does not know', async ({ request }) => {
    const inquiry = (await (await inquire(request, accounts.unknown)).json()) as Inquiry;

    expect(inquiry).toMatchObject({ status: 'UnknownAccount', availableBalance: null, currency: null });
  });

  test('replaying the same key returns the cached answer', async ({ request }) => {
    const key = newKey();

    const first = (await (await inquire(request, accounts.open, key)).json()) as Inquiry;
    const second = (await (await inquire(request, accounts.open, key)).json()) as Inquiry;

    expect(first.duplicate).toBe(false);
    expect(second).toMatchObject({ inquiryId: key, duplicate: true, availableBalance: first.availableBalance });
  });

  test('reusing a key for a different account is rejected', async ({ request }) => {
    const key = newKey();
    await inquire(request, accounts.open, key);

    const reused = await inquire(request, accounts.lowBalance, key);

    expect(reused.status()).toBe(422);
  });

  test('rejects a missing idempotency key', async ({ request }) => {
    const response = await request.post('/balance-inquiries', {
      data: { accountNumber: accounts.open, tellerId: 'T-e2e', branchId: 'BR-e2e' },
    });

    expect(response.status()).toBe(400);
  });

  test('rejects a malformed account number', async ({ request }) => {
    const response = await inquire(request, '12ab');

    expect(response.status()).toBe(400);
  });
});
