using Microsoft.Extensions.Options;
using MockHost.Application;
using MockHost.Domain;
using MockHost.Infrastructure;

namespace MockHost.Tests;

public class WithdrawalServiceTests
{
    private readonly HostLedger _ledger = Ledger();
    private WithdrawalService Withdrawals => new(_ledger);

    [Fact]
    public void Post_returns_the_original_receipt_when_the_transaction_id_is_repeated()
    {
        var id = Guid.NewGuid();

        var first = Withdrawals.Post(id, Number("1001001001"), MoneyOf(25m));
        var second = Withdrawals.Post(id, Number("1001001001"), MoneyOf(25m));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.False(first.Duplicate);
        Assert.True(second.Duplicate);
        Assert.Equal(WithdrawalOutcome.Posted, second.Outcome);
        Assert.Equal(2475m, second.AvailableBalance);
        Assert.Equal(first.AvailableBalance, second.AvailableBalance);
    }

    [Fact]
    public void Post_does_not_apply_a_replay_to_a_different_account()
    {
        var id = Guid.NewGuid();

        var first = Withdrawals.Post(id, Number("1001001001"), MoneyOf(10m));
        var replay = Withdrawals.Post(id, Number("1001001002"), MoneyOf(10m));
        var untouched = new BalanceInquiryService(_ledger).Get(Number("1001001002"));

        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.Equal("1001001001", replay.AccountNumber);
        Assert.True(replay.Duplicate);
        Assert.Equal(40m, untouched!.AvailableBalance);
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

    private static HostLedger Ledger() => new(Options.Create(new MockHostOptions
    {
        Accounts =
        [
            new AccountSeed { AccountNumber = "1001001001", AvailableBalance = 2500m, Currency = "USD", Status = "Open" },
            new AccountSeed { AccountNumber = "1001001002", AvailableBalance = 40m, Currency = "USD", Status = "Open" }
        ]
    }));
}
