using Microsoft.Extensions.Options;

namespace MockHost.Infrastructure;

public interface IResponseDelay
{
    Task DelayAsync(CancellationToken cancellationToken);
}

public sealed class ConfiguredResponseDelay(IOptions<MockHostOptions> options) : IResponseDelay
{
    public Task DelayAsync(CancellationToken cancellationToken) =>
        Task.Delay(options.Value.TimeoutAfterPostDelay, cancellationToken);
}
