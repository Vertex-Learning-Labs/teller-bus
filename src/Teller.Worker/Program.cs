using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Teller.Worker;
using Teller.Worker.Consumers;

var builder = WebApplication.CreateBuilder(args);

var lanes = builder.Configuration.GetSection(WorkerOptions.SectionName).Get<WorkerOptions>() ?? new WorkerOptions();
lanes.EnsureValid();

var rabbitHost = builder.Configuration["RabbitMq:Host"];
if (string.IsNullOrWhiteSpace(rabbitHost))
    throw new InvalidOperationException("Set RabbitMq:Host before starting the worker.");

builder.Services.AddOptions<MassTransitHostOptions>().Configure(options =>
{
    options.WaitUntilStarted = true;
    options.StartTimeout = TimeSpan.FromSeconds(30);
    options.StopTimeout = TimeSpan.FromSeconds(35);
    options.ConsumerStopTimeout = TimeSpan.FromSeconds(25);
});

builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"]);

var mockHost = builder.Configuration["MockHost:BaseUrl"] ?? "http://localhost:5081";
builder.Services.AddCoreHostClient(mockHost);

builder.Services.AddMassTransit(bus =>
{
    if (lanes.RunsReads)
        bus.AddConsumer<BalanceInquiryConsumer>();

    if (lanes.RunsPosts)
        bus.AddConsumer<CashWithdrawalConsumer>();

    bus.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", host =>
        {
            host.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            host.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        if (lanes.RunsReads)
        {
            cfg.ReceiveEndpoint("host-reads", endpoint =>
            {
                endpoint.PrefetchCount = Math.Max(lanes.ReadPrefetch, lanes.ReadConcurrency);
                endpoint.UseConcurrencyLimit(lanes.ReadConcurrency);
                endpoint.ConfigureConsumer<BalanceInquiryConsumer>(context);
            });
        }

        if (lanes.RunsPosts)
        {
            cfg.ReceiveEndpoint("host-posts", endpoint =>
            {
                endpoint.PrefetchCount = Math.Max(lanes.PostPrefetch, lanes.PostConcurrency);
                endpoint.UseConcurrencyLimit(lanes.PostConcurrency);
                endpoint.UseRateLimit(lanes.PostsPerSecond, TimeSpan.FromSeconds(1));
                endpoint.UseMessageRetry(retry =>
                {
                    retry.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
                    retry.Handle<CoreHostUnavailableException>();
                    retry.Handle<HttpRequestException>();
                });
                endpoint.ConfigureConsumer<CashWithdrawalConsumer>(context);
            });
        }
    });
});

var app = builder.Build();
app.Logger.LogInformation("Teller worker lanes: {Lanes}", lanes.Lanes);
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready");
app.Run();
