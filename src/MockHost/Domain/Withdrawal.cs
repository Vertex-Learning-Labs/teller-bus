namespace MockHost.Domain;

public enum AccountStatus
{
    Open,
    Closed
}

public enum WithdrawalOutcome
{
    Posted,
    Declined
}

public enum DeclineReason
{
    InsufficientFunds,
    AccountClosed,
    CurrencyMismatch
}

public readonly record struct LedgerResult(WithdrawalOutcome Outcome, DeclineReason? Reason, Money Available)
{
    public static LedgerResult Posted(Money available) => new(WithdrawalOutcome.Posted, null, available);

    public static LedgerResult Declined(DeclineReason reason, Money available) =>
        new(WithdrawalOutcome.Declined, reason, available);
}
