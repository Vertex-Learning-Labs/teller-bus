using Teller.Domain;

namespace Teller.Application;

public sealed record CashWithdrawalCommand(
    Guid TransactionId,
    AccountNumber AccountNumber,
    Money Amount,
    string TellerId,
    string BranchId);

public sealed record CashWithdrawalOutcome(Withdrawal Withdrawal, bool Duplicate, bool PayloadMismatch = false);

public sealed class RequestCashWithdrawal(IWithdrawalRepository repository, IWithdrawalPublisher publisher, TimeProvider clock)
{
    public async Task<CashWithdrawalOutcome> Handle(CashWithdrawalCommand command, CancellationToken cancellationToken = default)
    {
        var stored = repository.Find(command.TransactionId);
        var duplicate = stored is not null;
        if (stored is null)
        {
            var withdrawal = Withdrawal.Accept(
                command.TransactionId,
                command.AccountNumber,
                command.Amount,
                command.TellerId,
                command.BranchId,
                clock.GetUtcNow());

            stored = repository.AddOrGet(withdrawal);
            duplicate = !ReferenceEquals(stored, withdrawal);
        }

        if (!stored.Matches(command.AccountNumber, command.Amount, command.TellerId, command.BranchId))
            return new CashWithdrawalOutcome(stored, Duplicate: true, PayloadMismatch: true);

        // A withdrawal is stored before it is published. If an earlier publish failed, a retry with the
        // same key publishes it again; the host dedupes on TransactionId, so a second copy is harmless.
        if (!stored.Published && stored.Status == WithdrawalStatus.Accepted)
            await Publish(stored, cancellationToken);

        return new CashWithdrawalOutcome(stored, duplicate);
    }

    private async Task Publish(Withdrawal withdrawal, CancellationToken cancellationToken)
    {
        await publisher.Publish(new CashWithdrawalRequested(
            withdrawal.TransactionId,
            withdrawal.AccountNumber.Value,
            withdrawal.Amount.Amount,
            withdrawal.Amount.Currency,
            withdrawal.TellerId,
            withdrawal.BranchId,
            withdrawal.RequestedAt), cancellationToken);

        withdrawal.MarkPublished();
    }
}
