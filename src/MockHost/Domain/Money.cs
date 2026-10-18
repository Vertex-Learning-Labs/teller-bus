namespace MockHost.Domain;

public readonly record struct Money
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static bool TryCreate(decimal amount, string? currency, out Money money)
    {
        if (amount < 0 || currency is not { Length: 3 } || currency.Any(c => !char.IsLetter(c)))
        {
            money = default;
            return false;
        }

        money = new Money(decimal.Round(amount, 2, MidpointRounding.AwayFromZero), currency.ToUpperInvariant());
        return true;
    }

    public Money Debit(decimal amount) => new(Amount - amount, Currency);
}
