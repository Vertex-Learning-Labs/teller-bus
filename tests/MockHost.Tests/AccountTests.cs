using MockHost.Domain;

namespace MockHost.Tests;

public class AccountTests
{
    [Fact]
    public void Withdraw_posts_and_reduces_available_balance()
    {
        var account = Account.Restore(Number("1001001001"), MoneyOf(2500m), AccountStatus.Open);

        var result = account.Withdraw(MoneyOf(100m));

        Assert.Equal(WithdrawalOutcome.Posted, result.Outcome);
        Assert.Null(result.Reason);
        Assert.Equal(2400m, result.Available.Amount);
        Assert.Equal(2400m, account.Available.Amount);
    }

    [Fact]
    public void Withdraw_declines_when_funds_are_insufficient_and_leaves_the_balance()
    {
        var account = Account.Restore(Number("1001001002"), MoneyOf(40m), AccountStatus.Open);

        var result = account.Withdraw(MoneyOf(40.01m));

        Assert.Equal(WithdrawalOutcome.Declined, result.Outcome);
        Assert.Equal(DeclineReason.InsufficientFunds, result.Reason);
        Assert.Equal(40m, account.Available.Amount);
    }

    [Fact]
    public void Withdraw_declines_a_closed_account()
    {
        var account = Account.Restore(Number("1001001003"), MoneyOf(800m), AccountStatus.Closed);

        var result = account.Withdraw(MoneyOf(10m));

        Assert.Equal(DeclineReason.AccountClosed, result.Reason);
        Assert.Equal(800m, account.Available.Amount);
    }

    [Fact]
    public void Withdraw_declines_when_the_currency_does_not_match()
    {
        var account = Account.Restore(Number("1001001001"), MoneyOf(2500m), AccountStatus.Open);
        Assert.True(Money.TryCreate(10m, "EUR", out var euros));

        var result = account.Withdraw(euros);

        Assert.Equal(DeclineReason.CurrencyMismatch, result.Reason);
        Assert.Equal(2500m, account.Available.Amount);
    }

    private static AccountNumber Number(string value)
    {
        Assert.True(AccountNumber.TryCreate(value, out var number));
        return number;
    }

    private static Money MoneyOf(decimal amount)
    {
        Assert.True(Money.TryCreate(amount, "USD", out var money));
        return money;
    }
}
