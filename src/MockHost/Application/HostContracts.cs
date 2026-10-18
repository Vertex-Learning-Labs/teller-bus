using MockHost.Domain;

namespace MockHost.Application;

public sealed record BalanceSnapshot(
    string AccountNumber,
    decimal AvailableBalance,
    string Currency,
    AccountStatus Status);

public sealed record WithdrawalReceipt(
    Guid TransactionId,
    string AccountNumber,
    WithdrawalOutcome Outcome,
    DeclineReason? Reason,
    decimal AvailableBalance,
    string Currency,
    bool Duplicate);

public interface IHostLedger
{
    BalanceSnapshot? FindBalance(AccountNumber number);

    WithdrawalReceipt? TryWithdraw(AccountNumber number, Guid transactionId, Money amount);
}
