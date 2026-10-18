using System.Net;
using System.Text;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Teller.Worker;

namespace Teller.Worker.Tests;

public class CoreHostClientTests
{
    [Fact]
    public async Task Withdraw_returns_a_decline_without_throwing()
    {
        var client = Client("""
            {"transactionId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","accountNumber":"1001001002","outcome":"Declined","reason":"InsufficientFunds","availableBalance":40,"currency":"USD","duplicate":false}
            """, HttpStatusCode.OK);

        var receipt = await client.WithdrawAsync("1001001002", Guid.NewGuid(), 100m, "USD", CancellationToken.None);

        Assert.Equal("Declined", receipt.Outcome);
        Assert.Equal("InsufficientFunds", receipt.Reason);
    }

    [Fact]
    public async Task Withdraw_throws_when_the_host_is_unavailable()
    {
        var client = Client("{}", HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<CoreHostUnavailableException>(() =>
            client.WithdrawAsync("1001001001", Guid.NewGuid(), 10m, "USD", CancellationToken.None));
    }

    [Fact]
    public async Task Balance_returns_not_found_for_an_unknown_account()
    {
        var client = Client("{}", HttpStatusCode.NotFound);

        var balance = await client.GetBalanceAsync("1001001999", CancellationToken.None);

        Assert.False(balance.Found);
    }

    [Fact]
    public async Task Withdraw_turns_a_resilience_timeout_into_a_retryable_failure()
    {
        var client = Client(new TimeoutRejectedException());

        var failure = await Assert.ThrowsAsync<CoreHostUnavailableException>(() =>
            client.WithdrawAsync("1001001001", Guid.NewGuid(), 10m, "USD", CancellationToken.None));

        Assert.Equal(504, failure.StatusCode);
    }

    [Fact]
    public async Task Withdraw_turns_an_http_timeout_into_a_retryable_failure()
    {
        var client = Client(new TaskCanceledException("The request timed out."));

        await Assert.ThrowsAsync<CoreHostUnavailableException>(() =>
            client.WithdrawAsync("1001001001", Guid.NewGuid(), 10m, "USD", CancellationToken.None));
    }

    [Fact]
    public async Task Balance_turns_an_open_circuit_into_a_retryable_failure()
    {
        var client = Client(new BrokenCircuitException());

        var failure = await Assert.ThrowsAsync<CoreHostUnavailableException>(() =>
            client.GetBalanceAsync("1001001001", CancellationToken.None));

        Assert.Equal(503, failure.StatusCode);
    }

    [Fact]
    public async Task Withdraw_lets_caller_cancellation_through()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = Client(new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WithdrawAsync("1001001001", Guid.NewGuid(), 10m, "USD", cancellation.Token));
    }

    private static CoreHostClient Client(string json, HttpStatusCode status) => Client(new StubHandler(json, status));

    private static CoreHostClient Client(Exception failure) => Client(new ThrowingHandler(failure));

    private static CoreHostClient Client(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        return new CoreHostClient(http);
    }

    private sealed class StubHandler(string json, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ThrowingHandler(Exception failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(failure);
    }
}
