namespace MockHost.Domain;

public sealed class Account
{
    private Account(AccountNumber number, Money available, AccountStatus status)
    {
        Number = number;
        Available = available;
        Status = status;
    }

    public AccountNumber Number { get; }

    public Money Available { get; private set; }

    public AccountStatus Status { get; }

    public static Account Restore(AccountNumber number, Money available, AccountStatus status) =>
        new(number, available, status);

    public LedgerResult Withdraw(Money amount)
    {
        if (Status == AccountStatus.Closed)
            return LedgerResult.Declined(DeclineReason.AccountClosed, Available);

        if (amount.Currency != Available.Currency)
            return LedgerResult.Declined(DeclineReason.CurrencyMismatch, Available);

        if (amount.Amount > Available.Amount)
            return LedgerResult.Declined(DeclineReason.InsufficientFunds, Available);

        Available = Available.Debit(amount.Amount);
        return LedgerResult.Posted(Available);
    }
}
