using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MockHost.Domain;
using MockHost.Infrastructure;

namespace MockHost.Tests;

public class WithdrawalIdempotencyTests : IClassFixture<MockHostFactory>
{
    private readonly HttpClient _client;

    public WithdrawalIdempotencyTests(MockHostFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Repeating_a_transaction_id_returns_the_first_receipt_and_debits_once()
    {
        var transactionId = Guid.NewGuid();

        var first = await PostAsync(transactionId);
        var second = await PostAsync(transactionId);
        var balance = await _client.GetFromJsonAsync<BalanceBody>("/accounts/1001001001/balance");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.False(first.Receipt!.Duplicate);
        Assert.True(second.Receipt!.Duplicate);
        Assert.Equal(first.Receipt.AvailableBalance, second.Receipt.AvailableBalance);
        Assert.Equal(2400m, balance!.AvailableBalance);
    }

    [Fact]
    public void A_restarted_ledger_keeps_the_receipt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "teller-bus-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "ledger.json");
        var options = Options.Create(new MockHostOptions
        {
            LedgerPath = path,
            Accounts =
            [
                new AccountSeed { AccountNumber = "1001001001", AvailableBalance = 2500m, Currency = "USD", Status = "Open" }
            ]
        });
        var transactionId = Guid.NewGuid();

        try
        {
            var firstLedger = new HostLedger(options);
            var first = firstLedger.TryWithdraw(Number("1001001001"), transactionId, MoneyOf(80m));

            var restarted = new HostLedger(options);
            var replay = restarted.TryWithdraw(Number("1001001001"), transactionId, MoneyOf(80m));
            var balance = restarted.FindBalance(Number("1001001001"));

            Assert.NotNull(first);
            Assert.False(first.Duplicate);
            Assert.NotNull(replay);
            Assert.True(replay.Duplicate);
            Assert.Equal(2420m, balance!.AvailableBalance);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private async Task<(HttpStatusCode StatusCode, ReceiptBody? Receipt)> PostAsync(Guid transactionId)
    {
        var response = await _client.PostAsJsonAsync("/accounts/1001001001/withdrawals", new
        {
            transactionId,
            amount = 100m,
            currency = "USD"
        });
        var receipt = await response.Content.ReadFromJsonAsync<ReceiptBody>();
        return (response.StatusCode, receipt);
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

    private sealed record BalanceBody(string AccountNumber, decimal AvailableBalance, string Currency, string Status);

    private sealed record ReceiptBody(
        Guid TransactionId,
        string AccountNumber,
        string Outcome,
        string? Reason,
        decimal AvailableBalance,
        string Currency,
        bool Duplicate);
}
