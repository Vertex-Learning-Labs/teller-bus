using MassTransit;
using Teller.Application;

namespace Teller.Worker.Consumers;

public sealed class BalanceInquiryConsumer(CoreHostClient host, ILogger<BalanceInquiryConsumer> logger) : IConsumer<BalanceInquiryRequest>
{
    public async Task Consume(ConsumeContext<BalanceInquiryRequest> context)
    {
        var message = context.Message;
        logger.LogInformation("Balance inquiry {InquiryId} for account {AccountNumber}", message.InquiryId, message.AccountNumber);

        var balance = await host.GetBalanceAsync(message.AccountNumber, context.CancellationToken);
        await context.RespondAsync(new BalanceInquiryCompleted(
            message.InquiryId,
            message.AccountNumber,
            balance.Found,
            balance.Found ? balance.AvailableBalance : null,
            balance.Found ? balance.Currency : null));
    }
}

public sealed class CashWithdrawalConsumer(CoreHostClient host, ILogger<CashWithdrawalConsumer> logger) : IConsumer<CashWithdrawalRequested>
{
    public async Task Consume(ConsumeContext<CashWithdrawalRequested> context)
    {
        var message = context.Message;
        logger.LogInformation("Cash withdrawal {TransactionId} for account {AccountNumber}", message.TransactionId, message.AccountNumber);

        var receipt = await host.WithdrawAsync(
            message.AccountNumber,
            message.TransactionId,
            message.Amount,
            message.Currency,
            context.CancellationToken);

        await context.Publish(new CashWithdrawalCompleted(
            message.TransactionId,
            receipt.Outcome,
            receipt.Reason,
            receipt.AvailableBalance,
            receipt.Currency,
            receipt.Duplicate,
            context.GetRetryAttempt() + 1));
    }
}
