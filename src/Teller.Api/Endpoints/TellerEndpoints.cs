using Teller.Application;
using Teller.Domain;

namespace Teller.Api.Endpoints;

public static class TellerEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapTellerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/balance-inquiries", Inquire);
        app.MapPost("/cash-withdrawals", Withdraw);
        app.MapGet("/cash-withdrawals/{transactionId:guid}", GetWithdrawal);
        return app;
    }

    private static async Task<IResult> Inquire(
        BalanceInquiryBody? body,
        RequestBalanceInquiry inquiries,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCommand(request, body?.AccountNumber, body?.TellerId, body?.BranchId, out var id, out var account, out var error))
            return error;

        var outcome = await inquiries.Handle(new BalanceInquiryCommand(id, account, body!.TellerId!, body.BranchId!), cancellationToken);
        if (outcome.PayloadMismatch)
            return KeyReused(id);

        return Results.Ok(ToInquiryResponse(outcome.Inquiry, outcome.Duplicate));
    }

    private static async Task<IResult> Withdraw(
        CashWithdrawalBody? body,
        RequestCashWithdrawal withdrawals,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCommand(request, body?.AccountNumber, body?.TellerId, body?.BranchId, out var id, out var account, out var error))
            return error;

        if (!Money.TryCreate(body!.Amount, body.Currency, out var amount))
            return Results.BadRequest(new { title = "amount must be greater than zero and currency must be a 3-letter code." });

        var outcome = await withdrawals.Handle(new CashWithdrawalCommand(id, account, amount, body.TellerId!, body.BranchId!), cancellationToken);
        if (outcome.PayloadMismatch)
            return KeyReused(id);

        return Results.Accepted($"/cash-withdrawals/{outcome.Withdrawal.TransactionId}", ToWithdrawalResponse(outcome.Withdrawal, outcome.Duplicate));
    }

    private static IResult GetWithdrawal(Guid transactionId, GetCashWithdrawal withdrawals)
    {
        var withdrawal = withdrawals.Handle(transactionId);
        return withdrawal is null
            ? Results.NotFound(new { title = "Withdrawal was not found.", transactionId })
            : Results.Ok(ToWithdrawalResponse(withdrawal, duplicate: false));
    }

    private static bool TryCommand(
        HttpRequest request,
        string? accountNumber,
        string? tellerId,
        string? branchId,
        out Guid id,
        out AccountNumber account,
        out IResult error)
    {
        id = default;
        account = default;
        error = Results.Ok();

        if (!request.Headers.TryGetValue(IdempotencyHeader, out var key) || !Guid.TryParse(key, out id) || id == Guid.Empty)
        {
            error = Results.BadRequest(new { title = $"{IdempotencyHeader} must be a GUID." });
            return false;
        }

        if (!AccountNumber.TryCreate(accountNumber, out account))
        {
            error = Results.BadRequest(new { title = "accountNumber must be 6 to 20 digits." });
            return false;
        }

        if (string.IsNullOrWhiteSpace(tellerId) || string.IsNullOrWhiteSpace(branchId))
        {
            error = Results.BadRequest(new { title = "tellerId and branchId are required." });
            return false;
        }

        return true;
    }

    private static IResult KeyReused(Guid id) =>
        Results.UnprocessableEntity(new { title = $"{IdempotencyHeader} was already used for a different request.", idempotencyKey = id });

    private static object ToWithdrawalResponse(Withdrawal withdrawal, bool duplicate) => new
    {
        transactionId = withdrawal.TransactionId,
        accountNumber = withdrawal.AccountNumber.Value,
        amount = withdrawal.Amount.Amount,
        currency = withdrawal.Amount.Currency,
        tellerId = withdrawal.TellerId,
        branchId = withdrawal.BranchId,
        status = withdrawal.Status.ToString(),
        duplicate,
        requestedAt = withdrawal.RequestedAt
    };

    private static object ToInquiryResponse(BalanceInquiry inquiry, bool duplicate) => new
    {
        inquiryId = inquiry.InquiryId,
        accountNumber = inquiry.AccountNumber,
        tellerId = inquiry.TellerId,
        branchId = inquiry.BranchId,
        status = inquiry.Status.ToString(),
        availableBalance = inquiry.AvailableBalance,
        currency = inquiry.Currency,
        duplicate,
        requestedAt = inquiry.RequestedAt
    };
}

public sealed record BalanceInquiryBody(string? AccountNumber, string? TellerId, string? BranchId);

public sealed record CashWithdrawalBody(string? AccountNumber, decimal Amount, string? Currency, string? TellerId, string? BranchId);
