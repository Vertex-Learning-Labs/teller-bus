namespace MockHost.Domain;

public readonly record struct AccountNumber
{
    private AccountNumber(string value) => Value = value;

    public string Value { get; }

    public static bool TryCreate(string? value, out AccountNumber number)
    {
        if (value is not null && value.Length is >= 6 and <= 20 && value.All(char.IsDigit))
        {
            number = new AccountNumber(value);
            return true;
        }

        number = default;
        return false;
    }

    public override string ToString() => Value;
}
