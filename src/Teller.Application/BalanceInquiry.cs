namespace Teller.Application;

public enum BalanceInquiryStatus
{
    Pending,
    Answered,
    UnknownAccount,
    TimedOut
}

public sealed record BalanceInquiry(
    Guid InquiryId,
    string AccountNumber,
    string TellerId,
    string BranchId,
    BalanceInquiryStatus Status,
    decimal? AvailableBalance,
    string? Currency,
    DateTimeOffset RequestedAt);
