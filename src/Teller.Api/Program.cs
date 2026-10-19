using System.Text.Json.Serialization;
using MassTransit;
using Teller.Api.Adapters;
using Teller.Api.Consumers;
using Teller.Api.Endpoints;
using Teller.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InMemoryWithdrawalRepository>();
builder.Services.AddSingleton<IWithdrawalRepository>(sp => sp.GetRequiredService<InMemoryWithdrawalRepository>());
builder.Services.AddSingleton<IWithdrawalAuditLog, InMemoryWithdrawalAuditLog>();
builder.Services.AddSingleton<InMemoryBalanceInquiryRepository>();
builder.Services.AddSingleton<IBalanceInquiryRepository>(sp => sp.GetRequiredService<InMemoryBalanceInquiryRepository>());
builder.Services.AddSingleton<RecordWithdrawalOutcome>();
builder.Services.AddScoped<RequestCashWithdrawal>();
builder.Services.AddSingleton<GetCashWithdrawal>();
builder.Services.AddScoped<RequestBalanceInquiry>();

var rabbitHost = builder.Configuration["RabbitMq:Host"];
if (string.IsNullOrWhiteSpace(rabbitHost))
{
    builder.Services.AddSingleton<RecordingWithdrawalPublisher>();
    builder.Services.AddSingleton<IWithdrawalPublisher>(sp => sp.GetRequiredService<RecordingWithdrawalPublisher>());
    builder.Services.AddSingleton<IBalanceInquiryClient, NotConnectedBalanceInquiryClient>();
}
else
{
    builder.Services.AddOptions<MassTransitHostOptions>().Configure(options =>
    {
        options.WaitUntilStarted = true;
        options.StartTimeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddMassTransit(bus =>
    {
        bus.AddConsumer<WithdrawalStatusConsumer>();
        bus.AddConsumer<WithdrawalFaultConsumer>();
        bus.AddRequestClient<BalanceInquiryRequest>(RequestTimeout.After(s: 5));
        bus.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitHost, "/", host =>
            {
                host.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
                host.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
            });

            cfg.ReceiveEndpoint("teller-status", endpoint =>
            {
                endpoint.ConfigureConsumer<WithdrawalStatusConsumer>(context);
                endpoint.ConfigureConsumer<WithdrawalFaultConsumer>(context);
            });
        });
    });
    builder.Services.AddScoped<IWithdrawalPublisher, RabbitWithdrawalPublisher>();
    builder.Services.AddScoped<IBalanceInquiryClient, RabbitBalanceInquiryClient>();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("angular", policy =>
        policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();
app.UseCors("angular");
app.MapGet("/health", () => Results.Ok(new { service = "teller-api", status = "ready" }));
app.MapTellerEndpoints();
app.Run();

public partial class Program;
