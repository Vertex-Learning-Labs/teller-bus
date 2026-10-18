import { randomUUID } from 'node:crypto';
import { APIRequestContext, expect } from '@playwright/test';

export const accounts = {
  open: '1001001001',
  lowBalance: '1001001002',
  closed: '1001001003',
  unknown: '1009999999',
};

export const teller = { tellerId: 'T-e2e', branchId: 'BR-e2e' };

export type Withdrawal = {
  transactionId: string;
  accountNumber: string;
  amount: number;
  currency: string;
  status: 'Accepted' | 'Processing' | 'Posted' | 'Declined' | 'NeedsReview';
  duplicate: boolean;
};

export type Inquiry = {
  inquiryId: string;
  accountNumber: string;
  status: 'Pending' | 'Answered' | 'UnknownAccount' | 'TimedOut';
  availableBalance: number | null;
  currency: string | null;
  duplicate: boolean;
};

const terminal = new Set(['Posted', 'Declined', 'NeedsReview']);

export function newKey() {
  return randomUUID();
}

export function inquire(request: APIRequestContext, accountNumber: string, key = newKey()) {
  return request.post('/balance-inquiries', {
    headers: { 'Idempotency-Key': key },
    data: { accountNumber, ...teller },
  });
}

export function withdraw(
  request: APIRequestContext,
  body: { accountNumber: string; amount: number; currency?: string },
  key = newKey(),
) {
  return request.post('/cash-withdrawals', {
    headers: { 'Idempotency-Key': key },
    data: { currency: 'USD', ...teller, ...body },
  });
}

export async function balanceOf(request: APIRequestContext, accountNumber: string) {
  const response = await inquire(request, accountNumber);
  expect(response.ok()).toBeTruthy();
  const inquiry = (await response.json()) as Inquiry;
  expect(inquiry.status).toBe('Answered');
  return inquiry.availableBalance!;
}

/** Polls GET /cash-withdrawals/{id} until the bus round trip reaches a terminal status. */
export async function settled(request: APIRequestContext, transactionId: string, timeout = 15_000) {
  let latest: Withdrawal | undefined;
  await expect
    .poll(
      async () => {
        const response = await request.get(`/cash-withdrawals/${transactionId}`);
        latest = (await response.json()) as Withdrawal;
        return terminal.has(latest.status);
      },
      { timeout, intervals: [200, 500, 1_000] },
    )
    .toBe(true);
  return latest!;
}
