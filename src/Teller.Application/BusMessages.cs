namespace Teller.Application;

public sealed record BalanceInquiryRequest(
    Guid InquiryId,
    string AccountNumber,
    string TellerId,
    string BranchId,
    DateTimeOffset RequestedAt);

public sealed record BalanceInquiryCompleted(
    Guid InquiryId,
    string AccountNumber,
    bool Found,
    decimal? AvailableBalance,
    string? Currency);

public sealed record CashWithdrawalCompleted(
    Guid TransactionId,
    string Outcome,
    string? Reason,
    decimal? AvailableBalance,
    string? Currency);
