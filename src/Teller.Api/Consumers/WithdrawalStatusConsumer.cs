using MassTransit;
using Teller.Application;

namespace Teller.Api.Consumers;

public sealed class WithdrawalStatusConsumer(RecordWithdrawalOutcome outcomes) : IConsumer<CashWithdrawalCompleted>
{
    public Task Consume(ConsumeContext<CashWithdrawalCompleted> context)
    {
        outcomes.Apply(context.Message.TransactionId, context.Message.Outcome);
        return Task.CompletedTask;
    }
}

public sealed class WithdrawalFaultConsumer(RecordWithdrawalOutcome outcomes) : IConsumer<Fault<CashWithdrawalRequested>>
{
    public Task Consume(ConsumeContext<Fault<CashWithdrawalRequested>> context)
    {
        outcomes.Apply(context.Message.Message.TransactionId, RecordWithdrawalOutcome.NeedsReview);
        return Task.CompletedTask;
    }
}
