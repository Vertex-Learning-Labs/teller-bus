using System.Net;
using System.Net.Http.Json;
using MockHost.Endpoints;

namespace MockHost.Tests;

public class HostApiTests : IClassFixture<MockHostFactory>
{
    private readonly HttpClient _client;

    public HostApiTests(MockHostFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Balance_returns_the_seeded_account()
    {
        var balance = await _client.GetFromJsonAsync<BalanceBody>("/accounts/1001001001/balance");

        Assert.NotNull(balance);
        Assert.Equal("1001001001", balance.AccountNumber);
        Assert.Equal("USD", balance.Currency);
        Assert.Equal("Open", balance.Status);
        Assert.Equal(2500m, balance.AvailableBalance);
    }

    [Fact]
    public async Task Balance_returns_not_found_for_an_unknown_account()
    {
        var response = await _client.GetAsync("/accounts/1001001999/balance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Withdrawal_declines_a_closed_account_without_a_server_error()
    {
        var response = await _client.PostAsJsonAsync("/accounts/1001001003/withdrawals", new
        {
            transactionId = Guid.NewGuid(),
            amount = 10m,
            currency = "USD"
        });

        var receipt = await response.Content.ReadFromJsonAsync<ReceiptBody>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(receipt);
        Assert.Equal("Declined", receipt.Outcome);
        Assert.Equal("AccountClosed", receipt.Reason);
        Assert.Equal(800m, receipt.AvailableBalance);
    }

    [Fact]
    public async Task Withdrawal_returns_503_and_does_not_debit_when_the_host_is_unavailable()
    {
        var before = await _client.GetFromJsonAsync<BalanceBody>("/accounts/1001001002/balance");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/accounts/1001001002/withdrawals")
        {
            Content = JsonContent.Create(new
            {
                transactionId = Guid.NewGuid(),
                amount = 5m,
                currency = "USD"
            })
        };
        request.Headers.Add(AccountEndpoints.FaultHeader, "unavailable");

        var response = await _client.SendAsync(request);
        var after = await _client.GetFromJsonAsync<BalanceBody>("/accounts/1001001002/balance");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(before!.AvailableBalance, after!.AvailableBalance);
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
