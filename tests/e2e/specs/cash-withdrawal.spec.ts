import { expect, test } from '@playwright/test';
import { accounts, AuditTrail, balanceOf, newKey, settled, withdraw, Withdrawal } from '../support/teller';

test.describe('cash withdrawal', () => {
  test('is accepted, posted by the host, and debits the balance', async ({ request }) => {
    const before = await balanceOf(request, accounts.open);

    const response = await withdraw(request, { accountNumber: accounts.open, amount: 12.5 });

    expect(response.status()).toBe(202);
    const accepted = (await response.json()) as Withdrawal;
    expect(accepted).toMatchObject({ status: 'Accepted', duplicate: false, amount: 12.5 });
    expect(response.headers()['location']).toBe(`/cash-withdrawals/${accepted.transactionId}`);

    const final = await settled(request, accepted.transactionId);
    expect(final).toMatchObject({ status: 'Posted', reason: null });
    expect(await balanceOf(request, accounts.open)).toBeCloseTo(before - 12.5, 2);

    const audit = (await (await request.get(`/cash-withdrawals/${accepted.transactionId}/audit`)).json()) as AuditTrail;
    expect(audit.entries.map(entry => entry.event)).toEqual(['Accepted', 'Published', 'HostAnswered', 'Posted']);
  });

  test('retrying with the same key debits only once', async ({ request }) => {
    const before = await balanceOf(request, accounts.open);
    const key = newKey();

    const first = await withdraw(request, { accountNumber: accounts.open, amount: 5 }, key);
    const retry = await withdraw(request, { accountNumber: accounts.open, amount: 5 }, key);

    expect(first.status()).toBe(202);
    expect(retry.status()).toBe(202);
    expect(((await retry.json()) as Withdrawal).duplicate).toBe(true);
    expect((await settled(request, key)).status).toBe('Posted');
    expect(await balanceOf(request, accounts.open)).toBeCloseTo(before - 5, 2);
  });

  test('reusing a key for a different amount is rejected and changes nothing', async ({ request }) => {
    const key = newKey();
    await withdraw(request, { accountNumber: accounts.open, amount: 1 }, key);

    const reused = await withdraw(request, { accountNumber: accounts.open, amount: 999 }, key);

    expect(reused.status()).toBe(422);
    const original = await settled(request, key);
    expect(original).toMatchObject({ amount: 1, status: 'Posted' });
  });

  test('is declined when funds are insufficient', async ({ request }) => {
    const before = await balanceOf(request, accounts.lowBalance);

    const accepted = (await (await withdraw(request, { accountNumber: accounts.lowBalance, amount: before + 1_000 })).json()) as Withdrawal;

    expect((await settled(request, accepted.transactionId))).toMatchObject({ status: 'Declined', reason: 'InsufficientFunds' });
    expect(await balanceOf(request, accounts.lowBalance)).toBeCloseTo(before, 2);
  });

  test('is declined for a closed account', async ({ request }) => {
    const accepted = (await (await withdraw(request, { accountNumber: accounts.closed, amount: 10 })).json()) as Withdrawal;

    expect((await settled(request, accepted.transactionId))).toMatchObject({ status: 'Declined', reason: 'AccountClosed' });
  });

  test('is declined for an account the host does not know', async ({ request }) => {
    const accepted = (await (await withdraw(request, { accountNumber: accounts.unknown, amount: 10 })).json()) as Withdrawal;

    expect((await settled(request, accepted.transactionId))).toMatchObject({ status: 'Declined', reason: 'AccountNotFound' });
  });

  test('is declined for a currency the account is not held in', async ({ request }) => {
    const accepted = (await (await withdraw(request, { accountNumber: accounts.open, amount: 10, currency: 'EUR' })).json()) as Withdrawal;

    expect((await settled(request, accepted.transactionId))).toMatchObject({ status: 'Declined', reason: 'CurrencyMismatch' });
  });

  for (const [name, body] of [
    ['a zero amount', { accountNumber: accounts.open, amount: 0 }],
    ['a negative amount', { accountNumber: accounts.open, amount: -5 }],
    ['a bad currency code', { accountNumber: accounts.open, amount: 5, currency: 'US' }],
    ['a malformed account', { accountNumber: 'abc', amount: 5 }],
  ] as const) {
    test(`rejects ${name}`, async ({ request }) => {
      expect((await withdraw(request, body)).status()).toBe(400);
    });
  }

  test('returns 404 for a transaction that was never submitted', async ({ request }) => {
    expect((await request.get(`/cash-withdrawals/${newKey()}`)).status()).toBe(404);
  });
});
