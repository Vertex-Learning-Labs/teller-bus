using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Teller.Worker;

namespace Teller.Worker.Tests;

public class CoreHostRegistrationTests
{
    [Fact]
    public void Http_client_timeout_does_not_cap_the_resilience_pipeline()
    {
        var services = new ServiceCollection();
        services.AddCoreHostClient("http://localhost");
        using var provider = services.BuildServiceProvider();

        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(CoreHostClient));

        Assert.True(http.Timeout == Timeout.InfiniteTimeSpan || http.Timeout > CoreHostRegistration.TotalRequestTimeout);
    }

    [Fact]
    public async Task A_transient_host_failure_is_retried_by_the_pipeline()
    {
        var handler = new FlakyHandler();
        var services = new ServiceCollection();
        services.AddCoreHostClient("http://localhost").ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        var receipt = await provider.GetRequiredService<CoreHostClient>()
            .WithdrawAsync("1001001001", Guid.NewGuid(), 10m, "USD", CancellationToken.None);

        Assert.Equal("Posted", receipt.Outcome);
        Assert.Equal(2, handler.Calls);
    }

    private sealed class FlakyHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"transactionId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","accountNumber":"1001001001","outcome":"Posted","reason":null,"availableBalance":2490,"currency":"USD","duplicate":false}""",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
