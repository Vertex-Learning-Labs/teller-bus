using System.Text.Json.Serialization;
using MockHost.Application;
using MockHost.Endpoints;
using MockHost.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MockHostOptions>(builder.Configuration.GetSection(MockHostOptions.SectionName));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<IHostLedger, HostLedger>();
builder.Services.AddSingleton<BalanceInquiryService>();
builder.Services.AddSingleton<WithdrawalService>();
builder.Services.AddSingleton<IResponseDelay, ConfiguredResponseDelay>();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { service = "mock-host", status = "ready" }));
app.MapAccountEndpoints();
app.Run();

public partial class Program;
