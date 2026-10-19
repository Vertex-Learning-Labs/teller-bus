using System.Net;
using System.Net.Http.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Teller.Worker;

public sealed class CoreHostUnavailableException(int statusCode, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;
}

public sealed record HostBalance(bool Found, string AccountNumber, decimal AvailableBalance, string Currency);

public sealed record HostWithdrawal(string Outcome, string? Reason, decimal AvailableBalance, string Currency, bool Duplicate);

public sealed class CoreHostClient(HttpClient http)
{
    public async Task<HostBalance> GetBalanceAsync(string accountNumber, CancellationToken cancellationToken)
    {
        using var response = await Send(
            "Balance inquiry",
            () => http.GetAsync($"/accounts/{Uri.EscapeDataString(accountNumber)}/balance", cancellationToken),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new HostBalance(false, accountNumber, 0, "");

        if (!response.IsSuccessStatusCode)
            throw new CoreHostUnavailableException((int)response.StatusCode, $"Balance inquiry returned {(int)response.StatusCode}.");

        var body = await response.Content.ReadFromJsonAsync<BalanceBody>(cancellationToken)
            ?? throw new CoreHostUnavailableException(200, "Balance inquiry returned an empty body.");

        return new HostBalance(true, body.AccountNumber, body.AvailableBalance, body.Currency);
    }

    public async Task<HostWithdrawal> WithdrawAsync(
        string accountNumber,
        Guid transactionId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        using var response = await Send(
            "Withdrawal",
            () => http.PostAsJsonAsync(
                $"/accounts/{Uri.EscapeDataString(accountNumber)}/withdrawals",
                new { transactionId, amount, currency },
                cancellationToken),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new HostWithdrawal("Declined", "AccountNotFound", 0, currency, false);

        if (!response.IsSuccessStatusCode)
            throw new CoreHostUnavailableException((int)response.StatusCode, $"Withdrawal returned {(int)response.StatusCode}.");

        var body = await response.Content.ReadFromJsonAsync<WithdrawalBody>(cancellationToken)
            ?? throw new CoreHostUnavailableException(200, "Withdrawal returned an empty body.");

        return new HostWithdrawal(body.Outcome, body.Reason, body.AvailableBalance, body.Currency, body.Duplicate);
    }

    // Timeouts and an open circuit mean the outcome is unknown, not that the request was bad. The host dedupes
    // on transactionId, so these are surfaced as CoreHostUnavailableException for the bus retry to pick up.
    // Cancellation requested by the caller (for example a consumer shutting down) is left alone.
    private static async Task<HttpResponseMessage> Send(
        string operation,
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        try
        {
            return await send();
        }
        catch (TimeoutRejectedException exception)
        {
            throw new CoreHostUnavailableException(504, $"{operation} timed out.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CoreHostUnavailableException(504, $"{operation} timed out.", exception);
        }
        catch (BrokenCircuitException exception)
        {
            throw new CoreHostUnavailableException(503, $"{operation} skipped: host circuit is open.", exception);
        }
    }

    private sealed record BalanceBody(string AccountNumber, decimal AvailableBalance, string Currency, string Status);

    private sealed record WithdrawalBody(
        Guid TransactionId,
        string AccountNumber,
        string Outcome,
        string? Reason,
        decimal AvailableBalance,
        string Currency,
        bool Duplicate);
}
