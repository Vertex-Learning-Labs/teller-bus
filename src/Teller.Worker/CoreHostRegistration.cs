namespace Teller.Worker;

public static class CoreHostRegistration
{
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(20);

    public static IHttpClientBuilder AddCoreHostClient(this IServiceCollection services, string baseUrl)
    {
        var builder = services.AddHttpClient<CoreHostClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            // HttpClient.Timeout wraps the whole resilience pipeline, so any value below TotalRequestTimeout
            // would cut retries short. The pipeline's own timeouts bound each call instead.
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.AttemptTimeout.Timeout = AttemptTimeout;
            options.TotalRequestTimeout.Timeout = TotalRequestTimeout;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        });

        return builder;
    }
}
