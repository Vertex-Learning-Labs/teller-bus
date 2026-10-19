using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Teller.Api.Adapters;
using Teller.Api.Endpoints;
using Teller.Application;

namespace Teller.Api.Tests;

public class TellerApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TellerApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Cash_withdrawal_is_accepted_and_can_be_read_back()
    {
        var id = Guid.NewGuid();

        var created = await PostWithdrawalAsync(id, 80m);
        var fetched = await _client.GetFromJsonAsync<WithdrawalBody>($"/cash-withdrawals/{id}");

        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        Assert.Equal("Accepted", created.Body!.Status);
        Assert.False(created.Body.Duplicate);
        Assert.Equal(80m, fetched!.Amount);
        Assert.Equal("USD", fetched.Currency);
        Assert.Equal("Accepted", fetched.Status);
    }

    [Fact]
    public async Task Repeating_the_idempotency_key_does_not_publish_a_second_withdrawal()
    {
        var id = Guid.NewGuid();

        var first = await PostWithdrawalAsync(id, 25m);
        var second = await PostWithdrawalAsync(id, 25m);
        var published = _factory.Services.GetRequiredService<RecordingWithdrawalPublisher>().Messages
            .Count(message => message.TransactionId == id);

        Assert.False(first.Body!.Duplicate);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.True(second.Body!.Duplicate);
        Assert.Equal(25m, second.Body.Amount);
        Assert.Equal(1, published);
    }

    [Fact]
    public async Task Reusing_a_withdrawal_key_for_a_different_amount_is_rejected()
    {
        var id = Guid.NewGuid();

        await PostWithdrawalAsync(id, 25m);
        var reused = await PostWithdrawalAsync(id, 90m);
        var fetched = await _client.GetFromJsonAsync<WithdrawalBody>($"/cash-withdrawals/{id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        Assert.Equal(25m, fetched!.Amount);
    }

    [Fact]
    public async Task Reusing_an_inquiry_key_for_a_different_account_is_rejected()
    {
        var id = Guid.NewGuid();

        await PostInquiryAsync(id);
        var reused = await PostInquiryAsync(id, "1001001002");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
    }

    [Fact]
    public async Task Balance_inquiry_stays_pending_until_a_consumer_answers()
    {
        var id = Guid.NewGuid();

        var first = await PostInquiryAsync(id);
        var second = await PostInquiryAsync(id);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("Pending", first.Body!.Status);
        Assert.Null(first.Body.AvailableBalance);
        Assert.False(first.Body.Duplicate);
        Assert.True(second.Body!.Duplicate);
        Assert.Equal(id, second.Body.InquiryId);
    }

    [Fact]
    public async Task A_declined_withdrawal_shows_the_reason_and_its_audit_trail()
    {
        var id = Guid.NewGuid();

        await PostWithdrawalAsync(id, 15m);
        _factory.Services.GetRequiredService<RecordWithdrawalOutcome>()
            .Apply(id, RecordWithdrawalOutcome.Declined, "InsufficientFunds");
        var fetched = await _client.GetFromJsonAsync<WithdrawalBody>($"/cash-withdrawals/{id}");
        var audit = await _client.GetFromJsonAsync<AuditBody>($"/cash-withdrawals/{id}/audit");

        Assert.Equal("Declined", fetched!.Status);
        Assert.Equal("InsufficientFunds", fetched.Reason);
        Assert.Equal(["Accepted", "Published", "Declined"], audit!.Entries.Select(entry => entry.Event));
    }

    [Fact]
    public async Task Audit_for_an_unknown_withdrawal_is_not_found()
    {
        var response = await _client.GetAsync($"/cash-withdrawals/{Guid.NewGuid()}/audit");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Missing_idempotency_key_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/cash-withdrawals", new
        {
            accountNumber = "1001001001",
            amount = 10m,
            currency = "USD",
            tellerId = "teller-1",
            branchId = "branch-9"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(HttpStatusCode StatusCode, WithdrawalBody? Body)> PostWithdrawalAsync(Guid id, decimal amount)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/cash-withdrawals")
        {
            Content = JsonContent.Create(new
            {
                accountNumber = "1001001001",
                amount,
                currency = "USD",
                tellerId = "teller-1",
                branchId = "branch-9"
            })
        };
        request.Headers.Add(TellerEndpoints.IdempotencyHeader, id.ToString());
        var response = await _client.SendAsync(request);
        var body = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<WithdrawalBody>() : null;
        return (response.StatusCode, body);
    }

    private async Task<(HttpStatusCode StatusCode, InquiryBody? Body)> PostInquiryAsync(Guid id, string accountNumber = "1001001001")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/balance-inquiries")
        {
            Content = JsonContent.Create(new
            {
                accountNumber,
                tellerId = "teller-1",
                branchId = "branch-9"
            })
        };
        request.Headers.Add(TellerEndpoints.IdempotencyHeader, id.ToString());
        var response = await _client.SendAsync(request);
        var body = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<InquiryBody>() : null;
        return (response.StatusCode, body);
    }

    private sealed record WithdrawalBody(
        Guid TransactionId,
        string AccountNumber,
        decimal Amount,
        string Currency,
        string Status,
        string? Reason,
        bool Duplicate);

    private sealed record AuditBody(Guid TransactionId, List<AuditEntryBody> Entries);

    private sealed record AuditEntryBody(DateTimeOffset At, string Event, string? Detail);

    private sealed record InquiryBody(
        Guid InquiryId,
        string Status,
        decimal? AvailableBalance,
        bool Duplicate);
}
