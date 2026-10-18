using MockHost.Application;
using MockHost.Domain;
using MockHost.Infrastructure;

namespace MockHost.Endpoints;

public static class AccountEndpoints
{
    public const string FaultHeader = "X-Mock-Fault";

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/accounts");
        accounts.MapGet("/{accountNumber}/balance", GetBalance);
        accounts.MapPost("/{accountNumber}/withdrawals", PostWithdrawal);
        return app;
    }

    private static IResult GetBalance(string accountNumber, BalanceInquiryService inquiry, HttpRequest request)
    {
        if (TryFault(request, out var faultResult))
            return faultResult;

        if (!AccountNumber.TryCreate(accountNumber, out var number))
            return Results.BadRequest(new { title = "Account number must be 6 to 20 digits." });

        var balance = inquiry.Get(number);
        return balance is null
            ? Results.NotFound(new { title = "Account was not found.", accountNumber })
            : Results.Ok(balance);
    }

    private static async Task<IResult> PostWithdrawal(
        string accountNumber,
        WithdrawalBody? body,
        WithdrawalService withdrawals,
        IResponseDelay delay,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var fault = ReadFault(request);
        if (fault == MockFault.Unavailable)
            return Results.Json(new { title = "Host unavailable." }, statusCode: StatusCodes.Status503ServiceUnavailable);

        if (fault == MockFault.RateLimited)
            return Results.Json(new { title = "Host rate limited." }, statusCode: StatusCodes.Status429TooManyRequests);

        if (fault == MockFault.Unknown)
            return Results.BadRequest(new { title = $"Unsupported {FaultHeader} value." });

        if (body is null || body.TransactionId == Guid.Empty)
            return Results.BadRequest(new { title = "transactionId is required." });

        if (body.Amount <= 0)
            return Results.BadRequest(new { title = "amount must be greater than zero." });

        if (!AccountNumber.TryCreate(accountNumber, out var number))
            return Results.BadRequest(new { title = "Account number must be 6 to 20 digits." });

        if (!Money.TryCreate(body.Amount, body.Currency, out var amount))
            return Results.BadRequest(new { title = "currency must be a 3-letter code." });

        var receipt = withdrawals.Post(body.TransactionId, number, amount);
        if (receipt is null)
            return Results.NotFound(new { title = "Account was not found.", accountNumber });

        if (fault == MockFault.TimeoutAfterPost && receipt.Outcome == WithdrawalOutcome.Posted && !receipt.Duplicate)
            await delay.DelayAsync(cancellationToken);

        return Results.Ok(receipt);
    }

    private static bool TryFault(HttpRequest request, out IResult result)
    {
        var fault = ReadFault(request);
        if (fault == MockFault.Unavailable)
        {
            result = Results.Json(new { title = "Host unavailable." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            return true;
        }

        if (fault == MockFault.RateLimited)
        {
            result = Results.Json(new { title = "Host rate limited." }, statusCode: StatusCodes.Status429TooManyRequests);
            return true;
        }

        if (fault == MockFault.TimeoutAfterPost)
        {
            result = Results.BadRequest(new { title = "timeout-after-post applies to withdrawals only." });
            return true;
        }

        if (fault == MockFault.Unknown)
        {
            result = Results.BadRequest(new { title = $"Unsupported {FaultHeader} value." });
            return true;
        }

        result = Results.Ok();
        return false;
    }

    private static MockFault ReadFault(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(FaultHeader, out var value) || string.IsNullOrWhiteSpace(value))
            return MockFault.None;

        return value.ToString().Trim().ToLowerInvariant() switch
        {
            "unavailable" => MockFault.Unavailable,
            "rate-limited" => MockFault.RateLimited,
            "timeout-after-post" => MockFault.TimeoutAfterPost,
            _ => MockFault.Unknown
        };
    }

    private enum MockFault
    {
        None,
        Unavailable,
        RateLimited,
        TimeoutAfterPost,
        Unknown
    }
}

public sealed record WithdrawalBody(Guid TransactionId, decimal Amount, string? Currency);
