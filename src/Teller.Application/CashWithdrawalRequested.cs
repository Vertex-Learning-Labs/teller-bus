namespace Teller.Application;

public sealed record CashWithdrawalRequested(
    Guid TransactionId,
    string AccountNumber,
    decimal Amount,
    string Currency,
    string TellerId,
    string BranchId,
    DateTimeOffset RequestedAt);
