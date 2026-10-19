using MassTransit;
using Teller.Application;

namespace Teller.Api.Consumers;

public sealed class WithdrawalStatusConsumer(RecordWithdrawalOutcome outcomes, IWithdrawalAuditLog audit) : IConsumer<CashWithdrawalCompleted>
{
    public Task Consume(ConsumeContext<CashWithdrawalCompleted> context)
    {
        var message = context.Message;
        audit.Record(message.TransactionId, WithdrawalAuditEvents.HostAnswered, Describe(message));
        outcomes.Apply(message.TransactionId, message.Outcome, message.Reason);
        return Task.CompletedTask;
    }

    private static string Describe(CashWithdrawalCompleted message)
    {
        var reason = message.Reason is null ? "" : $" ({message.Reason})";
        var balance = message.AvailableBalance is null
            ? ""
            : FormattableString.Invariant($", available {message.AvailableBalance} {message.Currency}");
        var replay = message.HostDuplicate ? ", host returned the receipt of an earlier attempt" : "";
        return $"{message.Outcome}{reason}{balance}, worker attempt {message.Attempt}{replay}.";
    }
}

public sealed class WithdrawalFaultConsumer(RecordWithdrawalOutcome outcomes, IWithdrawalAuditLog audit) : IConsumer<Fault<CashWithdrawalRequested>>
{
    public Task Consume(ConsumeContext<Fault<CashWithdrawalRequested>> context)
    {
        var transactionId = context.Message.Message.TransactionId;
        var cause = context.Message.Exceptions.FirstOrDefault();
        audit.Record(
            transactionId,
            WithdrawalAuditEvents.HostFaulted,
            cause is null ? "Worker gave up without exception details." : $"Worker gave up: {cause.ExceptionType}: {cause.Message}");

        outcomes.Apply(transactionId, RecordWithdrawalOutcome.NeedsReview, RecordWithdrawalOutcome.HostOutcomeUnknown);
        return Task.CompletedTask;
    }
}
